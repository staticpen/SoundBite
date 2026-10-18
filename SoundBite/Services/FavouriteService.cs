using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class FavouriteService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider)
{
    public static bool IsStorageNotMigrated(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: 1 } sqliteException &&
                sqliteException.Message.Contains("no such table: FavouriteTracks", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public async Task<IReadOnlyList<FavouriteTrack>> GetMineAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync();
        return await GetForUserAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<FavouriteTrack>> GetForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FavouriteTracks.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Include(item => item.Track)
                .ThenInclude(track => track!.Artists.OrderBy(artist => artist.Name))
            .OrderBy(item => item.AddedAt)
            .ThenBy(item => item.TrackId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ContainsAsync(int trackId, CancellationToken cancellationToken = default)
    {
        ValidateTrackId(trackId);
        var userId = await GetCurrentUserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FavouriteTracks.AnyAsync(
            item => item.UserId == userId && item.TrackId == trackId,
            cancellationToken);
    }

    public async Task<IReadOnlySet<int>> GetMineTrackIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var trackIds = await db.FavouriteTracks.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.TrackId)
            .ToListAsync(cancellationToken);
        return trackIds.ToHashSet();
    }

    public async Task AddAsync(int trackId, CancellationToken cancellationToken = default)
    {
        ValidateTrackId(trackId);
        var userId = await GetCurrentUserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Tracks.AnyAsync(track => track.Id == trackId, cancellationToken))
            throw new InvalidOperationException($"Track {trackId} was not found.");

        db.FavouriteTracks.Add(new FavouriteTrack
        {
            UserId = userId,
            TrackId = trackId,
            AddedAt = DateTime.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            if (await db.FavouriteTracks.AnyAsync(
                    item => item.UserId == userId && item.TrackId == trackId,
                    cancellationToken))
                throw new InvalidOperationException("This track is already in your favourites.", exception);
            throw;
        }
    }

    public async Task RemoveAsync(int trackId, CancellationToken cancellationToken = default)
    {
        ValidateTrackId(trackId);
        var userId = await GetCurrentUserIdAsync();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.FavouriteTracks.SingleOrDefaultAsync(
            entry => entry.UserId == userId && entry.TrackId == trackId,
            cancellationToken);
        if (item is null)
            throw new InvalidOperationException("Track was not found in your favourites.");

        db.FavouriteTracks.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var userId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Sign in to manage your favourites.");
        return userId;
    }

    private static void ValidateTrackId(int trackId)
    {
        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), "Track ID must be greater than zero.");
    }
}
