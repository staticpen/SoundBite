using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class RankingService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IEnumerable<IRankingStrategy> strategies)
{
    public const int MinimumReviewsForRating = 2;
    public const int DefaultLimit = 10;
    public const int MaximumLimit = 50;

    private IRankingStrategy RatingStrategy =>
        strategies.Single(strategy => strategy.Metric == RankingMetric.Rating);

    private IRankingStrategy PopularityStrategy =>
        strategies.Single(strategy => strategy.Metric == RankingMetric.Popularity);

    public async Task<IReadOnlyList<TrackRankingEntry>> GetTopRatedTracksAsync(
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rankings = BuildTrackRankings(db)
            .Where(entry => entry.ReviewCount >= MinimumReviewsForRating);
        return await RatingStrategy.Apply(rankings)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackRankingEntry>> GetMostReviewedTracksAsync(
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await PopularityStrategy.Apply(BuildTrackRankings(db))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<TrackRankingEntry> BuildTrackRankings(ApplicationDbContext db) =>
        db.Reviews.AsNoTracking()
            .Select(review => new
            {
                review.TrackId,
                Title = review.Track!.Title,
                review.Rating,
                IsDemo = false
            })
            .Concat(db.DemoReviews.AsNoTracking()
                .Select(review => new
                {
                    review.TrackId,
                    Title = review.Track!.Title,
                    review.Rating,
                    IsDemo = true
                }))
            .GroupBy(review => new { review.TrackId, review.Title })
            .Select(group => new TrackRankingEntry
            {
                TrackId = group.Key.TrackId,
                Title = group.Key.Title,
                ReviewCount = group.Count(),
                AverageRating = group.Average(review => review.Rating),
                DemoReviewCount = group.Count(review => review.IsDemo)
            });

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaximumLimit)
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaximumLimit}.");
    }
}
