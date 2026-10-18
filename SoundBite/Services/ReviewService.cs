using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public class ReviewService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
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

    public async Task<Review?> GetUserReviewForTrackAsync(
        string userId,
        int trackId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Reviews.AsNoTracking()
            .SingleOrDefaultAsync(review => review.TrackId == trackId && review.UserId == userId, cancellationToken);
    }

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

    private static string NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim();
        return trimmed ?? string.Empty;
    }

    private static void ValidateComment(string comment)
    {
        if (comment.Length > 2000)
            throw new ArgumentException("Review comments cannot exceed 2000 characters.", nameof(comment));
    }

    private static void ValidateReviewCreateInputs(string userId, int trackId, int rating)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), "Track ID must be greater than zero.");

        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
    }

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
