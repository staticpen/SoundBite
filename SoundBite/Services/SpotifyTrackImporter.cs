using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed record SpotifyArtistImportResult(int AlbumsImported, int TracksImported);

public sealed class SpotifyTrackImporter(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    // Adds or updates one Spotify track and returns its local database ID.
    public async Task<int> ImportAsync(
        SpotifyTrackMatch match,
        CancellationToken cancellationToken = default)
    {
        ValidateTrack(match);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var track = await UpsertTrackAsync(db, match, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return track.Id;
    }

    // Imports an artist's albums and tracks as one database transaction.
    public async Task<SpotifyArtistImportResult> ImportArtistAsync(
        SpotifyArtistDiscography discography,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discography);
        ValidateArtist(discography.Artist);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Keep the discography atomic: a failure partway through should not leave an incomplete import.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var artist = await GetOrAddArtistAsync(db, discography.Artist, cancellationToken);
        var importedTracks = 0;
        foreach (var albumMatch in discography.Albums)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(albumMatch.SpotifyId) ||
                string.IsNullOrWhiteSpace(albumMatch.Title))
            {
                throw new ArgumentException("The Spotify album metadata is incomplete.", nameof(discography));
            }

            var album = db.Albums.Local.SingleOrDefault(item => item.SpotifyId == albumMatch.SpotifyId)
                ?? await db.Albums
                    .Include(item => item.Artists)
                    .SingleOrDefaultAsync(item => item.SpotifyId == albumMatch.SpotifyId, cancellationToken);
            if (album is null)
            {
                album = new Album { SpotifyId = albumMatch.SpotifyId };
                db.Albums.Add(album);
            }

            album.Title = albumMatch.Title;
            CacheAlbumCover(album, albumMatch.CoverUrl);
            // Some Spotify album responses omit artists, so use the imported artist as a fallback.
            var albumArtists = albumMatch.Artists.Count == 0
                ? [discography.Artist]
                : albumMatch.Artists.DistinctBy(item => item.SpotifyId);
            foreach (var albumArtist in albumArtists)
                AddAlbumArtist(album, await GetOrAddArtistAsync(db, albumArtist, cancellationToken));
            AddAlbumArtist(album, artist);

            foreach (var trackMatch in albumMatch.Tracks)
            {
                ValidateTrack(trackMatch);
                if (trackMatch.AlbumSpotifyId != albumMatch.SpotifyId)
                    throw new ArgumentException("Spotify track album does not match its catalogue album.", nameof(discography));
                await UpsertTrackAsync(db, trackMatch, cancellationToken);
                // Count processed tracks, including records updated rather than newly created.
                importedTracks++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SpotifyArtistImportResult(discography.Albums.Count, importedTracks);
    }

    // Adds a track or refreshes its album and artist details from Spotify.
    private static async Task<Track> UpsertTrackAsync(
        ApplicationDbContext db,
        SpotifyTrackMatch match,
        CancellationToken cancellationToken)
    {
        var artistMatches = match.Artists.DistinctBy(item => item.SpotifyId).ToArray();
        var artists = new List<Artist>(artistMatches.Length);
        foreach (var artistMatch in artistMatches)
            artists.Add(await GetOrAddArtistAsync(db, artistMatch, cancellationToken));

        var album = db.Albums.Local.SingleOrDefault(item => item.SpotifyId == match.AlbumSpotifyId)
            ?? await db.Albums
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
        CacheAlbumCover(album, match.AlbumCoverUrl);

        foreach (var artist in artists)
            AddAlbumArtist(album, artist);

        // Spotify IDs are stable identifiers, unlike titles which can be shared or updated.
        var track = db.Tracks.Local.SingleOrDefault(item => item.SpotifyId == match.SpotifyId)
            ?? await db.Tracks
                .Include(item => item.Artists)
                .SingleOrDefaultAsync(item => item.SpotifyId == match.SpotifyId, cancellationToken);
        if (track is null)
        {
            track = new Track { SpotifyId = match.SpotifyId };
            db.Tracks.Add(track);
        }

        track.Title = match.Title;
        track.Album = album;
        // Rebuild this relationship from the current Spotify response so stale artists are removed.
        track.Artists.Clear();
        foreach (var artist in artists)
            track.Artists.Add(artist);

        return track;
    }

    // Finds an artist by Spotify ID or adds it if it is new.
    private static async Task<Artist> GetOrAddArtistAsync(
        ApplicationDbContext db,
        SpotifyArtistMatch match,
        CancellationToken cancellationToken)
    {
        ValidateArtist(match);
        var artist = db.Artists.Local.SingleOrDefault(item => item.SpotifyId == match.SpotifyId)
            ?? await db.Artists.SingleOrDefaultAsync(
                item => item.SpotifyId == match.SpotifyId,
                cancellationToken);
        if (artist is null)
        {
            artist = new Artist { SpotifyId = match.SpotifyId };
            db.Artists.Add(artist);
        }

        artist.Name = match.Name;
        return artist;
    }

    // Adds an imported artist to an album's artist relationship.
    private static void AddAlbumArtist(Album album, Artist artist)
    {
        // An artist may already be linked through both the album and track import paths.
        if (!album.Artists.Any(existing => existing.SpotifyId == artist.SpotifyId))
            album.Artists.Add(artist);
    }

    // Applies Spotify cover data while keeping the album's saved image intact.
    private static void CacheAlbumCover(Album album, string? coverUrl)
    {
        // Keep an existing cached image if Spotify has no URL in a later response.
        if (string.IsNullOrWhiteSpace(album.CoverUrl) && !string.IsNullOrWhiteSpace(coverUrl))
            album.CoverUrl = coverUrl;
    }

    // Makes sure a Spotify track result has the fields needed for import.
    private static void ValidateTrack(SpotifyTrackMatch match)
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
    }

    // Makes sure a Spotify artist result has the fields needed for import.
    private static void ValidateArtist(SpotifyArtistMatch artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (string.IsNullOrWhiteSpace(artist.SpotifyId) || string.IsNullOrWhiteSpace(artist.Name))
            throw new ArgumentException("The Spotify artist metadata is incomplete.", nameof(artist));
    }
}
