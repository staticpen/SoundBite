using System.ComponentModel.DataAnnotations;

namespace SoundBite.Models;

public class Track
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Missing IDs are null; supplied IDs must be nonempty and contain no whitespace.
    [MinLength(1)]
    [RegularExpression(@"\S+")]
    public string? SpotifyId { get; set; }

    public ICollection<Artist> Artists { get; set; } = new List<Artist>();

    public int? AlbumId { get; set; }

    public Album? Album { get; set; }
}
