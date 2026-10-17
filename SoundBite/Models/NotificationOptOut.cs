using SoundBite.Data;

namespace SoundBite.Models;

// One row means the user has turned this notification category off.
// A user with no rows receives every category.
public class NotificationOptOut
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public NotificationType Type { get; set; }
}
