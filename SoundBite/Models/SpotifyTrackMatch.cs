namespace SoundBite.Models;

public sealed record SpotifyArtistMatch(string SpotifyId, string Name);

public sealed record SpotifyTrackMatch(
    string SpotifyId,
    string Title,
    string AlbumSpotifyId,
    string AlbumTitle,
    IReadOnlyList<SpotifyArtistMatch> Artists);
