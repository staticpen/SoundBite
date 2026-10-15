using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public class PlaylistService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task<IReadOnlyList<Playlist>> GetPublicPlaylistsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Playlists.AsNoTracking()
            .Where(playlist => playlist.IsPublic)
            .Include(playlist => playlist.Owner)
            .Include(playlist => playlist.Items)
            .OrderByDescending(playlist => playlist.UpdatedAt)
            .ThenBy(playlist => playlist.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Playlist?> GetPublicPlaylistAsync(
        int playlistId,
        CancellationToken cancellationToken = default)
    {
        if (playlistId <= 0)
            return null;

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Playlists.AsNoTracking()
            .Where(playlist => playlist.Id == playlistId && playlist.IsPublic)
            .Include(playlist => playlist.Owner)
            .Include(playlist => playlist.Items.OrderBy(item => item.Position))
                .ThenInclude(item => item.Track)
                    .ThenInclude(track => track!.Artists.OrderBy(artist => artist.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Playlist> CreateAsync(
        string ownerId,
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerId(ownerId);
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(user => user.Id == ownerId, cancellationToken))
            throw new InvalidOperationException("Playlist owner was not found.");

        var playlist = new Playlist
        {
            OwnerId = ownerId,
            Name = normalizedName,
            Description = normalizedDescription,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(cancellationToken);
        return playlist;
    }

    public async Task<Playlist> UpdateAsync(
        string ownerId,
        int playlistId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerId(ownerId);
        ValidatePlaylistId(playlistId);
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var playlist = await FindOwnedPlaylistAsync(db, ownerId, playlistId, cancellationToken);
        playlist.Name = normalizedName;
        playlist.Description = normalizedDescription;
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return playlist;
    }

    public async Task DeleteAsync(
        string ownerId,
        int playlistId,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerId(ownerId);
        ValidatePlaylistId(playlistId);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var playlist = await FindOwnedPlaylistAsync(db, ownerId, playlistId, cancellationToken);
        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTrackAsync(
        string ownerId,
        int playlistId,
        int trackId,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerId(ownerId);
        ValidatePlaylistId(playlistId);
        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), "Track ID must be greater than zero.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var playlist = await FindOwnedPlaylistAsync(db, ownerId, playlistId, cancellationToken);
        if (!await db.Tracks.AnyAsync(track => track.Id == trackId, cancellationToken))
            throw new InvalidOperationException($"Track {trackId} was not found.");

        if (await db.PlaylistItems.AnyAsync(
                item => item.PlaylistId == playlistId && item.TrackId == trackId,
                cancellationToken))
            throw new InvalidOperationException("This track is already in the playlist.");

        var lastPosition = await db.PlaylistItems
            .Where(item => item.PlaylistId == playlistId)
            .Select(item => (int?)item.Position)
            .MaxAsync(cancellationToken);
        db.PlaylistItems.Add(new PlaylistItem
        {
            PlaylistId = playlistId,
            TrackId = trackId,
            Position = (lastPosition ?? -1) + 1
        });
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveTrackAsync(
        string ownerId,
        int playlistId,
        int trackId,
        CancellationToken cancellationToken = default)
    {
        ValidateOwnerId(ownerId);
        ValidatePlaylistId(playlistId);
        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), "Track ID must be greater than zero.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var playlist = await FindOwnedPlaylistAsync(db, ownerId, playlistId, cancellationToken);
        var item = await db.PlaylistItems.SingleOrDefaultAsync(
            existing => existing.PlaylistId == playlistId && existing.TrackId == trackId,
            cancellationToken);
        if (item is null)
            throw new InvalidOperationException("Track was not found in the playlist.");

        db.PlaylistItems.Remove(item);
        playlist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Playlist> FindOwnedPlaylistAsync(
        ApplicationDbContext db,
        string ownerId,
        int playlistId,
        CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.SingleOrDefaultAsync(
            existing => existing.Id == playlistId && existing.OwnerId == ownerId,
            cancellationToken);
        return playlist ?? throw new InvalidOperationException("Playlist was not found for the current user.");
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Playlist name is required.", nameof(name));

        var trimmed = name.Trim();
        if (trimmed.Length > 100)
            throw new ArgumentException("Playlist name cannot exceed 100 characters.", nameof(name));
        return trimmed;
    }

    private static string NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length > 1000)
            throw new ArgumentException("Playlist description cannot exceed 1000 characters.", nameof(description));
        return trimmed;
    }

    private static void ValidateOwnerId(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Playlist owner is required.", nameof(ownerId));
    }

    private static void ValidatePlaylistId(int playlistId)
    {
        if (playlistId <= 0)
            throw new ArgumentOutOfRangeException(nameof(playlistId), "Playlist ID must be greater than zero.");
    }
}
