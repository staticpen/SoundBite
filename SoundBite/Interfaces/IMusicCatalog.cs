using SoundBite.Models;

namespace SoundBite.Interfaces;

/// <summary>
/// Read-only access to the local catalogue. Lists contain no related entities;
/// detail methods load the relationships described below and return null for missing IDs.
/// Collections are ordered by name/title, then ID. Results are detached from EF tracking.
/// </summary>
public interface IMusicCatalog
{
    /// <summary>
    /// Matches artist names and album/track titles by ordinal case-insensitive substring.
    /// Trims the query; null or blank queries return empty results. Relationships are not loaded.
    /// </summary>
    Task<CatalogueSearchResults> SearchAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>Returns all artists in the catalogue.</summary>
    Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the artist's albums (including their tracks) and credited tracks.</summary>
    Task<Artist?> GetArtistAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns all albums in the catalogue.</summary>
    Task<IReadOnlyList<Album>> GetAlbumsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the album's artists and tracks.</summary>
    Task<Album?> GetAlbumAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Loads the track's artists and optional album.</summary>
    Task<Track?> GetTrackAsync(int id, CancellationToken cancellationToken = default);
}
