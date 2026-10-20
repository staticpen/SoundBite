using SoundBite.Models;

namespace SoundBite.Interfaces;

public enum RankingMetric
{
    Rating,
    Popularity
}

public interface IRankingStrategy
{
    RankingMetric Metric { get; }

    /// <summary>Orders track rankings according to this strategy's metric.</summary>
    IOrderedQueryable<TrackRankingEntry> Apply(IQueryable<TrackRankingEntry> rankings);
}
