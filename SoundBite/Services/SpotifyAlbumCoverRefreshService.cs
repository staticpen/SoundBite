using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Interfaces;

namespace SoundBite.Services;

public sealed record SpotifyAlbumCoverRefreshResult(int AlbumsChecked, int CoversAdded, int AlbumsWithoutCover);

public sealed class SpotifyAlbumCoverRefreshService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ISpotifyCatalog spotifyCatalog)
{
    public async Task<SpotifyAlbumCoverRefreshResult> RefreshMissingCoversAsync(
        CancellationToken cancellationToken = default)
    {
        List<(int Id, string SpotifyId)> albums;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var rows = await db.Albums.AsNoTracking()
                .Where(album =>
                    string.IsNullOrWhiteSpace(album.CoverUrl) &&
                    album.SpotifyId != null &&
                    album.SpotifyId != string.Empty)
                .OrderBy(album => album.Id)
                .Select(album => new { album.Id, SpotifyId = album.SpotifyId! })
                .ToListAsync(cancellationToken);
            albums = rows.Select(album => (album.Id, album.SpotifyId)).ToList();
        }

        if (albums.Count == 0)
            return new SpotifyAlbumCoverRefreshResult(0, 0, 0);

        var covers = await spotifyCatalog.GetAlbumCoverUrlsAsync(
            albums.Select(album => album.SpotifyId).ToArray(),
            cancellationToken);

        await using var updateDb = await contextFactory.CreateDbContextAsync(cancellationToken);
        var albumIds = albums.Select(candidate => candidate.Id).ToArray();
        var spotifyIdsByAlbumId = albums.ToDictionary(candidate => candidate.Id, candidate => candidate.SpotifyId);
        var refreshedAlbums = await updateDb.Albums
            .Where(album => albumIds.Contains(album.Id))
            .ToListAsync(cancellationToken);
        var coversAdded = 0;
        foreach (var album in refreshedAlbums)
        {
            var spotifyId = spotifyIdsByAlbumId[album.Id];
            if (covers.TryGetValue(spotifyId, out var coverUrl) &&
                string.IsNullOrWhiteSpace(album.CoverUrl))
            {
                album.CoverUrl = coverUrl;
                coversAdded++;
            }
        }

        await updateDb.SaveChangesAsync(cancellationToken);
        return new SpotifyAlbumCoverRefreshResult(
            albums.Count,
            coversAdded,
            albums.Count - coversAdded);
    }
}
