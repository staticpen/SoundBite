using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class FeedServiceTests
{
    private SqliteConnection connection = null!;
    private TestFactory factory = null!;
    private TestAuthentication authentication = null!;
    private FeedService service = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        factory = new TestFactory(options);
        authentication = new TestAuthentication("alice");
        service = new FeedService(factory, authentication);

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" },
            new ApplicationUser { Id = "bob", UserName = "bob", DisplayName = "Bob" },
            new ApplicationUser { Id = "carol", UserName = "carol", DisplayName = "Carol" });
        db.Tracks.AddRange(
            new Track { Title = "Alice's track" },
            new Track { Title = "Bob's track" },
            new Track { Title = "Carol's track" });
        await db.SaveChangesAsync();
        var tracks = await db.Tracks.OrderBy(track => track.Id).ToListAsync();
        db.Follows.Add(new Follow { FollowerId = "alice", FollowingId = "bob" });
        db.Reviews.AddRange(
            new Review { UserId = "alice", TrackId = tracks[0].Id, Rating = 5, Comment = "My review", CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new Review { UserId = "bob", TrackId = tracks[1].Id, Rating = 4, Comment = "Bob's review", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new Review { UserId = "carol", TrackId = tracks[2].Id, Rating = 1, Comment = "Private to this feed", CreatedAt = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();
        var bobReviewId = await db.Reviews.Where(review => review.UserId == "bob").Select(review => review.Id).SingleAsync();
        db.Comments.Add(new Comment
        {
            ReviewId = bobReviewId,
            UserId = "bob",
            Content = "Bob commented",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public async Task FeedContainsOwnAndFollowedReviewsAndCommentsInNewestFirstOrder()
    {
        var result = await service.GetPageAsync();
        Assert.That(result.HasNext, Is.False);
        Assert.That(result.Items.Select(item => item.Type),
            Is.EqualTo(new[] { ActivityType.Comment, ActivityType.Review, ActivityType.Review }));
        Assert.That(result.Items.Select(item => item.UserName),
            Is.EqualTo(new[] { "bob", "bob", "alice" }));
        Assert.That(result.Items.Select(item => item.TrackTitle),
            Is.EqualTo(new[] { "Bob's track", "Bob's track", "Alice's track" }));
        Assert.That(result.Items[0].Content, Is.EqualTo("Bob commented"));
        Assert.That(result.Items[0].ReviewId, Is.EqualTo(result.Items[1].ReviewId));
        Assert.That(result.Items.Any(item => item.UserName == "carol"), Is.False);
    }

    [Test]
    public void AnonymousAndInvalidPageRequestsAreRejected()
    {
        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPageAsync());
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPageAsync(-1));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPageAsync(int.MaxValue));
    }

    [Test]
    public async Task FeedPagesAreBoundedAndHaveStableHasNext()
    {
        await using (var db = factory.CreateDbContext())
        {
            var reviewId = await db.Reviews.Where(review => review.UserId == "bob").Select(review => review.Id).SingleAsync();
            db.Comments.AddRange(Enumerable.Range(0, FeedService.PageSize + 1).Select(i => new Comment
            {
                ReviewId = reviewId,
                UserId = "bob",
                Content = $"Comment {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i)
            }));
            await db.SaveChangesAsync();
        }

        var first = await service.GetPageAsync();
        var second = await service.GetPageAsync(1);
        Assert.That(first.Items, Has.Count.EqualTo(FeedService.PageSize));
        Assert.That(first.HasNext, Is.True);
        Assert.That(second.Items, Has.Count.EqualTo(4));
        Assert.That(second.HasNext, Is.False);
    }

    private sealed class TestAuthentication(string? userId) : AuthenticationStateProvider
    {
        public void Set(string? id) => userId = id;
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = userId is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
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
