using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Strategies;

public sealed class RatingRankingStrategy : IRankingStrategy
{
    public RankingMetric Metric => RankingMetric.Rating;

    public IOrderedQueryable<TrackRankingEntry> Apply(IQueryable<TrackRankingEntry> rankings) =>
        rankings.OrderByDescending(entry => entry.AverageRating)
            .ThenByDescending(entry => entry.ReviewCount)
            .ThenBy(entry => entry.Title)
            .ThenBy(entry => entry.TrackId);
}
