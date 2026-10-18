using Microsoft.EntityFrameworkCore;
using SoundBite.Data;

namespace SoundBite.Services;

public class UserService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    // Returns the display name when set, otherwise the username; null when the user doesn't exist.
    public async Task<string?> GetPreferredNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.DisplayName, candidate.UserName })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return null;
        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
    }
}
