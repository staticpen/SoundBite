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

        var userIds = await db.Follows
            .Where(follow => follow.FollowerId == actorId)
            .Select(follow => follow.FollowingId)
            .ToListAsync(cancellationToken);
        userIds.Add(actorId);

        var reviews = db.Reviews.AsNoTracking()
            .Where(review => userIds.Contains(review.UserId))
            .Select(review => new
            {
                Type = (int)ActivityType.Review,
                Id = review.Id,
                ReviewId = review.Id,
                UserName = review.User!.UserName!,
                DisplayName = review.User.DisplayName,
                TrackId = review.TrackId,
                TrackTitle = review.Track!.Title,
                Rating = (int?)review.Rating,
                Content = review.Comment,
                CreatedAt = review.CreatedAt
            });

        var comments = db.Comments.AsNoTracking()
            .Where(comment => userIds.Contains(comment.UserId))
            .Select(comment => new
            {
                Type = (int)ActivityType.Comment,
                Id = comment.Id,
                ReviewId = comment.ReviewId,
                UserName = comment.User!.UserName!,
                DisplayName = comment.User.DisplayName,
                TrackId = comment.Review!.TrackId,
                TrackTitle = comment.Review.Track!.Title,
                Rating = (int?)comment.Review.Rating,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt
            });

        var rows = await reviews.Concat(comments)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Type).ThenByDescending(item => item.Id)
            .Skip(page * PageSize)
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);
        var entries = rows.Select(item => new ActivityItem(
            (ActivityType)item.Type,
            item.Id,
            item.ReviewId,
            item.UserName,
            item.DisplayName,
            item.TrackId,
            item.TrackTitle,
            item.Rating,
            item.Content,
            item.CreatedAt)).ToArray();

        return new ActivityPage(entries.Take(PageSize).ToArray(), entries.Length > PageSize);
    }
}
