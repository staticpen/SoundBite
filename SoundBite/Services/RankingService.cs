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

    // Gets the highest-rated tracks that have enough reviews to be reliable.
    public async Task<IReadOnlyList<TrackRankingEntry>> GetTopRatedTracksAsync(
        int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Require multiple reviews so one rating alone cannot decide the top-rated list.
        var rankings = BuildTrackRankings(db)
            .Where(entry => entry.ReviewCount >= MinimumReviewsForRating);
        return await RatingStrategy.Apply(rankings)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    // Gets tracks ordered by how many reviews they have.
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

    // Builds a query with each track's review count and average rating.
    private static IQueryable<TrackRankingEntry> BuildTrackRankings(ApplicationDbContext db) =>
        db.Reviews.AsNoTracking()
            .Select(review => new
            {
                review.TrackId,
                Title = review.Track!.Title,
                AlbumCoverUrl = review.Track.Album!.CoverUrl,
                review.Rating,
            })
            // Group per track to compute its all-time review count and average rating.
            .GroupBy(review => new { review.TrackId, review.Title, review.AlbumCoverUrl })
            .Select(group => new TrackRankingEntry
            {
                TrackId = group.Key.TrackId,
                Title = group.Key.Title,
                AlbumCoverUrl = group.Key.AlbumCoverUrl,
                ReviewCount = group.Count(),
                AverageRating = group.Average(review => review.Rating)
            });

    // Makes sure ranking requests stay within the supported result size.
    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaximumLimit)
            throw new ArgumentOutOfRangeException(nameof(limit), $"Limit must be between 1 and {MaximumLimit}.");
    }
}
