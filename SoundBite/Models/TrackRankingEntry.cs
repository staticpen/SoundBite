namespace SoundBite.Models;

public sealed class TrackRankingEntry
{
    public int TrackId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int ReviewCount { get; init; }
    public double AverageRating { get; init; }
}
