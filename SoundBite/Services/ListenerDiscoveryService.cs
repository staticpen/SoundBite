using Microsoft.EntityFrameworkCore;
using SoundBite.Data;

namespace SoundBite.Services;

public sealed record ListenerProfile(string UserName, string DisplayName);

public sealed record ActiveListenerProfile(
    string UserName,
    string DisplayName,
    int TrackId,
    string TrackTitle,
    DateTime ReviewedAt);

public class ListenerDiscoveryService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public const int MinimumSearchLength = 2;
    public const int UserSearchLimit = 20;
    public const int ActiveListenerLimit = 8;
    public const int ActiveReviewWindowDays = 30;

    public async Task<IReadOnlyList<ListenerProfile>> FindUsersAsync(
        string? query,
        string? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedQuery) || normalizedQuery.Length < MinimumSearchLength)
            return [];

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(user =>
                user.UserName != null &&
                user.NormalizedUserName != null &&
                user.NormalizedUserName.Contains(normalizedQuery) &&
                (excludeUserId == null || user.Id != excludeUserId))
            .OrderBy(user => user.NormalizedUserName)
            .ThenBy(user => user.Id)
            .Take(UserSearchLimit)
            .Select(user => new ListenerProfile(user.UserName!, user.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveListenerProfile>> GetActiveListenersAsync(
        string? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddDays(-ActiveReviewWindowDays);
        var latestReviewIds = db.Reviews.AsNoTracking()
            .Where(review =>
                review.CreatedAt >= cutoff &&
                (excludeUserId == null || review.UserId != excludeUserId))
            .GroupBy(review => review.UserId)
            .Select(group => group
                .OrderByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.Id)
                .Select(review => review.Id)
                .First());

        return await (
                from review in db.Reviews.AsNoTracking()
                join reviewId in latestReviewIds on review.Id equals reviewId
                join user in db.Users.AsNoTracking() on review.UserId equals user.Id
                where user.UserName != null
                orderby review.CreatedAt descending, review.Id descending
                select new ActiveListenerProfile(
                    user.UserName!,
                    user.DisplayName,
                    review.TrackId,
                    review.Track!.Title,
                    review.CreatedAt))
            .Take(ActiveListenerLimit)
            .ToListAsync(cancellationToken);
    }
}
