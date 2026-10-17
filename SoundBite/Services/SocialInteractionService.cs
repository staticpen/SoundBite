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
    bool IsOwner,
    int LikeCount,
    bool IsLiked);

public sealed record ReviewInteractions(
    int LikeCount,
    bool IsLiked,
    IReadOnlyList<ReviewComment> Comments);

public sealed record LikeSummary(int Count, bool IsLiked);

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
                actorId != null && comment.UserId == actorId,
                db.Likes.Count(like => like.CommentId == comment.Id),
                actorId != null && db.Likes.Any(like => like.CommentId == comment.Id && like.UserId == actorId)))
            .ToListAsync(cancellationToken);

        return new ReviewInteractions(likeCount, isLiked, comments);
    }

    public Task LikeReviewAsync(int reviewId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Review, reviewId, true, cancellationToken);

    public Task UnlikeReviewAsync(int reviewId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Review, reviewId, false, cancellationToken);

    public Task LikeTrackAsync(int trackId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Track, trackId, true, cancellationToken);

    public Task UnlikeTrackAsync(int trackId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Track, trackId, false, cancellationToken);

    public Task LikePlaylistAsync(int playlistId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Playlist, playlistId, true, cancellationToken);

    public Task UnlikePlaylistAsync(int playlistId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Playlist, playlistId, false, cancellationToken);

    public Task LikeCommentAsync(int commentId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Comment, commentId, true, cancellationToken);

    public Task UnlikeCommentAsync(int commentId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(LikeTargetType.Comment, commentId, false, cancellationToken);

    public Task LikeAsync(LikeTargetType targetType, int targetId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(targetType, targetId, true, cancellationToken);

    public Task UnlikeAsync(LikeTargetType targetType, int targetId, CancellationToken cancellationToken = default) =>
        SetLikeAsync(targetType, targetId, false, cancellationToken);

    public async Task<LikeSummary> GetLikeSummaryAsync(
        LikeTargetType targetType,
        int targetId,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(targetType, targetId);
        var actorId = await CurrentUserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureTargetAvailableAsync(db, targetType, targetId, actorId, cancellationToken);
        var count = await LikeQuery(db.Likes, targetType, targetId).CountAsync(cancellationToken);
        var isLiked = actorId is not null && await LikeQuery(db.Likes, targetType, targetId)
            .AnyAsync(like => like.UserId == actorId, cancellationToken);
        return new LikeSummary(count, isLiked);
    }

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

    private async Task SetLikeAsync(
        LikeTargetType targetType,
        int targetId,
        bool liked,
        CancellationToken cancellationToken)
    {
        ValidateTarget(targetType, targetId);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetCurrentUserAsync(db, cancellationToken);
        await EnsureTargetAvailableAsync(db, targetType, targetId, user.Id, cancellationToken);

        if (liked)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Likes" ("ReviewId", "TrackId", "PlaylistId", "CommentId", "UserId", "CreatedAt")
                VALUES (
                    {(targetType == LikeTargetType.Review ? targetId : null)},
                    {(targetType == LikeTargetType.Track ? targetId : null)},
                    {(targetType == LikeTargetType.Playlist ? targetId : null)},
                    {(targetType == LikeTargetType.Comment ? targetId : null)},
                    {user.Id},
                    {DateTime.UtcNow})
                ON CONFLICT DO NOTHING
                """, cancellationToken);
        }
        else
        {
            await LikeQuery(db.Likes, targetType, targetId)
                .Where(like => like.UserId == user.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static IQueryable<Like> LikeQuery(IQueryable<Like> likes, LikeTargetType targetType, int targetId) =>
        targetType switch
        {
            LikeTargetType.Review => likes.Where(like => like.ReviewId == targetId),
            LikeTargetType.Track => likes.Where(like => like.TrackId == targetId),
            LikeTargetType.Playlist => likes.Where(like => like.PlaylistId == targetId),
            LikeTargetType.Comment => likes.Where(like => like.CommentId == targetId),
            _ => throw new ArgumentOutOfRangeException(nameof(targetType))
        };

    private static async Task EnsureTargetAvailableAsync(
        ApplicationDbContext db,
        LikeTargetType targetType,
        int targetId,
        string? actorId,
        CancellationToken cancellationToken)
    {
        var exists = targetType switch
        {
            LikeTargetType.Review => await db.Reviews.AnyAsync(review => review.Id == targetId, cancellationToken),
            LikeTargetType.Track => await db.Tracks.AnyAsync(track => track.Id == targetId, cancellationToken),
            LikeTargetType.Playlist => await db.Playlists.AnyAsync(
                playlist => playlist.Id == targetId && (playlist.IsPublic || playlist.OwnerId == actorId),
                cancellationToken),
            LikeTargetType.Comment => await db.Comments.AnyAsync(comment => comment.Id == targetId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(targetType))
        };
        if (!exists)
            throw new InvalidOperationException($"{targetType} was not found or is not available.");
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
            comment.CreatedAt, comment.UpdatedAt, isOwner, 0, false);

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

    private static void ValidateTarget(LikeTargetType targetType, int targetId)
    {
        if (!Enum.IsDefined(targetType))
            throw new ArgumentOutOfRangeException(nameof(targetType));
        ValidateReviewId(targetId);
    }

    private static void ValidateCommentId(int commentId)
    {
        if (commentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(commentId), "Comment ID must be greater than zero.");
    }
}
