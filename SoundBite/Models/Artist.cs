using System.ComponentModel.DataAnnotations;

namespace SoundBite.Models;

public class Artist
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? SpotifyId { get; set; }

    public ICollection<Album> Albums { get; set; } = new List<Album>();

    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}
