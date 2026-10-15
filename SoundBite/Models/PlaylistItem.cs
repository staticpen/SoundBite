namespace SoundBite.Models;

public class PlaylistItem
{
    public int Id { get; set; }

    public int PlaylistId { get; set; }

    public Playlist? Playlist { get; set; }

    public int TrackId { get; set; }

    public Track? Track { get; set; }

    public int Position { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
