using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SoundBite.Components.Pages;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

// Renders the real /notifications/settings page against an isolated, migrated in-memory database
// and raises its real onchange / onclick events through the renderer (no browser, no JavaScript),
// so the markup's event wiring is exercised, not just the handler methods.
public class NotificationSettingsPageTests
{
    private SqliteConnection connection = null!;
    private TestFactory factory = null!;
    private TestAuthentication authentication = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        factory = new TestFactory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        authentication = new TestAuthentication("alice");

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" },
            new ApplicationUser { Id = "bob", UserName = "bob", DisplayName = "Bob" });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public void PageIsInteractiveSoTheChangeHandlersRun()
    {
        // App.razor makes every routed page interactive unless it opts out with this attribute.
        Assert.That(typeof(NotificationSettings).GetCustomAttribute<ExcludeFromInteractiveRoutingAttribute>(), Is.Null);
    }

    [Test]
    public async Task ShowsEveryCategoryOnByDefault()
    {
        await WithPageAsync(page =>
        {
            var switches = page.Switches();
            Assert.That(switches.Keys, Is.EquivalentTo(Enum.GetValues<NotificationType>()));
            Assert.That(switches.Values, Has.All.True);
            Assert.That(page.Html, Does.Contain("Someone follows you").And.Contain("Someone likes your playlist"));
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ShowsOnlyTheSignedInUsersSavedSettings()
    {
        await using (var db = factory.CreateDbContext())
        {
            db.NotificationOptOuts.AddRange(
                new NotificationOptOut { UserId = "alice", Type = NotificationType.Follow },
                new NotificationOptOut { UserId = "bob", Type = NotificationType.ReviewLike });
            await db.SaveChangesAsync();
        }

        await WithPageAsync(page =>
        {
            Assert.That(page.Switches()[NotificationType.Follow], Is.False);
            Assert.That(page.Switches()[NotificationType.ReviewLike], Is.True);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task ChangesAreSavedStayAfterReloadAndDoNotAffectAnotherUser()
    {
        await using (var db = factory.CreateDbContext())
        {
            db.NotificationOptOuts.Add(new NotificationOptOut { UserId = "bob", Type = NotificationType.PlaylistLike });
            await db.SaveChangesAsync();
        }

        await WithPageAsync(async page =>
        {
            await page.ToggleAsync(NotificationType.CommentReply, false);

            Assert.That(page.Html, Does.Contain("Preferences saved."));
            Assert.That(page.Switches()[NotificationType.CommentReply], Is.False);
            Assert.That(page.SwitchesDisabled(), Is.False);
        });
        Assert.That(await OptOutsAsync("alice"), Is.EqualTo(new[] { NotificationType.CommentReply }));
        Assert.That(await OptOutsAsync("bob"), Is.EqualTo(new[] { NotificationType.PlaylistLike }));

        // A new page (new scope, as after a refresh or sign-in) shows the saved setting.
        await WithPageAsync(async page =>
        {
            Assert.That(page.Switches()[NotificationType.CommentReply], Is.False);
            await page.ToggleAsync(NotificationType.CommentReply, true);
            Assert.That(page.Switches()[NotificationType.CommentReply], Is.True);
        });
        await WithPageAsync(page =>
        {
            Assert.That(page.Switches().Values, Has.All.True);
            return Task.CompletedTask;
        });
        Assert.That(await OptOutsAsync("alice"), Is.Empty);
        Assert.That(await OptOutsAsync("bob"), Is.EqualTo(new[] { NotificationType.PlaylistLike }));

        // Bob sees only his own setting.
        authentication.UserId = "bob";
        await WithPageAsync(page =>
        {
            Assert.That(page.Switches().Where(entry => !entry.Value).Select(entry => entry.Key),
                Is.EqualTo(new[] { NotificationType.PlaylistLike }));
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task SwitchShowsTheNewStateWhileSavingAndLocksTheSwitches()
    {
        await WithPageAsync(async page =>
        {
            var gate = new TaskCompletionSource();
            authentication.Gate = gate;
            var save = page.ToggleAsync(NotificationType.Follow, false);

            Assert.That(page.Switches()[NotificationType.Follow], Is.False);
            Assert.That(page.SwitchesDisabled(), Is.True);

            gate.SetResult();
            await save;

            Assert.That(page.Switches()[NotificationType.Follow], Is.False);
            Assert.That(page.SwitchesDisabled(), Is.False);
            Assert.That(page.Html, Does.Contain("Preferences saved."));
        });
        Assert.That(await OptOutsAsync("alice"), Is.EqualTo(new[] { NotificationType.Follow }));
    }

    [Test]
    public async Task FailedSaveRestoresTheSwitchAndShowsAnError()
    {
        await WithPageAsync(async page =>
        {
            var gate = new TaskCompletionSource();
            authentication.Gate = gate;
            authentication.FailNext = true;
            var save = page.ToggleAsync(NotificationType.Follow, false);

            // The new state is shown first, while the save is still running.
            Assert.That(page.Switches()[NotificationType.Follow], Is.False);

            gate.SetResult();
            await save;

            // Then the switch goes back to what is actually saved.
            Assert.That(page.Switches()[NotificationType.Follow], Is.True);
            Assert.That(page.SwitchesDisabled(), Is.False);
            Assert.That(page.Html, Does.Contain("couldn&#x27;t be saved").Or.Contain("couldn't be saved"));
            Assert.That(page.Html, Does.Not.Contain("Preferences saved."));

            // The switch still works once the problem has passed.
            await page.ToggleAsync(NotificationType.Follow, false);
            Assert.That(page.Switches()[NotificationType.Follow], Is.False);
            Assert.That(page.Html, Does.Contain("Preferences saved."));
        });
        Assert.That(await OptOutsAsync("alice"), Is.EqualTo(new[] { NotificationType.Follow }));
    }

    [Test]
    public async Task FailedSaveOfAnAlreadyDisabledCategoryRestoresItToOff()
    {
        await using (var db = factory.CreateDbContext())
        {
            db.NotificationOptOuts.Add(new NotificationOptOut { UserId = "alice", Type = NotificationType.ReviewLike });
            await db.SaveChangesAsync();
        }

        await WithPageAsync(async page =>
        {
            authentication.FailNext = true;
            await page.ToggleAsync(NotificationType.ReviewLike, true);

            Assert.That(page.Switches()[NotificationType.ReviewLike], Is.False);
        });
        Assert.That(await OptOutsAsync("alice"), Is.EqualTo(new[] { NotificationType.ReviewLike }));
    }

    [Test]
    public async Task RetryHidesTheSwitchesWhileLoadingThenShowsTheSavedSettings()
    {
        // The service rejects a signed-in user whose account no longer exists, so the first load fails.
        await using (var db = factory.CreateDbContext())
            await db.Users.Where(user => user.Id == "alice").ExecuteDeleteAsync();

        await WithPageAsync(async page =>
        {
            Assert.That(page.Html, Does.Contain("couldn").And.Contain("Try again"));
            Assert.That(page.Switches(), Is.Empty);

            await using (var db = factory.CreateDbContext())
            {
                db.Users.Add(new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" });
                db.NotificationOptOuts.Add(new NotificationOptOut { UserId = "alice", Type = NotificationType.CommentLike });
                await db.SaveChangesAsync();
            }

            var gate = new TaskCompletionSource();
            authentication.Gate = gate;
            var retry = page.ClickTryAgainAsync();

            // While the retry loads there is nothing to edit.
            Assert.That(page.Html, Does.Contain("Loading notification settings"));
            Assert.That(page.Switches(), Is.Empty);
            Assert.That(page.Html, Does.Not.Contain("Try again"));

            gate.SetResult();
            await retry;

            Assert.That(page.Html, Does.Not.Contain("Loading notification settings").And.Not.Contain("Try again"));
            Assert.That(page.Switches().Where(entry => !entry.Value).Select(entry => entry.Key),
                Is.EqualTo(new[] { NotificationType.CommentLike }));
        });
    }

    [Test]
    public async Task FailedRetryShowsTryAgainAgainAndIsNotStuckLoading()
    {
        await using (var db = factory.CreateDbContext())
            await db.Users.Where(user => user.Id == "alice").ExecuteDeleteAsync();

        await WithPageAsync(async page =>
        {
            await page.ClickTryAgainAsync(); // still failing

            Assert.That(page.Html, Does.Contain("Try again"));
            Assert.That(page.Html, Does.Not.Contain("Loading notification settings"));
            Assert.That(page.Switches(), Is.Empty);
        });
    }

    [Test]
    public async Task SignedOutVisitorIsAskedToLogInWithoutSettings()
    {
        authentication.UserId = null;

        await WithPageAsync(page =>
        {
            Assert.That(page.Html, Does.Contain("href=\"/login\""));
            Assert.That(page.Switches(), Is.Empty);
            return Task.CompletedTask;
        });
    }

    private async Task<List<NotificationType>> OptOutsAsync(string userId)
    {
        await using var db = factory.CreateDbContext();
        return await db.NotificationOptOuts.Where(optOut => optOut.UserId == userId)
            .OrderBy(optOut => optOut.Type).Select(optOut => optOut.Type).ToListAsync();
    }

    private async Task WithPageAsync(Func<SettingsPage, Task> test)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDbContextFactory<ApplicationDbContext>>(factory)
            .AddSingleton<AuthenticationStateProvider>(authentication)
            .AddSingleton(new NotificationEventDispatcher(NullLogger<NotificationEventDispatcher>.Instance))
            .AddScoped<NotificationService>()
            .BuildServiceProvider();
        await using var renderer = new TestRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<NotificationSettings>(ParameterView.Empty);
            await test(new SettingsPage(renderer, root));
        });
    }

    private sealed class TestRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, loggerFactory)
    {
        // The page is the only (root) component rendered, so it is the first component the renderer creates.
        private const int PageComponentId = 0;

        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);

        public async Task<HtmlRootComponent> RenderComponentAsync<T>(ParameterView parameters) where T : IComponent
        {
            var root = BeginRenderingComponent(typeof(T), parameters);
            await root.QuiescenceTask;
            return root;
        }

        // Reading the render tree is how a test finds the handler the markup attached; the framework warns
        // that these types may change, which is acceptable in a test (a change would make the tests fail loudly).
#pragma warning disable BL0006
        // Raises the page's real event for the element (matched by its id attribute, or by name if no id is given),
        // exactly as the browser would: it finds the handler the markup attached and calls it.
        public Task RaiseEventAsync(string elementName, string? elementId, string eventName, EventArgs args)
        {
            var handlerId = FindHandlerId(elementName, elementId, eventName);
            return Dispatcher.InvokeAsync(() => DispatchEventAsync(handlerId, null, args));
        }

        private ulong FindHandlerId(string elementName, string? elementId, string eventName)
        {
            var frames = GetCurrentRenderTreeFrames(PageComponentId);
            for (var index = 0; index < frames.Count; index++)
            {
                var element = frames.Array[index];
                if (element.FrameType != RenderTreeFrameType.Element || element.ElementName != elementName)
                    continue;

                // An element's attribute frames follow it directly.
                var attributes = new List<RenderTreeFrame>();
                for (var next = index + 1; next < frames.Count && frames.Array[next].FrameType == RenderTreeFrameType.Attribute; next++)
                    attributes.Add(frames.Array[next]);

                if (elementId is not null && !attributes.Any(attribute => attribute.AttributeName == "id" &&
                        Equals(attribute.AttributeValue, elementId)))
                    continue;
                var handler = attributes.FirstOrDefault(attribute => attribute.AttributeName == eventName);
                if (handler.AttributeEventHandlerId != 0)
                    return handler.AttributeEventHandlerId;
            }
            throw new InvalidOperationException($"The page has no <{elementName}> {elementId} with an {eventName} handler.");
        }
#pragma warning restore BL0006
    }

    // The rendered page: what is displayed, and the events a user can raise.
    private sealed class SettingsPage(TestRenderer renderer, HtmlRootComponent root)
    {
        public string Html => root.ToHtmlString();

        // Ticks or unticks a switch, as the browser's change event does (checkboxes report true/false).
        public Task ToggleAsync(NotificationType type, bool enabled) =>
            renderer.RaiseEventAsync("input", $"notify-{type}", "onchange", new ChangeEventArgs { Value = enabled });

        public Task ClickTryAgainAsync() =>
            renderer.RaiseEventAsync("button", null, "onclick", new MouseEventArgs());

        // Each displayed switch: true if it is shown as checked (notifications on).
        public Dictionary<NotificationType, bool> Switches() =>
            Regex.Matches(Html, "<input[^>]*id=\"notify-(\\w+)\"[^>]*>")
                .ToDictionary(
                    match => Enum.Parse<NotificationType>(match.Groups[1].Value),
                    match => Regex.IsMatch(match.Value, @"\bchecked\b"));

        // True when the displayed switches are locked (a save is running).
        public bool SwitchesDisabled() =>
            Regex.Matches(Html, "<input[^>]*id=\"notify-\\w+\"[^>]*>")
                .All(match => Regex.IsMatch(match.Value, @"\bdisabled\b"));
    }

    private sealed class TestAuthentication(string? userId) : AuthenticationStateProvider
    {
        public string? UserId { get; set; } = userId;
        public bool FailNext { get; set; }
        public TaskCompletionSource? Gate { get; set; }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var gate = Gate;
            Gate = null;
            if (gate is not null)
                await gate.Task;
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("Simulated settings failure.");
            }
            var identity = UserId is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "test");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
    }

    private sealed class TestFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateDbContext());
    }
}
