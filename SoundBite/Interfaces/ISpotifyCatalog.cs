using SoundBite.Models;

namespace SoundBite.Interfaces;

public interface ISpotifyCatalog
{
    Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
        SpotifyArtistMatch artist,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetAlbumCoverUrlsAsync(
        IReadOnlyList<string> spotifyAlbumIds,
        CancellationToken cancellationToken = default);
}
