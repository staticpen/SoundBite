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

    public async Task<NotificationPage> GetPageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await GetCurrentUserIdAsync(db, cancellationToken);
        var unreadCount = await db.Notifications.CountAsync(
            notification => notification.RecipientUserId == userId && notification.ReadAt == null,
            cancellationToken);
        var items = await db.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(PageSize)
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

        return new NotificationPage(items, unreadCount);
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
