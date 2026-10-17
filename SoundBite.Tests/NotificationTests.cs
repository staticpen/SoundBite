using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class NotificationTests
{
    private SqliteConnection connection = null!;
    private TestFactory factory = null!;
    private TestAuthentication authentication = null!;
    private FollowService followService = null!;
    private SocialInteractionService socialService = null!;
    private NotificationService notificationService = null!;
    private NotificationEventDispatcher dispatcher = null!;
    private int reviewId;
    private int trackId;
    private int commentId;
    private int playlistId;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        factory = new TestFactory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        authentication = new TestAuthentication("alice");
        var notificationEvents = new NotificationEventDispatcher(NullLogger<NotificationEventDispatcher>.Instance);
        followService = new FollowService(factory, authentication, notificationEvents);
        socialService = new SocialInteractionService(factory, authentication, notificationEvents);
        notificationService = new NotificationService(factory, authentication, notificationEvents);
        dispatcher = notificationEvents;

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice", UserName = "alice", DisplayName = "Alice" },
            new ApplicationUser { Id = "bob", UserName = "bob", DisplayName = "Bob" });
        db.Tracks.Add(new Track { Title = "Test track" });
        db.Playlists.Add(new Playlist { OwnerId = "bob", Name = "Bob's playlist", IsPublic = true });
        await db.SaveChangesAsync();
        trackId = await db.Tracks.Select(track => track.Id).SingleAsync();
        playlistId = await db.Playlists.Select(playlist => playlist.Id).SingleAsync();
        db.Reviews.Add(new Review { UserId = "bob", TrackId = trackId, Rating = 4, Comment = "Bob's review" });
        await db.SaveChangesAsync();
        reviewId = await db.Reviews.Select(review => review.Id).SingleAsync();
        db.Comments.Add(new Comment { ReviewId = reviewId, UserId = "bob", Content = "Bob's comment" });
        await db.SaveChangesAsync();
        commentId = await db.Comments.Select(comment => comment.Id).SingleAsync();
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public async Task FollowCommentReplyAndLikesCreateDeduplicatedNotificationsForOwners()
    {
        var bobUpdates = 0;
        var aliceUpdates = 0;
        using var bobSubscription = dispatcher.Subscribe("bob", () =>
        {
            bobUpdates++;
            return Task.CompletedTask;
        });
        using var aliceSubscription = dispatcher.Subscribe("alice", () =>
        {
            aliceUpdates++;
            return Task.CompletedTask;
        });

        await followService.FollowAsync("bob");
        await followService.FollowAsync("bob");
        await socialService.LikeReviewAsync(reviewId);
        await socialService.LikeReviewAsync(reviewId);
        await socialService.LikeCommentAsync(commentId);
        await socialService.LikeCommentAsync(commentId);
        await socialService.LikePlaylistAsync(playlistId);
        await socialService.LikePlaylistAsync(playlistId);
        await socialService.LikeTrackAsync(trackId);

        var comment = await socialService.AddCommentAsync(reviewId, "A new comment");
        var reply = await socialService.AddReplyAsync(commentId, "A reply");

        await using var db = factory.CreateDbContext();
        var notifications = await db.Notifications
            .Where(notification => notification.RecipientUserId == "bob")
            .OrderBy(notification => notification.Type)
            .ToListAsync();
        Assert.That(notifications.Select(notification => notification.Type), Is.EquivalentTo(new[]
        {
            NotificationType.Follow,
            NotificationType.ReviewComment,
            NotificationType.CommentReply,
            NotificationType.ReviewLike,
            NotificationType.CommentLike,
            NotificationType.PlaylistLike
        }));
        Assert.That(notifications.Single(notification => notification.Type == NotificationType.ReviewComment).CommentId,
            Is.EqualTo(comment.Id));
        Assert.That(notifications.Single(notification => notification.Type == NotificationType.CommentReply).CommentId,
            Is.EqualTo(reply.Id));
        Assert.That(notifications.Single(notification => notification.Type == NotificationType.ReviewLike).LikeId,
            Is.Not.Null);
        Assert.That(notifications.Count(notification => notification.LikeId is not null), Is.EqualTo(3));
        Assert.That(bobUpdates, Is.EqualTo(6));
        Assert.That(aliceUpdates, Is.Zero);

        authentication.Set("bob");
        await socialService.LikeReviewAsync(reviewId);
        await socialService.LikeCommentAsync(commentId);
        await using var verify = factory.CreateDbContext();
        Assert.That(await verify.Notifications.CountAsync(notification => notification.RecipientUserId == "bob"),
            Is.EqualTo(6));
    }

    [Test]
    public async Task LiveNotificationEventsAreRecipientScopedAndSubscriptionsCanBeRemoved()
    {
        var bobUpdates = 0;
        var aliceUpdates = 0;
        using var bobSubscription = dispatcher.Subscribe("bob", () =>
        {
            bobUpdates++;
            return Task.CompletedTask;
        });
        var removedSubscription = dispatcher.Subscribe("bob", () =>
            throw new InvalidOperationException("Removed subscriber must not be called."));
        using var aliceSubscription = dispatcher.Subscribe("alice", () =>
        {
            aliceUpdates++;
            return Task.CompletedTask;
        });

        removedSubscription.Dispose();
        await dispatcher.PublishAsync("bob");

        Assert.That(bobUpdates, Is.EqualTo(1));
        Assert.That(aliceUpdates, Is.Zero);
    }

    [Test]
    public async Task CircuitReconnectNotifiesActiveSubscribersToReloadPersistedState()
    {
        var refreshCount = 0;
        var circuitHandler = new NotificationCircuitHandler(
            NullLogger<NotificationCircuitHandler>.Instance);
        using var subscription = circuitHandler.Subscribe(() =>
        {
            refreshCount++;
            return Task.CompletedTask;
        });

        await circuitHandler.OnConnectionUpAsync(null!, CancellationToken.None);

        Assert.That(refreshCount, Is.EqualTo(1));
    }

    [Test]
    public async Task NotificationsArePrivateAndCanBeMarkedReadIndividuallyOrTogether()
    {
        await followService.FollowAsync("bob");
        authentication.Set("bob");
        await followService.FollowAsync("alice");

        var bobPage = await notificationService.GetPageAsync();
        Assert.That(bobPage.Items, Has.Count.EqualTo(1));
        Assert.That(bobPage.UnreadCount, Is.EqualTo(1));
        Assert.That(bobPage.Items[0].Type, Is.EqualTo(NotificationType.Follow));
        Assert.That(bobPage.Items[0].ActorUserName, Is.EqualTo("alice"));

        authentication.Set("alice");
        var alicePage = await notificationService.GetPageAsync();
        Assert.That(alicePage.Items, Has.Count.EqualTo(1));
        Assert.That(alicePage.Items[0].ActorUserName, Is.EqualTo("bob"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => notificationService.MarkReadAsync(bobPage.Items[0].Id));
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.EqualTo(1));
        await notificationService.MarkReadAsync(alicePage.Items[0].Id);
        await notificationService.MarkReadAsync(alicePage.Items[0].Id);
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.Zero);

        authentication.Set("bob");
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.EqualTo(1));
        authentication.Set("alice");
        await notificationService.MarkAllReadAsync();
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.Zero);
        Assert.That((await notificationService.GetPageAsync()).Items.Single().ReadAt, Is.Not.Null);
    }

    [Test]
    public void AnonymousNotificationAccessIsRejected()
    {
        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => notificationService.GetPageAsync());
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => notificationService.GetUnreadCountAsync());
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => notificationService.MarkAllReadAsync());
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
