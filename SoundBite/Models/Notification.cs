using SoundBite.Data;

namespace SoundBite.Models;

public enum NotificationType
{
    Follow,
    ReviewComment,
    CommentReply,
    ReviewLike,
    CommentLike
}

public class Notification
{
    public int Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public ApplicationUser? Recipient { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public ApplicationUser? Actor { get; set; }
    public NotificationType Type { get; set; }
    public int? ReviewId { get; set; }
    public Review? Review { get; set; }
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }
    public int? LikeId { get; set; }
    public Like? Like { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public sealed record NotificationItem(
    int Id,
    NotificationType Type,
    string ActorUserName,
    string ActorDisplayName,
    DateTime CreatedAt,
    DateTime? ReadAt,
    int? TrackId,
    int? ReviewId,
    string? Content)
{
    public string ActorName => string.IsNullOrWhiteSpace(ActorDisplayName) ? ActorUserName : ActorDisplayName;
}

public sealed record NotificationPage(IReadOnlyList<NotificationItem> Items, int UnreadCount, bool HasMore = false);
