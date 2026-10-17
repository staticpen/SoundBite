using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class TrendService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public const int DefaultWindowDays = 7;
    public const int MaximumWindowDays = 30;

    public async Task<IReadOnlyList<TrendingTrackEntry>> GetTrendingTracksAsync(
        int windowDays = DefaultWindowDays,
        int limit = RankingService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (windowDays is < 1 or > MaximumWindowDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowDays),
                $"Window must be between 1 and {MaximumWindowDays} days.");
        }
        if (limit is < 1 or > RankingService.MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {RankingService.MaximumLimit}.");
        }

        var cutoff = DateTime.UtcNow.AddDays(-windowDays);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reviews.AsNoTracking()
            .Where(review => review.CreatedAt >= cutoff)
            .GroupBy(review => new { review.TrackId, Title = review.Track!.Title })
            .Select(group => new TrendingTrackEntry
            {
                TrackId = group.Key.TrackId,
                Title = group.Key.Title,
                RecentReviewCount = group.Count(),
                RecentAverageRating = group.Average(review => review.Rating)
            })
            .OrderByDescending(entry => entry.RecentReviewCount)
            .ThenByDescending(entry => entry.RecentAverageRating)
            .ThenBy(entry => entry.Title)
            .ThenBy(entry => entry.TrackId)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
