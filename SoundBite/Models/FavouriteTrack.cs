using SoundBite.Data;

namespace SoundBite.Models;

public class FavouriteTrack
{
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int TrackId { get; set; }

    public Track? Track { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
