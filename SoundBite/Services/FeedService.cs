using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class FeedService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider)
{
    public const int PageSize = 20;

    public async Task<ActivityPage> GetPageAsync(int page = 0, CancellationToken cancellationToken = default)
    {
        if (page < 0 || page > int.MaxValue / PageSize - 1)
            throw new ArgumentOutOfRangeException(nameof(page));

        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var actorId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (string.IsNullOrWhiteSpace(actorId))
            throw new UnauthorizedAccessException("Sign in to view your activity feed.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(user => user.Id == actorId, cancellationToken))
            throw new UnauthorizedAccessException("Your account is no longer available.");

        var followedUserIds = db.Follows.AsNoTracking()
            .Where(follow => follow.FollowerId == actorId)
            .Select(follow => follow.FollowingId);

        IQueryable<ActivityRow> rows = db.Reviews.AsNoTracking()
            .Select(review => new ActivityRow
            {
                Type = (int)ActivityType.Review,
                Id = review.Id,
                ReviewId = review.Id,
                UserId = review.UserId,
                UserName = review.User!.UserName!,
                DisplayName = review.User.DisplayName,
                TrackId = review.TrackId,
                TrackTitle = review.Track!.Title,
                Rating = (int?)review.Rating,
                Content = review.Comment,
                CreatedAt = review.CreatedAt,
                TargetType = null,
                TargetUserName = null,
                TargetDisplayName = null,
                ParentCommentId = null,
                ParentUserName = null,
                ParentDisplayName = null
            });

        rows = rows.Concat(db.Comments.AsNoTracking()
            .Select(comment => new ActivityRow
            {
                Type = (int)ActivityType.Comment,
                Id = comment.Id,
                ReviewId = comment.ReviewId,
                UserId = comment.UserId,
                UserName = comment.User!.UserName!,
                DisplayName = comment.User.DisplayName,
                TrackId = comment.Review!.TrackId,
                TrackTitle = comment.Review.Track!.Title,
                Rating = (int?)comment.Review.Rating,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                ParentCommentId = comment.ParentCommentId,
                ParentUserName = comment.ParentComment == null ? null : comment.ParentComment.User!.UserName,
                ParentDisplayName = comment.ParentComment == null ? null : comment.ParentComment.User!.DisplayName,
                TargetType = null,
                TargetUserName = null,
                TargetDisplayName = null
            }));

        rows = rows.Concat(db.Likes.AsNoTracking()
            .Where(like => like.ReviewId != null)
            .Select(like => new ActivityRow
            {
                Type = (int)ActivityType.Like,
                Id = like.Id,
                ReviewId = like.Review!.Id,
                UserId = like.UserId,
                UserName = like.User!.UserName!,
                DisplayName = like.User.DisplayName,
                TrackId = like.Review.TrackId,
                TrackTitle = like.Review.Track!.Title,
                Rating = (int?)like.Review.Rating,
                Content = like.Review.Comment,
                CreatedAt = like.CreatedAt,
                TargetType = (int)LikeTargetType.Review,
                TargetUserName = like.Review.User!.UserName,
                TargetDisplayName = like.Review.User.DisplayName,
                ParentCommentId = null,
                ParentUserName = null,
                ParentDisplayName = null
            }));

        rows = rows.Concat(db.Likes.AsNoTracking()
            .Where(like => like.CommentId != null)
            .Select(like => new ActivityRow
            {
                Type = (int)ActivityType.Like,
                Id = like.Id,
                ReviewId = like.Comment!.ReviewId,
                UserId = like.UserId,
                UserName = like.User!.UserName!,
                DisplayName = like.User.DisplayName,
                TrackId = like.Comment.Review!.TrackId,
                TrackTitle = like.Comment.Review.Track!.Title,
                Rating = (int?)like.Comment.Review.Rating,
                Content = like.Comment.Content,
                CreatedAt = like.CreatedAt,
                TargetType = (int)LikeTargetType.Comment,
                TargetUserName = like.Comment.User!.UserName,
                TargetDisplayName = like.Comment.User.DisplayName,
                ParentCommentId = like.Comment.ParentCommentId,
                ParentUserName = like.Comment.ParentComment == null ? null : like.Comment.ParentComment.User!.UserName,
                ParentDisplayName = like.Comment.ParentComment == null ? null : like.Comment.ParentComment.User!.DisplayName,
            }));

        rows = rows.Concat(db.Likes.AsNoTracking()
            .Where(like => like.TrackId != null)
            .Select(like => new ActivityRow
            {
                Type = (int)ActivityType.Like,
                Id = like.Id,
                UserId = like.UserId,
                UserName = like.User!.UserName!,
                DisplayName = like.User.DisplayName,
                TrackId = like.Track!.Id,
                TrackTitle = like.Track.Title,
                ReviewId = null,
                Rating = null,
                Content = string.Empty,
                CreatedAt = like.CreatedAt,
                TargetType = (int)LikeTargetType.Track,
                TargetUserName = null,
                TargetDisplayName = null,
                ParentCommentId = null,
                ParentUserName = null,
                ParentDisplayName = null,
            }));

        rows = rows.Concat(db.FavouriteTracks.AsNoTracking()
            .Select(favourite => new ActivityRow
            {
                Type = (int)ActivityType.Favourite,
                Id = favourite.TrackId,
                ReviewId = null,
                UserId = favourite.UserId,
                UserName = favourite.User!.UserName!,
                DisplayName = favourite.User.DisplayName,
                TrackId = favourite.Track!.Id,
                TrackTitle = favourite.Track.Title,
                Rating = null,
                Content = string.Empty,
                CreatedAt = favourite.AddedAt,
                TargetType = null,
                TargetUserName = null,
                TargetDisplayName = null,
                ParentCommentId = null,
                ParentUserName = null,
                ParentDisplayName = null
            }));

        rows = rows.Where(item => followedUserIds.Contains(item.UserId));

        var results = await rows
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Type)
            .ThenByDescending(item => item.Id)
            .ThenBy(item => item.UserName)
            .Skip(page * PageSize)
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);
        var entries = results.Select(item => item.ToActivityItem()).ToArray();

        return new ActivityPage(entries.Take(PageSize).ToArray(), entries.Length > PageSize);
    }

    private sealed class ActivityRow
    {
        public int Type { get; init; }
        public int Id { get; init; }
        public int? ReviewId { get; init; }
        public string UserId { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int? TrackId { get; init; }
        public string? TrackTitle { get; init; }
        public int? Rating { get; init; }
        public string Content { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public int? TargetType { get; init; }
        public string? TargetUserName { get; init; }
        public string? TargetDisplayName { get; init; }
        public int? ParentCommentId { get; init; }
        public string? ParentUserName { get; init; }
        public string? ParentDisplayName { get; init; }

        public ActivityItem ToActivityItem() => new(
            (ActivityType)Type,
            Id,
            ReviewId,
            UserName,
            DisplayName,
            TrackId,
            TrackTitle,
            Rating,
            Content,
            CreatedAt,
            TargetType is int targetType ? (LikeTargetType)targetType : null,
            TargetUserName,
            TargetDisplayName,
            ParentCommentId,
            ParentUserName,
            ParentDisplayName);
    }
}
