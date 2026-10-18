namespace SoundBite.Models;

public sealed record SpotifyArtistMatch(string SpotifyId, string Name);

public sealed record SpotifyArtistDiscography(
    SpotifyArtistMatch Artist,
    IReadOnlyList<SpotifyAlbumCatalogue> Albums);

public sealed record SpotifyAlbumCatalogue(
    string SpotifyId,
    string Title,
    IReadOnlyList<SpotifyArtistMatch> Artists,
    IReadOnlyList<SpotifyTrackMatch> Tracks,
    string? CoverUrl = null);

public sealed record SpotifyTrackMatch(
    string SpotifyId,
    string Title,
    string AlbumSpotifyId,
    string AlbumTitle,
    IReadOnlyList<SpotifyArtistMatch> Artists,
    string? AlbumCoverUrl = null);
