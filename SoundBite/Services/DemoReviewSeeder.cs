using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed record DemoReviewSeedResult(int TracksSeeded, int ReviewsSeeded);

public sealed class DemoReviewSeeder(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public const int TrackLimit = 12;

    // Creates or refreshes repeatable sample reviews for the first catalogue tracks.
    public async Task<DemoReviewSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tracks = await db.Tracks.AsNoTracking()
            .Where(track => track.SpotifyId != null)
            .OrderBy(track => track.Id)
            .Take(TrackLimit)
            .Select(track => new { track.Id, track.SpotifyId })
            .ToListAsync(cancellationToken);
        if (tracks.Count < RankingService.MinimumReviewsForRating)
        {
            throw new InvalidOperationException(
                $"At least {RankingService.MinimumReviewsForRating} Spotify tracks are required before seeding demonstration rankings.");
        }

        var now = DateTime.UtcNow;
        var seededCount = 0;
        for (var index = 0; index < tracks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = tracks[index];
            var sampleReviews = new List<DemoReview>
            {
                CreateReview(track.Id, track.SpotifyId!, 1, index % 3 == 0 ? 5 : 4,
                    index < 5 ? now.AddDays(-1) : now.AddDays(-15)),
                CreateReview(track.Id, track.SpotifyId!, 2, index % 4 == 0 ? 5 : 3,
                    index < 5 ? now.AddDays(-3) : now.AddDays(-30))
            };
            if (index < 2)
                sampleReviews.Add(CreateReview(track.Id, track.SpotifyId!, 3, 5, now.AddDays(-2)));

            foreach (var sampleReview in sampleReviews)
            {
                var existing = await db.DemoReviews.SingleOrDefaultAsync(
                    review => review.SeedKey == sampleReview.SeedKey,
                    cancellationToken);
                if (existing is null)
                {
                    db.DemoReviews.Add(sampleReview);
                }
                else
                {
                    existing.ReviewerLabel = sampleReview.ReviewerLabel;
                    existing.Rating = sampleReview.Rating;
                    existing.Comment = sampleReview.Comment;
                    existing.CreatedAt = sampleReview.CreatedAt;
                    existing.TrackId = sampleReview.TrackId;
                }

                seededCount++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new DemoReviewSeedResult(tracks.Count, seededCount);
    }

    // Builds one stable sample review so rerunning the seeder updates rather than duplicates it.
    private static DemoReview CreateReview(
        int trackId,
        string spotifyId,
        int reviewerNumber,
        int rating,
        DateTime createdAt) =>
        new()
        {
            SeedKey = $"presentation:{spotifyId}:reviewer-{reviewerNumber}",
            ReviewerLabel = $"Sample listener {reviewerNumber}",
            Rating = rating,
            Comment = "Demonstration review — sample data, not a community submission.",
            TrackId = trackId,
            CreatedAt = createdAt
        };
}
