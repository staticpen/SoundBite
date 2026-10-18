using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        await db.SaveChangesAsync();
        trackId = await db.Tracks.Select(track => track.Id).SingleAsync();
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
            NotificationType.CommentLike
        }));
        Assert.That(notifications.Single(notification => notification.Type == NotificationType.ReviewComment).CommentId,
            Is.EqualTo(comment.Id));
        Assert.That(notifications.Single(notification => notification.Type == NotificationType.CommentReply).CommentId,
            Is.EqualTo(reply.Id));
        Assert.That(notifications.Single(notification => notification.Type == NotificationType.ReviewLike).LikeId,
            Is.Not.Null);
        Assert.That(notifications.Count(notification => notification.LikeId is not null), Is.EqualTo(2));
        Assert.That(bobUpdates, Is.EqualTo(5));
        Assert.That(aliceUpdates, Is.Zero);

        authentication.Set("bob");
        await socialService.LikeReviewAsync(reviewId);
        await socialService.LikeCommentAsync(commentId);
        await using var verify = factory.CreateDbContext();
        Assert.That(await verify.Notifications.CountAsync(notification => notification.RecipientUserId == "bob"),
            Is.EqualTo(5));
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

    // Adds follow notifications for a recipient, created one minute apart starting at "newest" and going back in time.
    // Returns their IDs in display order (newest first); the IDs themselves rise as the notifications get older.
    private async Task<List<int>> SeedNotificationsAsync(string recipientId, string actorId, int count, DateTime? newest = null)
    {
        var newestTime = newest ?? new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        await using var db = factory.CreateDbContext();
        var notifications = Enumerable.Range(0, count)
            .Select(index => new Notification
            {
                RecipientUserId = recipientId,
                ActorUserId = actorId,
                Type = NotificationType.Follow,
                CreatedAt = newestTime.AddMinutes(-index)
            })
            .ToList();
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();
        return notifications.Select(notification => notification.Id).ToList();
    }

    [Test]
    public async Task OlderNotificationsAreAvailableInPagesWithoutDuplicatesOrGaps()
    {
        var ids = await SeedNotificationsAsync("alice", "bob", NotificationService.PageSize + 1);

        var first = await notificationService.GetPageAsync();
        Assert.That(first.Items, Has.Count.EqualTo(NotificationService.PageSize));
        Assert.That(first.HasMore, Is.True);
        Assert.That(first.UnreadCount, Is.EqualTo(ids.Count));

        var last = first.Items[^1];
        var second = await notificationService.GetPageAsync(last.CreatedAt, last.Id);
        Assert.That(second.Items, Has.Count.EqualTo(1));
        Assert.That(second.HasMore, Is.False);

        var allIds = first.Items.Concat(second.Items).Select(item => item.Id).ToList();
        Assert.That(allIds, Is.EqualTo(ids));
    }

    [Test]
    public async Task ExactlyOnePageOfNotificationsHasNoMore()
    {
        await SeedNotificationsAsync("alice", "bob", NotificationService.PageSize);

        var page = await notificationService.GetPageAsync();

        Assert.That(page.Items, Has.Count.EqualTo(NotificationService.PageSize));
        Assert.That(page.HasMore, Is.False);
    }

    [Test]
    public async Task PagingPastTheOldestNotificationReturnsAnEmptyPage()
    {
        await SeedNotificationsAsync("alice", "bob", 3);
        var page = await notificationService.GetPageAsync();
        var oldest = page.Items[^1];

        var older = await notificationService.GetPageAsync(oldest.CreatedAt, oldest.Id);

        Assert.That(older.Items, Is.Empty);
        Assert.That(older.HasMore, Is.False);
    }

    [Test]
    public async Task NotificationsWithTheSameTimestampArePagedByIdWithoutDuplicates()
    {
        var sameTime = new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
        await using (var db = factory.CreateDbContext())
        {
            db.Notifications.AddRange(Enumerable.Range(0, NotificationService.PageSize + 5)
                .Select(_ => new Notification
                {
                    RecipientUserId = "alice",
                    ActorUserId = "bob",
                    Type = NotificationType.Follow,
                    CreatedAt = sameTime
                }));
            await db.SaveChangesAsync();
        }

        var first = await notificationService.GetPageAsync();
        var last = first.Items[^1];
        var second = await notificationService.GetPageAsync(last.CreatedAt, last.Id);

        Assert.That(second.Items, Has.Count.EqualTo(5));
        Assert.That(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)), Is.Empty);
        Assert.That(first.Items.Concat(second.Items).Select(item => item.Id), Is.Ordered.Descending);
    }

    [Test]
    public async Task NotificationPagesNeverIncludeAnotherUsersNotifications()
    {
        var aliceIds = await SeedNotificationsAsync("alice", "bob", NotificationService.PageSize + 2);
        var bobIds = await SeedNotificationsAsync("bob", "alice", 40);

        var first = await notificationService.GetPageAsync();
        var last = first.Items[^1];
        var second = await notificationService.GetPageAsync(last.CreatedAt, last.Id);

        Assert.That(first.Items.Select(item => item.Id), Is.EqualTo(aliceIds.Take(NotificationService.PageSize)));
        Assert.That(second.Items.Select(item => item.Id), Is.EqualTo(aliceIds.Skip(NotificationService.PageSize)));

        // Alice's cursor is her 30th notification (index 29). Used by Bob, it returns only Bob's own
        // notifications older than that time: indexes 30 to 39. Bob's index 29 has the same time but a
        // higher Id, so it is not "before" the cursor.
        authentication.Set("bob");
        var bobPage = await notificationService.GetPageAsync(last.CreatedAt, last.Id);
        Assert.That(bobPage.Items.Select(item => item.Id), Is.EqualTo(bobIds.Skip(NotificationService.PageSize)));
        Assert.That(bobPage.Items.Select(item => item.ActorUserName), Has.All.EqualTo("alice"));
    }

    [Test]
    public async Task NewNotificationArrivingBetweenPagesDoesNotDuplicateOrSkipOlderItems()
    {
        var ids = await SeedNotificationsAsync("alice", "bob", NotificationService.PageSize + 5);
        var first = await notificationService.GetPageAsync();
        var last = first.Items[^1];

        // A newer notification arrives after the first page was loaded.
        await SeedNotificationsAsync("alice", "bob", 1, new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc));
        var second = await notificationService.GetPageAsync(last.CreatedAt, last.Id);

        Assert.That(second.Items.Select(item => item.Id), Is.EqualTo(ids.Skip(NotificationService.PageSize)));
    }

    [Test]
    public void InvalidNotificationCursorIsRejected()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => notificationService.GetPageAsync(DateTime.UtcNow, 0));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => notificationService.GetPageAsync(DateTime.UtcNow, -5));
    }

    [Test]
    public async Task AnonymousUserCannotLoadOlderNotifications()
    {
        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => notificationService.GetPageAsync(DateTime.UtcNow, 1));
        await Task.CompletedTask;
    }

    [Test]
    public async Task UnreadCountAndMarkReadIgnoreWhichPageIsShown()
    {
        var ids = await SeedNotificationsAsync("alice", "bob", NotificationService.PageSize + 2);
        var first = await notificationService.GetPageAsync();
        var last = first.Items[^1];
        var second = await notificationService.GetPageAsync(last.CreatedAt, last.Id);
        Assert.That(second.UnreadCount, Is.EqualTo(ids.Count));

        // Mark an item that only appears on the older page.
        await notificationService.MarkReadAsync(second.Items[0].Id);
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.EqualTo(ids.Count - 1));
        var secondAgain = await notificationService.GetPageAsync(last.CreatedAt, last.Id);
        Assert.That(secondAgain.Items.Single(item => item.Id == second.Items[0].Id).ReadAt, Is.Not.Null);
        Assert.That(secondAgain.Items.Single(item => item.Id == second.Items[1].Id).ReadAt, Is.Null);

        await notificationService.MarkAllReadAsync();
        Assert.That((await notificationService.GetPageAsync(last.CreatedAt, last.Id)).UnreadCount, Is.Zero);
    }

    private NotificationService NewNotificationService() =>
        new(factory, authentication, dispatcher);

    [Test]
    public async Task EveryCategoryIsEnabledByDefault()
    {
        Assert.That(await notificationService.GetDisabledTypesAsync(), Is.Empty);
    }

    [Test]
    public async Task DisabledCategoriesPersistAcrossNewServiceInstancesAndStayPerUser()
    {
        authentication.Set("bob");
        await notificationService.SetTypeEnabledAsync(NotificationType.ReviewLike, false);
        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);
        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);

        var freshService = NewNotificationService();
        Assert.That(await freshService.GetDisabledTypesAsync(),
            Is.EqualTo(new[] { NotificationType.Follow, NotificationType.ReviewLike }));

        authentication.Set("alice");
        Assert.That(await freshService.GetDisabledTypesAsync(), Is.Empty);

        authentication.Set("bob");
        await freshService.SetTypeEnabledAsync(NotificationType.Follow, true);
        await freshService.SetTypeEnabledAsync(NotificationType.Follow, true);
        Assert.That(await NewNotificationService().GetDisabledTypesAsync(),
            Is.EqualTo(new[] { NotificationType.ReviewLike }));
    }

    [Test]
    public async Task DisabledCategoriesAreNotCreatedOrDeliveredButOthersStillAre()
    {
        authentication.Set("bob");
        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);
        await notificationService.SetTypeEnabledAsync(NotificationType.ReviewLike, false);
        authentication.Set("alice");

        var bobUpdates = 0;
        using var bobSubscription = dispatcher.Subscribe("bob", () =>
        {
            bobUpdates++;
            return Task.CompletedTask;
        });

        await followService.FollowAsync("bob");
        await socialService.LikeReviewAsync(reviewId);
        await socialService.LikeCommentAsync(commentId);
        await socialService.AddCommentAsync(reviewId, "Still delivered");

        await using var db = factory.CreateDbContext();
        var types = await db.Notifications
            .Where(notification => notification.RecipientUserId == "bob")
            .Select(notification => notification.Type)
            .ToListAsync();
        Assert.That(types, Is.EquivalentTo(new[] { NotificationType.CommentLike, NotificationType.ReviewComment }));
        Assert.That(bobUpdates, Is.EqualTo(2));
        // The follow and the like themselves are still saved.
        Assert.That(await db.Follows.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.Likes.CountAsync(like => like.ReviewId == reviewId), Is.EqualTo(1));
    }

    [Test]
    public async Task DisabledCommentCategorySavesTheCommentWithoutNotifyingOrPublishing()
    {
        authentication.Set("bob");
        await notificationService.SetTypeEnabledAsync(NotificationType.ReviewComment, false);
        authentication.Set("alice");

        var bobUpdates = 0;
        using var bobSubscription = dispatcher.Subscribe("bob", () =>
        {
            bobUpdates++;
            return Task.CompletedTask;
        });

        var comment = await socialService.AddCommentAsync(reviewId, "Comment on a disabled category");

        await using var db = factory.CreateDbContext();
        Assert.That(await db.Comments.AnyAsync(saved => saved.Id == comment.Id), Is.True);
        Assert.That(await db.Notifications.CountAsync(notification => notification.RecipientUserId == "bob"), Is.Zero);
        Assert.That(bobUpdates, Is.Zero);
    }

    [Test]
    public async Task OneUsersSettingDoesNotChangeAnotherUsersNotifications()
    {
        // Alice turns follows off for herself. Bob's follow notification from her is unaffected.
        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);
        await followService.FollowAsync("bob");
        authentication.Set("bob");
        await followService.FollowAsync("alice");

        authentication.Set("bob");
        Assert.That((await notificationService.GetPageAsync()).Items, Has.Count.EqualTo(1));
        authentication.Set("alice");
        Assert.That((await notificationService.GetPageAsync()).Items, Is.Empty);
    }

    [Test]
    public async Task EnablingACategoryAgainOnlyAffectsFutureNotifications()
    {
        authentication.Set("bob");
        await notificationService.SetTypeEnabledAsync(NotificationType.ReviewLike, false);
        authentication.Set("alice");
        await socialService.LikeReviewAsync(reviewId);
        await socialService.UnlikeReviewAsync(reviewId);

        authentication.Set("bob");
        await notificationService.SetTypeEnabledAsync(NotificationType.ReviewLike, true);
        Assert.That((await notificationService.GetPageAsync()).Items, Is.Empty);

        authentication.Set("alice");
        await socialService.LikeReviewAsync(reviewId);
        authentication.Set("bob");
        var page = await notificationService.GetPageAsync();
        Assert.That(page.Items.Select(item => item.Type), Is.EqualTo(new[] { NotificationType.ReviewLike }));
    }

    [Test]
    public async Task ChangingPreferencesLeavesExistingNotificationsAndReadStatesUntouched()
    {
        await SeedNotificationsAsync("alice", "bob", 3);
        var before = await notificationService.GetPageAsync();
        await notificationService.MarkReadAsync(before.Items[0].Id);
        var readAt = (await notificationService.GetPageAsync()).Items[0].ReadAt;

        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);

        var after = await notificationService.GetPageAsync();
        Assert.That(after.Items, Has.Count.EqualTo(3));
        Assert.That(after.UnreadCount, Is.EqualTo(2));
        Assert.That(after.Items[0].ReadAt, Is.EqualTo(readAt));
        Assert.That(after.Items.Skip(1).Select(item => item.ReadAt), Has.All.Null);
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.EqualTo(2));

        Assert.That(await notificationService.MarkAllReadAsync(), Is.EqualTo(2));
        Assert.That(await notificationService.GetUnreadCountAsync(), Is.Zero);
    }

    [Test]
    public void InvalidCategoryAndAnonymousPreferenceAccessAreRejected()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => notificationService.SetTypeEnabledAsync((NotificationType)99, false));

        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => notificationService.GetDisabledTypesAsync());
        Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => notificationService.SetTypeEnabledAsync(NotificationType.Follow, false));
    }

    [Test]
    public async Task DeletingAUserRemovesTheirPreferencesOnly()
    {
        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);
        authentication.Set("bob");
        await notificationService.SetTypeEnabledAsync(NotificationType.CommentLike, false);

        await using var db = factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        db.Users.Remove(await db.Users.SingleAsync(user => user.Id == "alice"));
        await db.SaveChangesAsync();

        var remaining = await db.NotificationOptOuts.SingleAsync();
        Assert.That(remaining.UserId, Is.EqualTo("bob"));
        Assert.That(remaining.Type, Is.EqualTo(NotificationType.CommentLike));
    }

    [Test]
    public async Task PreferenceMigrationRollsBackAndUpgradesWithoutLosingNotifications()
    {
        await SeedNotificationsAsync("alice", "bob", 2);
        await notificationService.SetTypeEnabledAsync(NotificationType.Follow, false);

        await using var db = factory.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261017013935_AddNotifications");
        Assert.That(await db.Notifications.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = 'NotificationOptOuts'").SingleAsync(),
            Is.Zero);

        await db.Database.MigrateAsync();
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        Assert.That(await db.Notifications.CountAsync(), Is.EqualTo(2));
        // Rolling back drops only the preference table, so preferences start again with everything on.
        Assert.That(await db.NotificationOptOuts.CountAsync(), Is.Zero);
        Assert.That(await notificationService.GetDisabledTypesAsync(), Is.Empty);
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
