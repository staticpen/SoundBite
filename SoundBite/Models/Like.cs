using SoundBite.Data;

namespace SoundBite.Models;

public enum LikeTargetType
{
    Review,
    Track,
    Comment
}

public class Like
{
    public int Id { get; set; }

    public int? ReviewId { get; set; }
    public Review? Review { get; set; }

    public int? TrackId { get; set; }
    public Track? Track { get; set; }

    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
