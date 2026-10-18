using System.ComponentModel.DataAnnotations;

namespace SoundBite.Models;

public sealed class DemoReview
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string SeedKey { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string ReviewerLabel { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    [Required]
    public int TrackId { get; set; }

    public Track? Track { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
