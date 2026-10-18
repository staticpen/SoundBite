using System.ComponentModel.DataAnnotations;

namespace SoundBite.Models;

public class Album
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Missing IDs are null; supplied IDs must be nonempty and contain no whitespace.
    [MinLength(1)]
    [RegularExpression(@"\S+")]
    public string? SpotifyId { get; set; }

    [MaxLength(2048)]
    public string? CoverUrl { get; set; }

    public ICollection<Artist> Artists { get; set; } = new List<Artist>();

    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}
