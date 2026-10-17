using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed record ReviewComment(
    int Id,
    string UserName,
    string DisplayName,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsOwner);

public sealed record ReviewInteractions(
    int LikeCount,
    bool IsLiked,
    IReadOnlyList<ReviewComment> Comments);

public sealed class SocialInteractionService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider)
{
    public const int MaximumCommentLength = 2000;

    public async Task<ReviewInteractions> GetReviewInteractionsAsync(
        int reviewId,
        CancellationToken cancellationToken = default)
    {
        ValidateReviewId(reviewId);
        var actorId = await CurrentUserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Reviews.AnyAsync(review => review.Id == reviewId, cancellationToken))
            throw new InvalidOperationException("Review was not found.");

        var likeCount = await db.Likes.CountAsync(like => like.ReviewId == reviewId, cancellationToken);
        var isLiked = actorId is not null &&
            await db.Likes.AnyAsync(like => like.ReviewId == reviewId && like.UserId == actorId, cancellationToken);
        var comments = await db.Comments.AsNoTracking()
            .Where(comment => comment.ReviewId == reviewId)
            .OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Select(comment => new ReviewComment(
                comment.Id,
                comment.User!.UserName!,
                comment.User.DisplayName,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt,
                actorId != null && comment.UserId == actorId))
            .ToListAsync(cancellationToken);

        return new ReviewInteractions(likeCount, isLiked, comments);
    }

    public Task LikeReviewAsync(int reviewId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(reviewId, true, cancellationToken);

    public Task UnlikeReviewAsync(int reviewId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(reviewId, false, cancellationToken);

    public async Task<ReviewComment> AddCommentAsync(
        int reviewId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        ValidateReviewId(reviewId);
        var cleanedContent = NormalizeComment(content);
        ValidateComment(cleanedContent);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetCurrentUserAsync(db, cancellationToken);
        if (!await db.Reviews.AnyAsync(review => review.Id == reviewId, cancellationToken))
            throw new InvalidOperationException("Review was not found.");

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            ReviewId = reviewId,
            UserId = user.Id,
            Content = cleanedContent,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        return ToReviewComment(comment, user, true);
    }

    public async Task<ReviewComment> UpdateCommentAsync(
        int commentId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        ValidateCommentId(commentId);
        var cleanedContent = NormalizeComment(content);
        ValidateComment(cleanedContent);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetCurrentUserAsync(db, cancellationToken);
        var comment = await db.Comments.SingleOrDefaultAsync(
            existing => existing.Id == commentId && existing.UserId == user.Id,
            cancellationToken);
        if (comment is null)
            throw new InvalidOperationException("Comment was not found for the current user.");

        comment.Content = cleanedContent;
        comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToReviewComment(comment, user, true);
    }

    public async Task DeleteCommentAsync(int commentId, CancellationToken cancellationToken = default)
    {
        ValidateCommentId(commentId);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetCurrentUserAsync(db, cancellationToken);
        var comment = await db.Comments.SingleOrDefaultAsync(
            existing => existing.Id == commentId && existing.UserId == user.Id,
            cancellationToken);
        if (comment is null)
            throw new InvalidOperationException("Comment was not found for the current user.");

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SetLikeAsync(int reviewId, bool liked, CancellationToken cancellationToken)
    {
        ValidateReviewId(reviewId);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetCurrentUserAsync(db, cancellationToken);
        if (!await db.Reviews.AnyAsync(review => review.Id == reviewId, cancellationToken))
            throw new InvalidOperationException("Review was not found.");

        if (liked)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Likes" ("ReviewId", "UserId", "CreatedAt")
                VALUES ({reviewId}, {user.Id}, {DateTime.UtcNow})
                ON CONFLICT ("ReviewId", "UserId") DO NOTHING
                """, cancellationToken);
        }
        else
        {
            await db.Likes.Where(like => like.ReviewId == reviewId && like.UserId == user.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task<ApplicationUser> GetCurrentUserAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = await CurrentUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Sign in to interact with reviews.");

        return await db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Your account is no longer available.");
    }

    private async Task<string?> CurrentUserIdAsync()
    {
        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        return principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }

    private static ReviewComment ToReviewComment(Comment comment, ApplicationUser user, bool isOwner) =>
        new(comment.Id, user.UserName ?? string.Empty, user.DisplayName, comment.Content,
            comment.CreatedAt, comment.UpdatedAt, isOwner);

    private static string NormalizeComment(string? content) => content?.Trim() ?? string.Empty;

    private static void ValidateComment(string content)
    {
        if (content.Length == 0)
            throw new ArgumentException("Comment text is required.", nameof(content));
        if (content.Length > MaximumCommentLength)
            throw new ArgumentException($"Comments cannot exceed {MaximumCommentLength} characters.", nameof(content));
    }

    private static void ValidateReviewId(int reviewId)
    {
        if (reviewId <= 0)
            throw new ArgumentOutOfRangeException(nameof(reviewId), "Review ID must be greater than zero.");
    }

    private static void ValidateCommentId(int commentId)
    {
        if (commentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(commentId), "Comment ID must be greater than zero.");
    }
}
