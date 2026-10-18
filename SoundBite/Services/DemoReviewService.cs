using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class DemoReviewService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task<IReadOnlyList<DemoReview>> GetForTrackAsync(
        int trackId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DemoReviews.AsNoTracking()
            .Where(review => review.TrackId == trackId)
            .OrderByDescending(review => review.CreatedAt)
            .ThenBy(review => review.Id)
            .ToListAsync(cancellationToken);
    }
}
