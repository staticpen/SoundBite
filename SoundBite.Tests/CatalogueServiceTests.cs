using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    private readonly QueryCounter queryCounter = new();

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection).AddInterceptors(queryCounter));
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

    [TestCase("lPh")]
    [TestCase(" ALPHA ")]
    public async Task SearchMatchesPartialNamesAndTitlesIgnoringCaseAndTrimsQuery(string query)
    {
        await SeedAsync();
        var results = await catalog.SearchAsync(query);
        Assert.That(results.Artists.Select(a => a.Id), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(results.Albums.Select(a => a.Id), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(results.Tracks.Select(t => t.Id), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(results.TotalCount, Is.EqualTo(6));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not found")]
    public async Task BlankAndUnmatchedSearchesReturnEmptyResults(string? query)
    {
        await SeedAsync();
        var results = await catalog.SearchAsync(query);
        Assert.That(results.Artists, Is.Empty);
        Assert.That(results.Albums, Is.Empty);
        Assert.That(results.Tracks, Is.Empty);
    }

    [Test]
    public async Task SearchOfEmptyCatalogueReturnsNoResults()
    {
        Assert.That((await catalog.SearchAsync("music")).TotalCount, Is.Zero);
    }

    [Test]
    public async Task SearchFindsSinglesWithoutReturningTheirArtistsOrAlbums()
    {
        await SeedAsync();
        var results = await catalog.SearchAsync("SINGLE");
        Assert.That(results.Artists, Is.Empty);
        Assert.That(results.Albums, Is.Empty);
        Assert.That(results.Tracks.Select(t => t.Id), Is.EqualTo(new[] { 4 }));
    }

    [Test]
    public async Task SearchHandlesAccentedCaseAndLiteralWildcardCharacters()
    {
        await using (var db = CreateContext())
        {
            db.AddRange(new Artist { Name = "Écho 100%_" },
                new Album { Title = "Écho 100%_" }, new Track { Title = "Écho 100%_" },
                new Artist { Name = "Other" });
            await db.SaveChangesAsync();
        }
        Assert.That((await catalog.SearchAsync("éCHO")).TotalCount, Is.EqualTo(3));
        Assert.That((await catalog.SearchAsync("%_")).TotalCount, Is.EqualTo(3));
    }

    [TestCase(null, 0)]
    [TestCase(" \t\n", 0)]
    [TestCase("Alpha", 3)]
    [TestCase("not found", 3)]
    public async Task SearchOnlyQueriesEachEntitySetOnce(string? query, int expectedQueries)
    {
        await SeedAsync();
        queryCounter.Count = 0;
        var results = await catalog.SearchAsync(query);
        _ = results.TotalCount;
        Assert.That(queryCounter.Count, Is.EqualTo(expectedQueries));
    }

    [TestCase("'")]
    [TestCase("\"")]
    [TestCase("<script>")]
    [TestCase("%")]
    [TestCase("_")]
    [TestCase("[a]")]
    [TestCase("🎵")]
    public async Task SearchTreatsSpecialCharactersAsLiteralText(string term)
    {
        await using (var db = CreateContext())
        {
            db.AddRange(new Artist { Name = "Before " + term + " After" },
                new Album { Title = "Before " + term + " After" },
                new Track { Title = "Before " + term + " After" },
                new Artist { Name = "Other" }, new Album { Title = "Other" },
                new Track { Title = "Other" });
            await db.SaveChangesAsync();
        }
        Assert.That((await catalog.SearchAsync(term)).TotalCount, Is.EqualTo(3));
    }

    [Test]
    public async Task SearchOrdersResultsAndReturnsDetachedModelsWithoutRelationships()
    {
        await SeedAsync();
        var results = await catalog.SearchAsync("l");
        Assert.That(results.Artists.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(results.Albums.Select(a => a.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(results.Tracks.Select(t => t.Id), Is.EqualTo(new[] { 1, 2, 4, 3 }));
        Assert.That(results.Artists.All(a => a.Albums.Count == 0 && a.Tracks.Count == 0), Is.True);
        Assert.That(results.Albums.All(a => a.Artists.Count == 0 && a.Tracks.Count == 0), Is.True);
        Assert.That(results.Tracks.All(t => t.Artists.Count == 0 && t.Album is null), Is.True);
        results.Artists[0].Name = "Changed";
        results.Albums[0].Title = "Changed";
        results.Tracks[0].Title = "Changed";
        Assert.That((await catalog.SearchAsync("Alpha")).TotalCount, Is.EqualTo(6));
    }

    [Test]
    public void AllOperationsHonorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        Assert.That(async () => await catalog.SearchAsync("Alpha", token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.SearchAsync(" ", token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetArtistsAsync(token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetArtistAsync(1, token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetAlbumsAsync(token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetAlbumAsync(1, token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await catalog.GetTrackAsync(1, token), Throws.InstanceOf<OperationCanceledException>());
    }
    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
