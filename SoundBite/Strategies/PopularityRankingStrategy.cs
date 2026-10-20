using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Strategies;

public sealed class PopularityRankingStrategy : IRankingStrategy
{
    public RankingMetric Metric => RankingMetric.Popularity;

    /// <summary>Ranks tracks by review count, then by rating and title.</summary>
    public IOrderedQueryable<TrackRankingEntry> Apply(IQueryable<TrackRankingEntry> rankings) =>
        // Review count is the main popularity measure; the remaining keys break ties.
        rankings.OrderByDescending(entry => entry.ReviewCount)
            .ThenByDescending(entry => entry.AverageRating)
            .ThenBy(entry => entry.Title)
            .ThenBy(entry => entry.TrackId);
}
