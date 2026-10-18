using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundBite.Components.Pages;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class UserProfileTests
{
    private sealed class ProfileHost : ComponentBase
    {
        [Parameter] public string UserName { get; set; } = string.Empty;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(content =>
            {
                content.OpenComponent<UserProfile>(0);
                content.AddAttribute(1, nameof(UserProfile.UserName), UserName);
                content.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    [Test]
    public async Task ProfileDisplaysOnlyTheSelectedUsersFavouritesAndReviews()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .Services
            .AddAuthorizationCore()
            .AddScoped<FavouriteService>()
            .AddScoped<ReviewService>()
            .AddScoped<FollowService>()
            .AddSingleton<NotificationEventDispatcher>()
            .AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider())
            .BuildServiceProvider();

        await using (services)
        {
            var factory = services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
                db.Users.AddRange(
                    new ApplicationUser
                    {
                        Id = "alice",
                        UserName = "alice",
                        NormalizedUserName = "ALICE",
                        Email = "private@example.com",
                        DisplayName = "Alice"
                    },
                    new ApplicationUser
                    {
                        Id = "bob",
                        UserName = "bob",
                        NormalizedUserName = "BOB"
                    });
                db.Tracks.AddRange(
                    new Track { Id = 1, Title = "Alice favourite" },
                    new Track { Id = 2, Title = "Alice reviewed" },
                    new Track { Id = 3, Title = "Bob private track" });
                db.FavouriteTracks.AddRange(
                    new FavouriteTrack { UserId = "alice", TrackId = 1 },
                    new FavouriteTrack { UserId = "bob", TrackId = 3 });
                db.Reviews.AddRange(
                    new Review { UserId = "alice", TrackId = 2, Rating = 5, Comment = "Alice's review" },
                    new Review { UserId = "bob", TrackId = 3, Rating = 4, Comment = "Bob's review" });
                await db.SaveChangesAsync();
            }

            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var root = await renderer.Dispatcher.InvokeAsync(() =>
                renderer.RenderComponentAsync<ProfileHost>(ParameterView.FromDictionary(
                    new Dictionary<string, object?> { [nameof(ProfileHost.UserName)] = "alice" })));
            var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);

            Assert.That(html, Does.Contain("Alice favourite")
                .And.Contain("Alice reviewed")
                .And.Contain("Alice&#x27;s review")
                .And.Contain("5 / 5")
                .And.Not.Contain("Bob private track")
                .And.Not.Contain("Bob's review")
                .And.Not.Contain("private@example.com"));
        }
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
