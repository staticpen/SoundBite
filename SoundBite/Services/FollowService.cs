using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;

namespace SoundBite.Services;

public record FollowSummary(int Followers, int Following, bool IsFollowing, bool CanFollow);
public record FollowProfile(string UserName, string DisplayName);
public record FollowPage(IReadOnlyList<FollowProfile> Users, bool HasNext);

public class FollowService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider,
    NotificationEventDispatcher notificationEvents)
{
    public const int PageSize = 10;

    private async Task<string?> CurrentUserIdAsync()
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        return user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    }

    public async Task<FollowSummary> GetSummaryAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        var actor = await CurrentUserIdAsync();
        return new FollowSummary(
            await db.Follows.CountAsync(f => f.FollowingId == userId),
            await db.Follows.CountAsync(f => f.FollowerId == userId),
            actor is not null && await db.Follows.AnyAsync(f => f.FollowerId == actor && f.FollowingId == userId),
            actor is not null && actor != userId && await db.Users.AnyAsync(u => u.Id == actor));
    }

    public async Task<FollowPage> GetPageAsync(string userId, bool followers, int page = 0)
    {
        if (page < 0 || page > int.MaxValue / PageSize - 1)
            throw new ArgumentOutOfRangeException(nameof(page));
        await using var db = await contextFactory.CreateDbContextAsync();
        var users = followers
            ? db.Follows.Where(f => f.FollowingId == userId).Select(f => f.Follower)
            : db.Follows.Where(f => f.FollowerId == userId).Select(f => f.Following);
        // The unique ID breaks username ties and gives stable pagination.
        var rows = await users.AsNoTracking().OrderBy(u => u.UserName).ThenBy(u => u.Id)
            .Skip(page * PageSize).Take(PageSize + 1)
            .Select(u => new FollowProfile(u.UserName!, u.DisplayName)).ToListAsync();
        return new FollowPage(rows.Take(PageSize).ToArray(), rows.Count > PageSize);
    }

    public Task FollowAsync(string targetUserId) => SetFollowingAsync(targetUserId, true);
    public Task UnfollowAsync(string targetUserId) => SetFollowingAsync(targetUserId, false);

    private async Task SetFollowingAsync(string targetUserId, bool following)
    {
        var actor = await CurrentUserIdAsync();
        if (string.IsNullOrWhiteSpace(actor))
            throw new UnauthorizedAccessException("Sign in to follow users.");
        if (string.IsNullOrWhiteSpace(targetUserId) || actor == targetUserId)
            throw new ArgumentException("Choose another user to follow.", nameof(targetUserId));
        await using var db = await contextFactory.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == actor))
            throw new UnauthorizedAccessException("Your account is no longer available.");
        if (!await db.Users.AnyAsync(u => u.Id == targetUserId))
            throw new InvalidOperationException("This profile is no longer available.");

        if (following)
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            // Create the notification only for the request that inserts the unique follow.
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Follows" ("FollowerId", "FollowingId") VALUES ({actor}, {targetUserId})
                    ON CONFLICT ("FollowerId", "FollowingId") DO NOTHING
                    """);
            if (inserted > 0)
            {
                db.Notifications.Add(new SoundBite.Models.Notification
                {
                    RecipientUserId = targetUserId,
                    ActorUserId = actor,
                    Type = SoundBite.Models.NotificationType.Follow
                });
                await db.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            if (inserted > 0)
                await notificationEvents.PublishAsync(targetUserId);
        }
        else
        {
            await db.Follows.Where(f => f.FollowerId == actor && f.FollowingId == targetUserId)
                .ExecuteDeleteAsync();
        }
    }
}
