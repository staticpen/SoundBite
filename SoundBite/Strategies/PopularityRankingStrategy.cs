using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Strategies;

public sealed class PopularityRankingStrategy : IRankingStrategy
{
    public RankingMetric Metric => RankingMetric.Popularity;

    public IOrderedQueryable<TrackRankingEntry> Apply(IQueryable<TrackRankingEntry> rankings) =>
        rankings.OrderByDescending(entry => entry.ReviewCount)
            .ThenByDescending(entry => entry.AverageRating)
            .ThenBy(entry => entry.Title)
            .ThenBy(entry => entry.TrackId);
}
