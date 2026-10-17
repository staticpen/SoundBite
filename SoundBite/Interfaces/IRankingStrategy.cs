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

    IOrderedQueryable<TrackRankingEntry> Apply(IQueryable<TrackRankingEntry> rankings);
}
