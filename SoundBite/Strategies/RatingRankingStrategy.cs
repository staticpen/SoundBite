using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Strategies;

public sealed class RatingRankingStrategy : IRankingStrategy
{
    public RankingMetric Metric => RankingMetric.Rating;

    /// <summary>Ranks tracks by average rating, then by review count and title.</summary>
    public IOrderedQueryable<TrackRankingEntry> Apply(IQueryable<TrackRankingEntry> rankings) =>
        // Tie-breaks make ordering predictable when tracks have equal average ratings.
        rankings.OrderByDescending(entry => entry.AverageRating)
            .ThenByDescending(entry => entry.ReviewCount)
            .ThenBy(entry => entry.Title)
            .ThenBy(entry => entry.TrackId);
}
