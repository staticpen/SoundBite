using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public class CatalogueService(IDbContextFactory<ApplicationDbContext> contextFactory) : IMusicCatalog
{
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
