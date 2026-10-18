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
    public async Task FeedIncludesReplyContextAndVisibleLikeTargets()
    {
        await using (var db = factory.CreateDbContext())
        {
            var bobReview = await db.Reviews.SingleAsync(review => review.UserId == "bob");
            var bobComment = await db.Comments.SingleAsync(comment => comment.UserId == "bob");
            var aliceTrack = await db.Tracks.SingleAsync(track => track.Title == "Alice's track");
            var now = DateTime.UtcNow;
            var reply = new Comment
            {
                ReviewId = bobReview.Id,
                UserId = "alice",
                ParentCommentId = bobComment.Id,
                Content = "Alice replied",
                CreatedAt = now.AddSeconds(1)
            };
            db.Comments.Add(reply);
            await db.SaveChangesAsync();

            db.Likes.AddRange(
                new Like { UserId = "bob", ReviewId = await db.Reviews.Where(review => review.UserId == "alice").Select(review => review.Id).SingleAsync(), CreatedAt = now.AddSeconds(5) },
                new Like { UserId = "bob", CommentId = reply.Id, CreatedAt = now.AddSeconds(4) },
                new Like { UserId = "bob", TrackId = aliceTrack.Id, CreatedAt = now.AddSeconds(3) });
            await db.SaveChangesAsync();
        }

        var result = await service.GetPageAsync();
        var replyItem = result.Items.Single(item => item.Type == ActivityType.Comment && item.Content == "Alice replied");
        Assert.That(replyItem.ParentUserName, Is.EqualTo("bob"));

        var likes = result.Items.Where(item => item.Type == ActivityType.Like).ToArray();
        Assert.That(likes.Select(item => item.TargetType),
            Is.EqualTo(new[]
            {
                LikeTargetType.Review,
                LikeTargetType.Comment,
                LikeTargetType.Track
            }));
        Assert.That(likes[0].TargetUserName, Is.EqualTo("alice"));
        Assert.That(likes[1].ParentUserName, Is.EqualTo("bob"));
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
            var aliceReviewId = await db.Reviews.Where(review => review.UserId == "alice").Select(review => review.Id).SingleAsync();
            var bobCommentId = await db.Comments.Where(comment => comment.UserId == "bob").Select(comment => comment.Id).SingleAsync();
            var aliceTrackId = await db.Tracks.Where(track => track.Title == "Alice's track").Select(track => track.Id).SingleAsync();
            db.Likes.AddRange(
                new Like { UserId = "bob", ReviewId = aliceReviewId },
                new Like { UserId = "alice", CommentId = bobCommentId },
                new Like { UserId = "bob", TrackId = aliceTrackId });
            await db.SaveChangesAsync();
        }

        var first = await service.GetPageAsync();
        var second = await service.GetPageAsync(1);
        Assert.That(first.Items, Has.Count.EqualTo(FeedService.PageSize));
        Assert.That(first.HasNext, Is.True);
        Assert.That(second.Items, Has.Count.EqualTo(7));
        Assert.That(second.HasNext, Is.False);
        var allItems = first.Items.Concat(second.Items).ToArray();
        Assert.That(allItems, Has.Length.EqualTo(FeedService.PageSize + 7));
        Assert.That(allItems.Select(item => (item.Type, item.Id)).Distinct().Count(), Is.EqualTo(allItems.Length));
        Assert.That(allItems.Count(item => item.Type == ActivityType.Like), Is.EqualTo(3));
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
