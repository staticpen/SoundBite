using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed record ReviewComment(
    int Id,
    int? ParentCommentId,
    string? ParentUserName,
    string? ParentDisplayName,
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
    AuthenticationStateProvider authenticationStateProvider,
    NotificationEventDispatcher notificationEvents)
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
            .OrderBy(comment => comment.ParentCommentId)
            .ThenBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Select(comment => new ReviewComment(
                comment.Id,
                comment.ParentCommentId,
                comment.ParentComment == null ? null : comment.ParentComment.User!.UserName,
                comment.ParentComment == null ? null : comment.ParentComment.User!.DisplayName,
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
        CancellationToken cancellationToken = default) =>
        await SaveCommentAsync(reviewId, null, content, cancellationToken);

    public async Task<ReviewComment> AddReplyAsync(
        int parentCommentId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        ValidateCommentId(parentCommentId);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var parent = await db.Comments
            .Include(comment => comment.User)
            .SingleOrDefaultAsync(comment => comment.Id == parentCommentId, cancellationToken)
            ?? throw new InvalidOperationException("Parent comment was not found.");
        if (parent.ParentCommentId is not null)
            throw new InvalidOperationException("Replies can only be added to top-level comments.");

        return await SaveCommentAsync(
            parent.ReviewId,
            parent.Id,
            content,
            cancellationToken,
            parent.User);
    }

    private async Task<ReviewComment> SaveCommentAsync(
        int reviewId,
        int? parentCommentId,
        string? content,
        CancellationToken cancellationToken,
        ApplicationUser? parentUser = null)
    {
        ValidateReviewId(reviewId);
        var cleanedContent = NormalizeComment(content);
        ValidateComment(cleanedContent);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetCurrentUserAsync(db, cancellationToken);
        if (!await db.Reviews.AnyAsync(review => review.Id == reviewId, cancellationToken))
            throw new InvalidOperationException("Review was not found.");
        var recipientUserId = parentCommentId is not null
            ? parentUser?.Id
            : await db.Reviews.Where(review => review.Id == reviewId)
                .Select(review => review.UserId)
                .SingleAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            ReviewId = reviewId,
            ParentCommentId = parentCommentId,
            UserId = user.Id,
            Content = cleanedContent,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Comments.Add(comment);
        // The comment is always saved; the notification only if the recipient has not turned it off.
        var notificationType = parentCommentId is null ? NotificationType.ReviewComment : NotificationType.CommentReply;
        var notify = recipientUserId is not null && recipientUserId != user.Id &&
            await NotificationService.IsEnabledAsync(db, recipientUserId, notificationType, cancellationToken);
        if (notify)
        {
            db.Notifications.Add(new Notification
            {
                RecipientUserId = recipientUserId!,
                ActorUserId = user.Id,
                Type = notificationType,
                ReviewId = reviewId,
                Comment = comment
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        if (notify)
            await notificationEvents.PublishAsync(recipientUserId!);
        return ToReviewComment(comment, user, true, parentUser);
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

        await db.Comments
            .Where(reply => reply.ParentCommentId == commentId)
            .ExecuteUpdateAsync(update => update.SetProperty(reply => reply.ParentCommentId, (int?)null),
                cancellationToken);
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
        var notificationRecipientId = await GetNotificationRecipientIdAsync(
            db, targetType, targetId, cancellationToken);

        if (liked)
        {
            var notified = false;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
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
            if (inserted > 0 && notificationRecipientId is not null && notificationRecipientId != user.Id)
            {
                var likeId = await LikeQuery(db.Likes, targetType, targetId)
                    .Where(like => like.UserId == user.Id)
                    .Select(like => like.Id)
                    .SingleAsync(cancellationToken);
                var notification = CreateLikeNotification(
                    targetType,
                    targetId,
                    targetType == LikeTargetType.Review
                        ? targetId
                        : targetType == LikeTargetType.Comment
                            ? await db.Comments.Where(comment => comment.Id == targetId)
                                .Select(comment => (int?)comment.ReviewId).SingleAsync(cancellationToken)
                            : null,
                    likeId,
                    notificationRecipientId,
                    user.Id);
                // The like is always saved; the notification only if the recipient has not turned it off.
                if (notification is not null && await NotificationService.IsEnabledAsync(
                        db, notificationRecipientId, notification.Type, cancellationToken))
                {
                    db.Notifications.Add(notification);
                    await db.SaveChangesAsync(cancellationToken);
                    notified = true;
                }
            }
            await transaction.CommitAsync(cancellationToken);
            if (notified)
                await notificationEvents.PublishAsync(notificationRecipientId!);
        }
        else
        {
            await LikeQuery(db.Likes, targetType, targetId)
                .Where(like => like.UserId == user.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static async Task<string?> GetNotificationRecipientIdAsync(
        ApplicationDbContext db,
        LikeTargetType targetType,
        int targetId,
        CancellationToken cancellationToken) =>
        targetType switch
        {
            LikeTargetType.Review => await db.Reviews
                .Where(review => review.Id == targetId)
                .Select(review => review.UserId)
                .SingleAsync(cancellationToken),
            LikeTargetType.Comment => await db.Comments
                .Where(comment => comment.Id == targetId)
                .Select(comment => comment.UserId)
                .SingleAsync(cancellationToken),
            LikeTargetType.Playlist => await db.Playlists
                .Where(playlist => playlist.Id == targetId)
                .Select(playlist => playlist.OwnerId)
                .SingleAsync(cancellationToken),
            LikeTargetType.Track => null,
            _ => throw new ArgumentOutOfRangeException(nameof(targetType))
        };

    private static Notification? CreateLikeNotification(
        LikeTargetType targetType,
        int targetId,
        int? reviewId,
        int likeId,
        string recipientId,
        string actorId) =>
        targetType switch
        {
            LikeTargetType.Review => new Notification
            {
                RecipientUserId = recipientId,
                ActorUserId = actorId,
                Type = NotificationType.ReviewLike,
                ReviewId = targetId,
                LikeId = likeId
            },
            LikeTargetType.Comment => new Notification
            {
                RecipientUserId = recipientId,
                ActorUserId = actorId,
                Type = NotificationType.CommentLike,
                ReviewId = reviewId,
                CommentId = targetId,
                LikeId = likeId
            },
            LikeTargetType.Playlist => new Notification
            {
                RecipientUserId = recipientId,
                ActorUserId = actorId,
                Type = NotificationType.PlaylistLike,
                PlaylistId = targetId,
                LikeId = likeId
            },
            LikeTargetType.Track => null,
            _ => throw new ArgumentOutOfRangeException(nameof(targetType))
        };

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

    private static ReviewComment ToReviewComment(
        Comment comment,
        ApplicationUser user,
        bool isOwner,
        ApplicationUser? parentUser = null) =>
        new(comment.Id, comment.ParentCommentId, parentUser?.UserName, parentUser?.DisplayName,
            user.UserName ?? string.Empty, user.DisplayName, comment.Content,
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
