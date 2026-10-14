using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class CatalogueServiceTests
{
    private SqliteConnection connection = null!;
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private IMusicCatalog catalog = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IMusicCatalog, CatalogueService>();
        provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        scope = provider.CreateScope();
        catalog = scope.ServiceProvider.GetRequiredService<IMusicCatalog>();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        scope.Dispose();
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }

    private ApplicationDbContext CreateContext() =>
        provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();

    private async Task SeedAsync()
    {
        // Insert out of display order, with ties to exercise the ID tie-breaker.
        var artists = new List<Artist>
        {
            new() { Id = 3, Name = "Zulu" },
            new() { Id = 2, Name = "Alpha" },
            new() { Id = 1, Name = "Alpha" }
        };
        var albums = new List<Album>
        {
            new() { Id = 3, Title = "Zulu", Artists = artists },
            new() { Id = 2, Title = "Alpha", Artists = artists },
            new() { Id = 1, Title = "Alpha", Artists = artists }
        };
        var tracks = new List<Track>
        {
            new() { Id = 3, Title = "Zulu", Artists = artists, Album = albums[2] },
            new() { Id = 2, Title = "Alpha", Artists = artists, Album = albums[2] },
            new() { Id = 1, Title = "Alpha", Artists = artists, Album = albums[2] },
            new() { Id = 4, Title = "Single", Artists = artists }
        };
        await using var db = CreateContext();
        db.AddRange(albums);
        db.AddRange(tracks);
        await db.SaveChangesAsync();
    }

    [Test]
    public async Task EmptyCatalogueReturnsEmptyLists()
    {
        Assert.That(await catalog.GetArtistsAsync(), Is.Empty);
        Assert.That(await catalog.GetAlbumsAsync(), Is.Empty);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(999)]
    public async Task MissingRecordsReturnNull(int id)
    {
        await SeedAsync();
        Assert.That(await catalog.GetArtistAsync(id), Is.Null);
        Assert.That(await catalog.GetAlbumAsync(id), Is.Null);
        Assert.That(await catalog.GetTrackAsync(id), Is.Null);
    }

    [Test]
    public async Task ListsAreOrderedAndDoNotLoadRelationships()
    {
        await SeedAsync();
        var artists = await catalog.GetArtistsAsync();
        var albums = await catalog.GetAlbumsAsync();
        Assert.That(artists.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(albums.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(artists.All(a => a.Albums.Count == 0 && a.Tracks.Count == 0), Is.True);
        Assert.That(albums.All(a => a.Artists.Count == 0 && a.Tracks.Count == 0), Is.True);
    }

    [Test]
    public async Task ArtistIncludesOrderedAlbumsTheirTracksAndCreditedSingles()
    {
        await SeedAsync();
        var artist = await catalog.GetArtistAsync(1);
        Assert.That(artist, Is.Not.Null);
        Assert.That(artist!.Albums.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(artist.Albums.First().Tracks.Select(t => t.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(artist.Tracks.Select(t => t.Id), Is.EqualTo(new[] { 1, 2, 4, 3 }));
    }

    [Test]
    public async Task AlbumIncludesOrderedCollaboratorsAndTracks()
    {
        await SeedAsync();
        var album = await catalog.GetAlbumAsync(1);
        Assert.That(album, Is.Not.Null);
        Assert.That(album!.Artists.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(album.Tracks.Select(t => t.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task TrackIncludesOrderedCollaboratorsAndAlbum()
    {
        await SeedAsync();
        var track = await catalog.GetTrackAsync(1);
        Assert.That(track, Is.Not.Null);
        Assert.That(track!.Artists.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(track.Album?.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task SingleHasNoAlbum()
    {
        await SeedAsync();
        var single = await catalog.GetTrackAsync(4);
        Assert.That(single, Is.Not.Null);
        Assert.That(single!.Album, Is.Null);
        Assert.That(single.AlbumId, Is.Null);
        Assert.That(single.Artists, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task RecordsWithoutRelationshipsReturnEmptyCollections()
    {
        await using (var db = CreateContext())
        {
            db.AddRange(new Artist { Id = 1, Name = "Artist" },
                new Album { Id = 1, Title = "Album" }, new Track { Id = 1, Title = "Track" });
            await db.SaveChangesAsync();
        }
        var artist = await catalog.GetArtistAsync(1);
        var album = await catalog.GetAlbumAsync(1);
        var track = await catalog.GetTrackAsync(1);
        Assert.That(artist!.Albums, Is.Empty);
        Assert.That(artist.Tracks, Is.Empty);
        Assert.That(album!.Artists, Is.Empty);
        Assert.That(album.Tracks, Is.Empty);
        Assert.That(track!.Artists, Is.Empty);
        Assert.That(track.Album, Is.Null);
    }

    [Test]
    public async Task ReturnedModelsCanBeEditedWithoutChangingSubsequentReads()
    {
        await SeedAsync();
        var artist = await catalog.GetArtistAsync(1);
        artist!.Name = "Edited";
        artist.Albums.Clear();
        var reloaded = await catalog.GetArtistAsync(1);
        Assert.That(reloaded!.Name, Is.EqualTo("Alpha"));
        Assert.That(reloaded.Albums, Has.Count.EqualTo(3));
    }

    [Test]
    public void AllOperationsHonorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        Assert.That(async () => await catalog.GetArtistsAsync(token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetArtistAsync(1, token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetAlbumsAsync(token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetAlbumAsync(1, token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetTrackAsync(1, token), Throws.InstanceOf<OperationCanceledException>());
    }
}
