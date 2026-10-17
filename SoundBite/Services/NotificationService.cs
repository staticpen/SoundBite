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
                notification.PlaylistId,
                notification.Playlist == null ? null : notification.Playlist.Name,
                notification.Comment == null ? null : notification.Comment.Content))
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > PageSize;
        return new NotificationPage(items.Take(PageSize).ToList(), unreadCount, hasMore);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        return await db.Notifications.CountAsync(
            notification => notification.RecipientUserId == userId && notification.ReadAt == null,
            cancellationToken);
    }

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
