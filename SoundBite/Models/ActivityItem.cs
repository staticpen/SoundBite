namespace SoundBite.Models;

public enum ActivityType
{
    Review,
    Comment
}

public sealed record ActivityItem(
    ActivityType Type,
    int Id,
    int ReviewId,
    string UserName,
    string DisplayName,
    int TrackId,
    string TrackTitle,
    int? Rating,
    string Content,
    DateTime CreatedAt)
{
    public string ActorName => string.IsNullOrWhiteSpace(DisplayName) ? UserName : DisplayName;
}

public sealed record ActivityPage(IReadOnlyList<ActivityItem> Items, bool HasNext);
