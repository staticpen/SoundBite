using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
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
    private int trackId;
    private int publicPlaylistId;
    private int privatePlaylistId;
    private int commentId;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        factory = new TestFactory(options);
        authentication = new TestAuthentication("alice");
        service = new SocialInteractionService(
            factory,
            authentication,
            new NotificationEventDispatcher(NullLogger<NotificationEventDispatcher>.Instance));

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" },
            new ApplicationUser { Id = "bob", UserName = "bob", DisplayName = "Bob" });
        db.Tracks.Add(new Track { Title = "Test track" });
        db.Playlists.AddRange(
            new Playlist { OwnerId = "bob", Name = "Public playlist", IsPublic = true },
            new Playlist { OwnerId = "bob", Name = "Private playlist", IsPublic = false });
        await db.SaveChangesAsync();
        var track = await db.Tracks.SingleAsync();
        trackId = track.Id;
        publicPlaylistId = await db.Playlists.Where(playlist => playlist.IsPublic).Select(playlist => playlist.Id).SingleAsync();
        privatePlaylistId = await db.Playlists.Where(playlist => !playlist.IsPublic).Select(playlist => playlist.Id).SingleAsync();
        db.Reviews.Add(new Review { UserId = "bob", TrackId = track.Id, Rating = 4, Comment = "Nice track" });
        await db.SaveChangesAsync();
        reviewId = await db.Reviews.Select(review => review.Id).SingleAsync();
        db.Comments.Add(new Comment { ReviewId = reviewId, UserId = "bob", Content = "A comment" });
        await db.SaveChangesAsync();
        commentId = await db.Comments.Select(comment => comment.Id).SingleAsync();
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
    public async Task LikesCanTargetTracksPlaylistsAndCommentsWithoutCrossTargetDuplicates()
    {
        await service.LikeTrackAsync(trackId);
        await service.LikeTrackAsync(trackId);
        await service.LikePlaylistAsync(publicPlaylistId);
        await service.LikeCommentAsync(commentId);

        Assert.That(await service.GetLikeSummaryAsync(LikeTargetType.Track, trackId), Is.EqualTo(new LikeSummary(1, true)));
        Assert.That(await service.GetLikeSummaryAsync(LikeTargetType.Playlist, publicPlaylistId), Is.EqualTo(new LikeSummary(1, true)));
        Assert.That(await service.GetLikeSummaryAsync(LikeTargetType.Comment, commentId), Is.EqualTo(new LikeSummary(1, true)));
        var commentInteractions = (await service.GetReviewInteractionsAsync(reviewId)).Comments.Single();
        Assert.That(commentInteractions.LikeCount, Is.EqualTo(1));
        Assert.That(commentInteractions.IsLiked, Is.True);

        await service.UnlikeTrackAsync(trackId);
        await service.UnlikePlaylistAsync(publicPlaylistId);
        await service.UnlikeCommentAsync(commentId);
        Assert.That((await service.GetLikeSummaryAsync(LikeTargetType.Track, trackId)).Count, Is.Zero);
        Assert.That((await service.GetLikeSummaryAsync(LikeTargetType.Playlist, publicPlaylistId)).Count, Is.Zero);
        Assert.That((await service.GetLikeSummaryAsync(LikeTargetType.Comment, commentId)).Count, Is.Zero);
    }

    [Test]
    public async Task PrivatePlaylistLikesAreOwnerOnlyAndTargetsMustExist()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => service.LikePlaylistAsync(privatePlaylistId));
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetLikeSummaryAsync(LikeTargetType.Playlist, privatePlaylistId));
        Assert.ThrowsAsync<InvalidOperationException>(() => service.LikeTrackAsync(int.MaxValue));

        authentication.Set("bob");
        await service.LikePlaylistAsync(privatePlaylistId);
        Assert.That(await service.GetLikeSummaryAsync(LikeTargetType.Playlist, privatePlaylistId),
            Is.EqualTo(new LikeSummary(1, true)));
    }

    [Test]
    public async Task DatabaseRequiresExactlyOneLikeTargetAndAllowsSameUserAcrossTargets()
    {
        await service.LikeReviewAsync(reviewId);
        await service.LikeTrackAsync(trackId);
        await service.LikePlaylistAsync(publicPlaylistId);
        await service.LikeCommentAsync(commentId);

        await using var db = factory.CreateDbContext();
        Assert.That(await db.Likes.CountAsync(like => like.UserId == "alice"), Is.EqualTo(4));
        db.Likes.Add(new Like { UserId = "alice" });
        var missingTarget = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)missingTarget!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(275));

        db.ChangeTracker.Clear();
        db.Likes.Add(new Like { UserId = "alice", TrackId = trackId, ReviewId = reviewId });
        var multipleTargets = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)multipleTargets!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(275));
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
        Assert.That((await service.GetReviewInteractionsAsync(reviewId)).Comments.Single(comment => comment.Id == created.Id).Content,
            Is.EqualTo("I agree!"));

        authentication.Set("alice");
        var updated = await service.UpdateCommentAsync(created.Id, "Edited");
        Assert.That(updated.Content, Is.EqualTo("Edited"));
        Assert.That(updated.UpdatedAt, Is.GreaterThanOrEqualTo(created.UpdatedAt));
        await service.DeleteCommentAsync(created.Id);
        Assert.That((await service.GetReviewInteractionsAsync(reviewId)).Comments.Select(comment => comment.Id),
            Is.EqualTo(new[] { commentId }));
    }

    [Test]
    public async Task RepliesArePersistedUnderTopLevelCommentsAndCannotBeNested()
    {
        var reply = await service.AddReplyAsync(commentId, "A thoughtful response");

        Assert.That(reply.ParentCommentId, Is.EqualTo(commentId));
        Assert.That(reply.ParentUserName, Is.EqualTo("bob"));
        var comments = (await service.GetReviewInteractionsAsync(reviewId)).Comments;
        Assert.That(comments.Single(comment => comment.Id == reply.Id).ParentCommentId, Is.EqualTo(commentId));
        Assert.ThrowsAsync<InvalidOperationException>(() => service.AddReplyAsync(reply.Id, "Nested reply"));
    }

    [Test]
    public async Task DeletingACommentPromotesItsRepliesInsteadOfDeletingThem()
    {
        var reply = await service.AddReplyAsync(commentId, "Keep this response");
        authentication.Set("bob");

        await service.DeleteCommentAsync(commentId);

        var promoted = (await service.GetReviewInteractionsAsync(reviewId)).Comments.Single();
        Assert.That(promoted.Id, Is.EqualTo(reply.Id));
        Assert.That(promoted.ParentCommentId, Is.Null);
        Assert.That(promoted.Content, Is.EqualTo("Keep this response"));
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
        await service.AddReplyAsync(commentId, "A reply");
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

    [Test]
    public async Task DeletingTrackPlaylistOrCommentCascadesOnlyLikesForThatTarget()
    {
        int extraTrackId;
        int extraPlaylistId;
        await using (var db = factory.CreateDbContext())
        {
            db.Tracks.Add(new Track { Title = "Separately liked track" });
            db.Playlists.Add(new Playlist { OwnerId = "bob", Name = "Separately liked playlist" });
            await db.SaveChangesAsync();
            extraTrackId = await db.Tracks.Where(track => track.Title == "Separately liked track")
                .Select(track => track.Id).SingleAsync();
            extraPlaylistId = await db.Playlists.Where(playlist => playlist.Name == "Separately liked playlist")
                .Select(playlist => playlist.Id).SingleAsync();
        }

        await service.LikeReviewAsync(reviewId);
        await service.LikeTrackAsync(extraTrackId);
        await service.LikePlaylistAsync(extraPlaylistId);
        await service.LikeCommentAsync(commentId);

        await using (var db = factory.CreateDbContext())
        {
            await db.Tracks.Where(track => track.Id == extraTrackId).ExecuteDeleteAsync();
            await db.Playlists.Where(playlist => playlist.Id == extraPlaylistId).ExecuteDeleteAsync();
            await db.Comments.Where(comment => comment.Id == commentId).ExecuteDeleteAsync();
        }

        await using var verify = factory.CreateDbContext();
        Assert.That(await verify.Likes.CountAsync(like => like.ReviewId == reviewId), Is.EqualTo(1));
        Assert.That(await verify.Likes.CountAsync(like => like.TrackId == extraTrackId), Is.Zero);
        Assert.That(await verify.Likes.CountAsync(like => like.PlaylistId == extraPlaylistId), Is.Zero);
        Assert.That(await verify.Likes.CountAsync(like => like.CommentId == commentId), Is.Zero);
    }

    [Test]
    public async Task LikeTargetMigrationPreservesExistingReviewLikes()
    {
        await using var migrationConnection = new SqliteConnection("DataSource=:memory:");
        await migrationConnection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(migrationConnection).Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20261017002358_AddReviewSocialInteractions");
        db.Users.AddRange(
            new ApplicationUser { Id = "review-owner", UserName = "review-owner" },
            new ApplicationUser { Id = "liker", UserName = "liker" });
        db.Tracks.Add(new Track { Title = "Preserved track" });
        await db.SaveChangesAsync();
        var track = await db.Tracks.SingleAsync();
        db.Reviews.Add(new Review { UserId = "review-owner", TrackId = track.Id, Rating = 5 });
        await db.SaveChangesAsync();
        var review = await db.Reviews.SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Likes" ("ReviewId", "UserId", "CreatedAt")
            VALUES ({review.Id}, {"liker"}, {DateTime.UtcNow})
            """);

        await db.Database.MigrateAsync();
        Assert.That(await db.Likes.CountAsync(like => like.ReviewId == review.Id && like.UserId == "liker"), Is.EqualTo(1));
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
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
