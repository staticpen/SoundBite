using System.ComponentModel.DataAnnotations;

namespace SoundBite.Models;

public class Artist
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // Missing IDs are null; supplied IDs must be nonempty and contain no whitespace.
    [MinLength(1)]
    [RegularExpression(@"\S+")]
    public string? SpotifyId { get; set; }

    public ICollection<Album> Albums { get; set; } = new List<Album>();

    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}
