using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public class CatalogueService(IDbContextFactory<ApplicationDbContext> contextFactory) : IMusicCatalog
{
    public async Task<CatalogueSearchResults> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var term = query?.Trim();
        if (string.IsNullOrEmpty(term))
            return new CatalogueSearchResults();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // For this small local catalogue, match in .NET so SQLite's ASCII-only
        // case folding does not prevent case-insensitive matches in accented names.
        var artists = await db.Artists.AsNoTracking().ToListAsync(cancellationToken);
        var albums = await db.Albums.AsNoTracking().ToListAsync(cancellationToken);
        var tracks = await db.Tracks.AsNoTracking().ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new CatalogueSearchResults
        {
            Artists = artists.Where(a => a.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList(),
            Albums = albums.Where(a => a.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList(),
            Tracks = tracks.Where(t => t.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id).ToList()
        };
    }

    // Each call owns its context, including when Blazor components load concurrently.
    public async Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Artists.AsNoTracking()
            .OrderBy(artist => artist.Name).ThenBy(artist => artist.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Artist?> GetArtistAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Split collection queries avoid multiplying album and track rows in a single join.
        return await db.Artists.AsNoTracking().AsSplitQuery()
            .Include(artist => artist.Albums.OrderBy(album => album.Title).ThenBy(album => album.Id))
                .ThenInclude(album => album.Tracks.OrderBy(track => track.Title).ThenBy(track => track.Id))
            .Include(artist => artist.Tracks.OrderBy(track => track.Title).ThenBy(track => track.Id))
                .ThenInclude(track => track.Album)
            .SingleOrDefaultAsync(artist => artist.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Album>> GetAlbumsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Albums.AsNoTracking()
            .OrderBy(album => album.Title).ThenBy(album => album.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Album?> GetAlbumAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Albums.AsNoTracking().AsSplitQuery()
            .Include(album => album.Artists.OrderBy(artist => artist.Name).ThenBy(artist => artist.Id))
            .Include(album => album.Tracks.OrderBy(track => track.Title).ThenBy(track => track.Id))
            .SingleOrDefaultAsync(album => album.Id == id, cancellationToken);
    }

    public async Task<Track?> GetTrackAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tracks.AsNoTracking()
            .Include(track => track.Artists.OrderBy(artist => artist.Name).ThenBy(artist => artist.Id))
            .Include(track => track.Album)
            .SingleOrDefaultAsync(track => track.Id == id, cancellationToken);
    }
}
