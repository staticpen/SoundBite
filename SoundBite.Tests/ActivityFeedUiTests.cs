using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundBite.Components.Pages;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class ActivityFeedUiTests
{
    [Test]
    public async Task SignedInUserCanSeeRecentActivityFromUsersTheyDoNotFollow()
    {
        await RenderAsync("alice", async renderer =>
        {
            var root = await renderer.RenderComponentAsync<Home>();
            var html = root.ToHtmlString();
            Assert.That(html, Does.Contain("Activity Feed")
                .And.Contain("Bob")
                .And.Contain("reviewed")
                .And.Contain("Test track")
                .And.Contain("A recent review")
                .And.Contain("replied to")
                .And.Contain("liked a review by")
                .And.Contain("liked the track")
                .And.Contain("Carol</a><span> added </span><a href=\"/tracks/")
                .And.Contain("<span> to Favourites</span>"));
        });
    }

    [Test]
    public async Task AnonymousUserIsPromptedToLogInToSeeCommunityActivity()
    {
        await RenderAsync(null, async renderer =>
        {
            var root = await renderer.RenderComponentAsync<ActivityFeed>();
            Assert.That(root.ToHtmlString(), Does.Contain("Log in").And.Contain("Activity Feed"));
        });
    }

    private static async Task RenderAsync(string? userId, Func<HtmlRenderer, Task> assert)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var factory = new TestFactory(options);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" },
                new ApplicationUser { Id = "bob", UserName = "bob", DisplayName = "Bob" },
                new ApplicationUser { Id = "carol", UserName = "carol", DisplayName = "Carol" });
            db.Tracks.Add(new Track { Title = "Test track" });
            await db.SaveChangesAsync();
            var track = await db.Tracks.SingleAsync();
            db.Follows.Add(new Follow { FollowerId = "alice", FollowingId = "bob" });
            db.Reviews.Add(new Review { UserId = "bob", TrackId = track.Id, Rating = 4, Comment = "A recent review" });
            await db.SaveChangesAsync();
            var review = await db.Reviews.SingleAsync();
            var parent = new Comment { ReviewId = review.Id, UserId = "bob", Content = "A top-level comment" };
            db.Comments.Add(parent);
            await db.SaveChangesAsync();
            db.Comments.Add(new Comment
            {
                ReviewId = review.Id,
                UserId = "alice",
                ParentCommentId = parent.Id,
                Content = "A reply in the feed"
            });
            db.Likes.AddRange(
                new Like { UserId = "bob", ReviewId = review.Id },
                new Like { UserId = "bob", TrackId = track.Id },
                new Like { UserId = "carol", TrackId = track.Id });
            db.FavouriteTracks.AddRange(
                new FavouriteTrack { UserId = "carol", TrackId = track.Id },
                new FavouriteTrack { UserId = "bob", TrackId = track.Id });
            await db.SaveChangesAsync();
        }

        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDbContextFactory<ApplicationDbContext>>(factory)
            .AddScoped<FeedService>()
            .AddScoped<UserService>()
            .AddSingleton<AuthenticationStateProvider>(
                new TestAuthentication(new ClaimsPrincipal(identity)))
            .BuildServiceProvider();
        using (services)
        await using (var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>()))
        {
            await renderer.Dispatcher.InvokeAsync(() => assert(renderer));
        }
    }

    private sealed class TestAuthentication(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class TestFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateDbContext());
    }
}
