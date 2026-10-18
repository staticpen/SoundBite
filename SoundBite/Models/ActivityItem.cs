namespace SoundBite.Models;

public enum ActivityType
{
    Review,
    Comment,
    Like,
    Favourite
}

public sealed record ActivityItem(
    ActivityType Type,
    int Id,
    int? ReviewId,
    string UserName,
    string DisplayName,
    int? TrackId,
    string? TrackTitle,
    int? Rating,
    string Content,
    DateTime CreatedAt,
    LikeTargetType? TargetType = null,
    string? TargetUserName = null,
    string? TargetDisplayName = null,
    int? ParentCommentId = null,
    string? ParentUserName = null,
    string? ParentDisplayName = null)
{
    public string ActorName => string.IsNullOrWhiteSpace(DisplayName) ? UserName : DisplayName;
    public string TargetActorName => string.IsNullOrWhiteSpace(TargetDisplayName) ? TargetUserName ?? string.Empty : TargetDisplayName;
    public string ParentActorName => string.IsNullOrWhiteSpace(ParentDisplayName) ? ParentUserName ?? string.Empty : ParentDisplayName;
}

public sealed record ActivityPage(IReadOnlyList<ActivityItem> Items, bool HasNext);
