using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class NotificationService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider,
    NotificationEventDispatcher notificationEvents)
{
    public const int PageSize = 30;

    // Newest notifications (first page).
    public Task<NotificationPage> GetPageAsync(CancellationToken cancellationToken = default) =>
        GetPageCoreAsync(null, null, cancellationToken);

    // Older notifications: the next page after the last item already shown (its CreatedAt and Id).
    public Task<NotificationPage> GetPageAsync(
        DateTime beforeCreatedAt,
        int beforeId,
        CancellationToken cancellationToken = default)
    {
        if (beforeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(beforeId));
        return GetPageCoreAsync(beforeCreatedAt, beforeId, cancellationToken);
    }

    // Applies the optional cursor and loads a page of notifications for the signed-in user.
    private async Task<NotificationPage> GetPageCoreAsync(
        DateTime? beforeCreatedAt,
        int? beforeId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        var unreadCount = await db.Notifications.CountAsync(
            notification => notification.RecipientUserId == userId && notification.ReadAt == null,
            cancellationToken);

        // Only this user's notifications are ever queried, before paging is applied.
        var query = db.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId);
        if (beforeCreatedAt is DateTime createdAt && beforeId is int id)
        {
            query = query.Where(notification =>
                notification.CreatedAt < createdAt ||
                (notification.CreatedAt == createdAt && notification.Id < id));
        }

        // Take one extra row to learn whether an older page exists.
        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(PageSize + 1)
            .Select(notification => new NotificationItem(
                notification.Id,
                notification.Type,
                notification.Actor!.UserName!,
                notification.Actor.DisplayName,
                notification.CreatedAt,
                notification.ReadAt,
                notification.Review == null ? null : notification.Review.TrackId,
                notification.ReviewId,
                notification.Comment == null ? null : notification.Comment.Content))
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > PageSize;
        return new NotificationPage(items.Take(PageSize).ToList(), unreadCount, hasMore);
    }

    // Counts unread notifications for the current user.
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        return await db.Notifications.CountAsync(
            notification => notification.RecipientUserId == userId && notification.ReadAt == null,
            cancellationToken);
    }

    // Marks one of the current user's notifications as read and refreshes the UI.
    public async Task MarkReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(notificationId));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        var notification = await db.Notifications
            .Where(item => item.Id == notificationId && item.RecipientUserId == userId)
            .Select(item => new { item.Id, item.ReadAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Notification was not found.");
        if (notification.ReadAt is null)
        {
            await db.Notifications
                .Where(item => item.Id == notification.Id && item.RecipientUserId == userId)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(item => item.ReadAt, (DateTime?)DateTime.UtcNow),
                    cancellationToken);
            await notificationEvents.PublishAsync(userId);
        }
    }

    // Marks all unread notifications as read and publishes an update when anything changed.
    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        var updated = await db.Notifications
            .Where(notification => notification.RecipientUserId == userId && notification.ReadAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(notification => notification.ReadAt, (DateTime?)DateTime.UtcNow),
                cancellationToken);
        if (updated > 0)
            await notificationEvents.PublishAsync(userId);
        return updated;
    }

    // Categories the current user has turned off. An empty list means every category is on.
    public async Task<IReadOnlyList<NotificationType>> GetDisabledTypesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        return await db.NotificationOptOuts.AsNoTracking()
            .Where(optOut => optOut.UserId == userId)
            .OrderBy(optOut => optOut.Type)
            .Select(optOut => optOut.Type)
            .ToListAsync(cancellationToken);
    }

    // Applies to notifications created from now on. Existing notifications, their read state
    // and the unread count are not touched. Calling it twice with the same value is safe.
    public async Task SetTypeEnabledAsync(
        NotificationType type,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        if (enabled)
        {
            // Enabling means removing the opt-out; old notifications are left unchanged.
            await db.NotificationOptOuts
                .Where(optOut => optOut.UserId == userId && optOut.Type == type)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            // Ignore duplicate opt-outs so saving the same preference twice is safe.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "NotificationOptOuts" ("UserId", "Type") VALUES ({userId}, {(int)type})
                ON CONFLICT ("UserId", "Type") DO NOTHING
                """, cancellationToken);
        }
    }

    // Used where notifications are created. Runs on the caller's context so it joins any open transaction.
    internal static async Task<bool> IsEnabledAsync(
        ApplicationDbContext db,
        string recipientUserId,
        NotificationType type,
        CancellationToken cancellationToken = default) =>
        !await db.NotificationOptOuts.AnyAsync(
            optOut => optOut.UserId == recipientUserId && optOut.Type == type,
            cancellationToken);

    // Gets the signed-in user's ID and verifies that the account still exists.
    private async Task<string> GetCurrentUserIdAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var userId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Sign in to view notifications.");

        if (!await db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            throw new UnauthorizedAccessException("Your account is no longer available.");

        return userId;
    }
}
