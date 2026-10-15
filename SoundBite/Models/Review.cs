using System.ComponentModel.DataAnnotations;
using SoundBite.Data;

namespace SoundBite.Models;

public class Review
{
    public int Id { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    public int TrackId { get; set; }

    public Track? Track { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
