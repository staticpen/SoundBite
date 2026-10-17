using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class SocialInteractionTests
{
    private SqliteConnection connection = null!;
    private TestFactory factory = null!;
    private TestAuthentication authentication = null!;
    private SocialInteractionService service = null!;
    private int reviewId;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        factory = new TestFactory(options);
        authentication = new TestAuthentication("alice");
        service = new SocialInteractionService(factory, authentication);

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" },
            new ApplicationUser { Id = "bob", UserName = "bob", DisplayName = "Bob" });
        db.Tracks.Add(new Track { Title = "Test track" });
        await db.SaveChangesAsync();
        var track = await db.Tracks.SingleAsync();
        db.Reviews.Add(new Review { UserId = "bob", TrackId = track.Id, Rating = 4, Comment = "Nice track" });
        await db.SaveChangesAsync();
        reviewId = await db.Reviews.Select(review => review.Id).SingleAsync();
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public async Task LikesArePersistedIdempotentAndActorScoped()
    {
        await service.LikeReviewAsync(reviewId);
        await service.LikeReviewAsync(reviewId);
        var liked = await service.GetReviewInteractionsAsync(reviewId);
        Assert.That(liked.LikeCount, Is.EqualTo(1));
        Assert.That(liked.IsLiked, Is.True);

        authentication.Set("bob");
        await service.LikeReviewAsync(reviewId);
        Assert.That((await service.GetReviewInteractionsAsync(reviewId)).LikeCount, Is.EqualTo(2));
        await service.UnlikeReviewAsync(reviewId);
        await service.UnlikeReviewAsync(reviewId);
        var afterUnlike = await service.GetReviewInteractionsAsync(reviewId);
        Assert.That(afterUnlike.LikeCount, Is.EqualTo(1));
        Assert.That(afterUnlike.IsLiked, Is.False);
    }

    [Test]
    public async Task DatabaseRejectsDuplicateLikesAndInvalidReferences()
    {
        await service.LikeReviewAsync(reviewId);
        await using var db = factory.CreateDbContext();
        db.Likes.Add(new Like { ReviewId = reviewId, UserId = "alice" });
        var duplicate = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)duplicate!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(2067));

        db.ChangeTracker.Clear();
        db.Likes.Add(new Like { ReviewId = int.MaxValue, UserId = "alice" });
        var invalidReview = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)invalidReview!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(787));
    }

    [Test]
    public async Task CommentsCanBeCreatedEditedAndDeletedByTheirOwner()
    {
        var created = await service.AddCommentAsync(reviewId, "  I agree!  ");
        Assert.That(created.Content, Is.EqualTo("I agree!"));
        Assert.That(created.IsOwner, Is.True);
        Assert.That(created.DisplayName, Is.EqualTo("Alice"));

        authentication.Set("bob");
        Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateCommentAsync(created.Id, "Not mine"));
        Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteCommentAsync(created.Id));
        Assert.That((await service.GetReviewInteractionsAsync(reviewId)).Comments.Single().Content, Is.EqualTo("I agree!"));

        authentication.Set("alice");
        var updated = await service.UpdateCommentAsync(created.Id, "Edited");
        Assert.That(updated.Content, Is.EqualTo("Edited"));
        Assert.That(updated.UpdatedAt, Is.GreaterThanOrEqualTo(created.UpdatedAt));
        await service.DeleteCommentAsync(created.Id);
        Assert.That((await service.GetReviewInteractionsAsync(reviewId)).Comments, Is.Empty);
    }

    [Test]
    public async Task CommentsRequireContentAndRespectLengthLimit()
    {
        Assert.ThrowsAsync<ArgumentException>(() => service.AddCommentAsync(reviewId, "  "));
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddCommentAsync(reviewId, new string('x', SocialInteractionService.MaximumCommentLength + 1)));

        await using var db = factory.CreateDbContext();
        db.Comments.Add(new Comment { ReviewId = reviewId, UserId = "alice", Content = " " });
        var blankConstraint = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)blankConstraint!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(275));
    }

    [Test]
    public void AnonymousMutationsAndUnknownReviewsAreRejected()
    {
        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LikeReviewAsync(reviewId));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddCommentAsync(reviewId, "Hello"));
        Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReviewInteractionsAsync(int.MaxValue));
    }

    [Test]
    public async Task DeletingReviewCascadesItsLikesAndComments()
    {
        await service.LikeReviewAsync(reviewId);
        await service.AddCommentAsync(reviewId, "Hello");
        await using (var db = factory.CreateDbContext())
        {
            var review = await db.Reviews.SingleAsync(item => item.Id == reviewId);
            db.Reviews.Remove(review);
            await db.SaveChangesAsync();
        }

        await using var verify = factory.CreateDbContext();
        Assert.That(await verify.Likes.CountAsync(), Is.Zero);
        Assert.That(await verify.Comments.CountAsync(), Is.Zero);
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
