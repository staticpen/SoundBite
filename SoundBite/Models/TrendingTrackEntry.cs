namespace SoundBite.Models;

public sealed class TrendingTrackEntry
{
    public int TrackId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int RecentReviewCount { get; init; }
    public double RecentAverageRating { get; init; }
    public int DemoReviewCount { get; init; }
}
