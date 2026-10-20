using SoundBite.Models;

namespace SoundBite.Interfaces;

public interface ISpotifyCatalog
{
    /// <summary>Finds Spotify artists that match the supplied search text.</summary>
    Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the albums and tracks for a Spotify artist.</summary>
    Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
        SpotifyArtistMatch artist,
        CancellationToken cancellationToken = default);

    /// <summary>Finds Spotify tracks that match the supplied search text.</summary>
    Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns cover URLs keyed by Spotify album ID.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAlbumCoverUrlsAsync(
        IReadOnlyList<string> spotifyAlbumIds,
        CancellationToken cancellationToken = default);
}
