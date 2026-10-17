using SoundBite.Data;

namespace SoundBite.Models;

public class Like
{
    public int Id { get; set; }

    public int ReviewId { get; set; }
    public Review? Review { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
