using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class SpotifyTrackImporter(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task<int> ImportAsync(
        SpotifyTrackMatch match,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (string.IsNullOrWhiteSpace(match.SpotifyId) ||
            string.IsNullOrWhiteSpace(match.Title) ||
            string.IsNullOrWhiteSpace(match.AlbumSpotifyId) ||
            string.IsNullOrWhiteSpace(match.AlbumTitle) ||
            match.Artists.Count == 0 ||
            match.Artists.Any(artist =>
                string.IsNullOrWhiteSpace(artist.SpotifyId) ||
                string.IsNullOrWhiteSpace(artist.Name)))
        {
            throw new ArgumentException("The Spotify track metadata is incomplete.", nameof(match));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var artists = new List<Artist>(match.Artists.Count);
        foreach (var artistMatch in match.Artists)
        {
            var artist = await db.Artists.SingleOrDefaultAsync(
                item => item.SpotifyId == artistMatch.SpotifyId,
                cancellationToken);
            if (artist is null)
            {
                artist = new Artist
                {
                    Name = artistMatch.Name,
                    SpotifyId = artistMatch.SpotifyId
                };
                db.Artists.Add(artist);
            }
            else
            {
                artist.Name = artistMatch.Name;
            }

            artists.Add(artist);
        }

        var album = await db.Albums
            .Include(item => item.Artists)
            .SingleOrDefaultAsync(item => item.SpotifyId == match.AlbumSpotifyId, cancellationToken);
        if (album is null)
        {
            album = new Album
            {
                Title = match.AlbumTitle,
                SpotifyId = match.AlbumSpotifyId
            };
            db.Albums.Add(album);
        }
        else
        {
            album.Title = match.AlbumTitle;
        }

        foreach (var artist in artists)
            album.Artists.Add(artist);

        var track = await db.Tracks
            .Include(item => item.Artists)
            .SingleOrDefaultAsync(item => item.SpotifyId == match.SpotifyId, cancellationToken);
        if (track is null)
        {
            track = new Track { SpotifyId = match.SpotifyId };
            db.Tracks.Add(track);
        }

        track.Title = match.Title;
        track.Album = album;
        track.Artists.Clear();
        foreach (var artist in artists)
            track.Artists.Add(artist);

        await db.SaveChangesAsync(cancellationToken);
        return track.Id;
    }
}
