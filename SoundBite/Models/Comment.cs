using System.ComponentModel.DataAnnotations;
using SoundBite.Data;

namespace SoundBite.Models;

public class Comment
{
    public int Id { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public int ReviewId { get; set; }
    public Review? Review { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
