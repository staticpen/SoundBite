using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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

// Renders the real /notifications page and drives its existing "Load more" and refresh handlers,
// the same way CatalogueIntegrationTests drives the search page.
public class NotificationsPageTests
{
    private static readonly DateTime Newest = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private SqliteConnection connection = null!;
    private TestFactory factory = null!;
    private TestAuthentication authentication = null!;
    private NotificationEventDispatcher dispatcher = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        factory = new TestFactory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        authentication = new TestAuthentication("alice");
        dispatcher = new NotificationEventDispatcher(NullLogger<NotificationEventDispatcher>.Instance);

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
    public async Task LoadMoreShowsOlderNotificationsWithoutDuplicates()
    {
        await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 30)));
            Assert.That(page.Html, Does.Contain("35 unread").And.Contain("Load more"));

            await page.ClickLoadMoreAsync();

            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 35)));
            Assert.That(page.Html, Does.Not.Contain("Load more"));
        });
    }

    [Test]
    public async Task LiveRefreshResetsOlderPagesAndShowsCurrentReadStates()
    {
        var ids = await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            await page.ClickLoadMoreAsync();
            Assert.That(page.ShownTimes(), Has.Count.EqualTo(35));

            // Changes made elsewhere: two notifications read (one on the older page), one deleted, one new.
            await using (var db = factory.CreateDbContext())
            {
                await db.Notifications.Where(item => item.Id == ids[0] || item.Id == ids[33])
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadAt, (DateTime?)DateTime.UtcNow));
                await db.Notifications.Where(item => item.Id == ids[2]).ExecuteDeleteAsync();
                db.Notifications.Add(NewNotification(Newest.AddMinutes(5)));
                await db.SaveChangesAsync();
            }

            await dispatcher.PublishAsync("alice");

            // Back to the newest 30: the new item, then the old items without the deleted one.
            var expected = new List<long> { Newest.AddMinutes(5).Ticks };
            expected.AddRange(ExpectedTimes(0, 2));
            expected.AddRange(ExpectedTimes(3, 30));
            Assert.That(page.ShownTimes(), Is.EqualTo(expected));
            Assert.That(page.Html, Does.Contain("33 unread").And.Contain("Load more"));
            // 30 shown, only the notification with Id ids[0] is read.
            Assert.That(Regex.Matches(page.Html, "list-group-item-primary").Count, Is.EqualTo(29));

            // Loading the older page again shows the read state that was set elsewhere:
            // list order is new, ids[0], ids[1], ids[3]..ids[34], so ids[0] is at 1 and ids[33] at 33.
            await page.ClickLoadMoreAsync();
            var readPositions = page.ReadFlags().Select((isRead, index) => (isRead, index))
                .Where(entry => entry.isRead).Select(entry => entry.index);
            Assert.That(page.ShownTimes(), Has.Count.EqualTo(35));
            Assert.That(readPositions, Is.EqualTo(new[] { 1, 33 }));
        });
    }

    [Test]
    public async Task LoadMoreFailureKeepsNotificationsAndCanBeRetried()
    {
        await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            authentication.FailNext = true;
            await page.ClickLoadMoreAsync();

            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 30)));
            Assert.That(page.Html, Does.Contain("Older notifications couldn").And.Contain("Load more"));

            await page.ClickLoadMoreAsync();

            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 35)));
            Assert.That(page.Html, Does.Not.Contain("Older notifications couldn"));
        });
    }

    [Test]
    public async Task SuccessfulRefreshClearsTheLoadMoreError()
    {
        await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            authentication.FailNext = true;
            await page.ClickLoadMoreAsync();
            Assert.That(page.Html, Does.Contain("Older notifications couldn"));

            await dispatcher.PublishAsync("alice");

            Assert.That(page.Html, Does.Not.Contain("Older notifications couldn"));
            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 30)));
        });
    }

    [Test]
    public async Task LoadMoreResultIsIgnoredIfARefreshStartedAfterItBegan()
    {
        await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            // Hold the Load more request while it is in flight.
            var gate = new TaskCompletionSource();
            authentication.Gate = gate;
            var loadMore = page.ClickLoadMoreAsync();

            await using (var db = factory.CreateDbContext())
            {
                db.Notifications.Add(NewNotification(Newest.AddMinutes(5)));
                await db.SaveChangesAsync();
            }
            await dispatcher.PublishAsync("alice");

            gate.SetResult();
            await loadMore;

            // The refreshed list stays as the newest 30; the late older page was not appended.
            var expected = new List<long> { Newest.AddMinutes(5).Ticks };
            expected.AddRange(ExpectedTimes(0, 29));
            Assert.That(page.ShownTimes(), Is.EqualTo(expected));
            Assert.That(page.Html, Does.Contain("36 unread").And.Contain("Load more"));
        });
    }

    [Test]
    public async Task FailedRefreshOverlappingLoadMoreKeepsTheListAndRecoversOnRetry()
    {
        await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            // Load more is in flight when a live refresh starts, and the refresh fails.
            var gate = new TaskCompletionSource();
            authentication.Gate = gate;
            var loadMore = page.ClickLoadMoreAsync();
            authentication.FailNext = true;
            await dispatcher.PublishAsync("alice");
            gate.SetResult();
            await loadMore;

            // The list that was already shown is kept, with an error and a retry; Load more is not stuck.
            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 30)));
            Assert.That(page.Html, Does.Contain("Notifications couldn").And.Contain("Try again")
                .And.Contain("Load more").And.Not.Contain("Loading..."));
            Assert.That(Regex.IsMatch(page.Html, @"<button[^>]*\bdisabled\b[^>]*>\s*Load more"), Is.False);

            // Retrying refreshes successfully, and Load more works again.
            await page.ClickTryAgainAsync();
            Assert.That(page.Html, Does.Not.Contain("Notifications couldn").And.Not.Contain("Try again"));
            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 30)));

            await page.ClickLoadMoreAsync();
            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 35)));
        });
    }

    [Test]
    public async Task InitialLoadFailureOffersRetry()
    {
        // The service rejects a signed-in user whose account no longer exists, so the first load fails.
        await using (var db = factory.CreateDbContext())
            await db.Users.Where(user => user.Id == "alice").ExecuteDeleteAsync();

        await WithPageAsync(async page =>
        {
            Assert.That(page.Html, Does.Contain("Notifications couldn").And.Contain("Try again"));
            Assert.That(page.ShownTimes(), Is.Empty);

            await using (var db = factory.CreateDbContext())
            {
                db.Users.Add(new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" });
                await db.SaveChangesAsync();
            }
            await SeedAsync(3);
            await page.ClickTryAgainAsync();

            Assert.That(page.ShownTimes(), Is.EqualTo(ExpectedTimes(0, 3)));
            Assert.That(page.Html, Does.Not.Contain("Try again"));
        });
    }

    [Test]
    public async Task LoadMoreIsDisabledWhileARefreshIsRunning()
    {
        await SeedAsync(35);

        await WithPageAsync(async page =>
        {
            var gate = new TaskCompletionSource();
            authentication.Gate = gate;
            var refresh = dispatcher.PublishAsync("alice");

            Assert.That(Regex.IsMatch(page.Html, @"<button[^>]*\bdisabled\b[^>]*>\s*Load more"), Is.True);

            gate.SetResult();
            await refresh;

            Assert.That(Regex.IsMatch(page.Html, @"<button[^>]*\bdisabled\b[^>]*>\s*Load more"), Is.False);
        });
    }

    // Creates "count" follow notifications for Alice, one minute apart, newest first. Returns their IDs in that order.
    private async Task<List<int>> SeedAsync(int count)
    {
        await using var db = factory.CreateDbContext();
        var notifications = Enumerable.Range(0, count)
            .Select(index => NewNotification(Newest.AddMinutes(-index)))
            .ToList();
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();
        return notifications.Select(notification => notification.Id).ToList();
    }

    private static Notification NewNotification(DateTime createdAt) => new()
    {
        RecipientUserId = "alice",
        ActorUserId = "bob",
        Type = NotificationType.Follow,
        CreatedAt = createdAt
    };

    // The seeded notification times for positions [from, to), newest first.
    private static List<long> ExpectedTimes(int from, int to) =>
        Enumerable.Range(from, to - from).Select(index => Newest.AddMinutes(-index).Ticks).ToList();

    private async Task WithPageAsync(Func<TestNotificationsPage, Task> test)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDbContextFactory<ApplicationDbContext>>(factory)
            .AddSingleton<AuthenticationStateProvider>(authentication)
            .AddSingleton(dispatcher)
            .AddScoped<NotificationService>()
            .AddScoped<NotificationCircuitHandler>()
            .BuildServiceProvider();
        await using var renderer = new TestRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            TestNotificationsPage? page = null;
            var root = await renderer.RenderComponentAsync<TestNotificationsPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(TestNotificationsPage.Created)] = (Action<TestNotificationsPage>)(created => page = created)
                }));
            page!.Root = root;
            await test(page);
        });
    }

    private sealed class TestRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, loggerFactory)
    {
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);

        public async Task<HtmlRootComponent> RenderComponentAsync<T>(ParameterView parameters) where T : IComponent
        {
            var root = BeginRenderingComponent(typeof(T), parameters);
            await root.QuiescenceTask;
            return root;
        }
    }

    // The real page, plus helpers to click "Load more" and read what is displayed.
    public sealed class TestNotificationsPage : Notifications
    {
        [Parameter] public Action<TestNotificationsPage>? Created { get; set; }
        public HtmlRootComponent? Root { get; set; }

        public string Html => Root!.Value.ToHtmlString();

        protected override void OnInitialized() => Created?.Invoke(this);

        public Task ClickLoadMoreAsync() => InvokeAsync(async () =>
        {
            await (Task)typeof(Notifications).GetMethod("LoadMoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null)!;
            StateHasChanged();
        });

        public Task ClickTryAgainAsync() => InvokeAsync(async () =>
        {
            await (Task)typeof(Notifications).GetMethod("LoadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null)!;
            StateHasChanged();
        });

        // For each listed notification, in display order: true if it is shown as read.
        public List<bool> ReadFlags() =>
            Regex.Matches(Html, "<li class=\"([^\"]*)\"")
                .Select(match => !match.Groups[1].Value.Contains("list-group-item-primary"))
                .ToList();

        // The CreatedAt ticks of the notifications currently listed, in display order.
        public List<long> ShownTimes() =>
            Regex.Matches(Html, "datetime=\"([^\"]+)\"")
                .Select(match => DateTime.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).Ticks)
                .ToList();
    }

    private sealed class TestAuthentication(string userId) : AuthenticationStateProvider
    {
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
                throw new InvalidOperationException("Simulated notification failure.");
            }
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
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
