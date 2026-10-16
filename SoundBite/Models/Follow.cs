using SoundBite.Data;

namespace SoundBite.Models;

public class Follow
{
    public string FollowerId { get; set; } = string.Empty;
    public string FollowingId { get; set; } = string.Empty;
    public ApplicationUser Follower { get; set; } = null!;
    public ApplicationUser Following { get; set; } = null!;
}
