namespace SoundBite.Models;

public sealed class TrendingTrackEntry
{
    public int TrackId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? AlbumCoverUrl { get; init; }
    public int RecentReviewCount { get; init; }
    public double RecentAverageRating { get; init; }
}
