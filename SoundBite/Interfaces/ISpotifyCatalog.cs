using SoundBite.Models;

namespace SoundBite.Interfaces;

public interface ISpotifyCatalog
{
    Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default);
}
