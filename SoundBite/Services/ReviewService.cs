using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public class ReviewService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    // Creates a new review after checking the user, track, and review values.
    public async Task<Review> CreateReviewAsync(
        string userId,
        int trackId,
        int rating,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var cleanedComment = NormalizeComment(comment);
        ValidateReviewCreateInputs(userId, trackId, rating);
        ValidateComment(cleanedComment);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!await db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            throw new InvalidOperationException("User was not found.");

        if (!await db.Tracks.AnyAsync(track => track.Id == trackId, cancellationToken))
            throw new InvalidOperationException($"Track {trackId} was not found.");

        // A listener can contribute only one rating per track; they edit that review instead.
        if (await db.Reviews.AnyAsync(review => review.TrackId == trackId && review.UserId == userId, cancellationToken))
            throw new InvalidOperationException("A review for this track already exists for the current user.");

        var review = new Review
        {
            UserId = userId,
            TrackId = trackId,
            Rating = rating,
            Comment = cleanedComment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);
        return review;
    }

    // Gets a track's reviews with the users who wrote them.
    public async Task<IReadOnlyList<Review>> GetReviewsForTrackAsync(
        int trackId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Reviews.AsNoTracking()
            .Include(review => review.User)
            .Where(review => review.TrackId == trackId)
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .ToListAsync(cancellationToken);
    }

    // Gets a user's reviews together with their tracks and artists.
    public async Task<IReadOnlyList<Review>> GetReviewsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Reviews.AsNoTracking()
            .Where(review => review.UserId == userId)
            .Include(review => review.Track)
                .ThenInclude(track => track!.Artists.OrderBy(artist => artist.Name))
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .ToListAsync(cancellationToken);
    }

    // Finds one user's review for a track, if it exists.
    public async Task<Review?> GetUserReviewForTrackAsync(
        string userId,
        int trackId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Reviews.AsNoTracking()
            .SingleOrDefaultAsync(review => review.TrackId == trackId && review.UserId == userId, cancellationToken);
    }

    // Updates a review owned by the given user.
    public async Task<Review> UpdateReviewAsync(
        int reviewId,
        string userId,
        int rating,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var cleanedComment = NormalizeComment(comment);
        ValidateReviewUpdateInputs(userId, reviewId, rating);
        ValidateComment(cleanedComment);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var review = await db.Reviews
            .SingleOrDefaultAsync(existing => existing.Id == reviewId && existing.UserId == userId, cancellationToken);

        if (review is null)
            throw new InvalidOperationException("Review not found for the current user.");

        review.Rating = rating;
        review.Comment = cleanedComment;
        review.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return review;
    }

    // Trims a comment and treats a missing value as empty text.
    private static string NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim();
        return trimmed ?? string.Empty;
    }

    // Checks that the review comment fits the allowed length.
    private static void ValidateComment(string comment)
    {
        if (comment.Length > 2000)
            throw new ArgumentException("Review comments cannot exceed 2000 characters.", nameof(comment));
    }

    // Checks the values needed before creating a review.
    private static void ValidateReviewCreateInputs(string userId, int trackId, int rating)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), "Track ID must be greater than zero.");

        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
    }

    // Checks the values needed before changing a review.
    private static void ValidateReviewUpdateInputs(string userId, int reviewId, int rating)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (reviewId <= 0)
            throw new ArgumentOutOfRangeException(nameof(reviewId), "Review ID must be greater than zero.");

        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
    }
}
