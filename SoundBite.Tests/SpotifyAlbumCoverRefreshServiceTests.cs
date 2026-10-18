using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class SpotifyAlbumCoverRefreshServiceTests
{
    private sealed class SpotifyCatalogStub : ISpotifyCatalog
    {
        public List<string> RequestedIds { get; } = [];

        public Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
            SpotifyArtistMatch artist,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, string>> GetAlbumCoverUrlsAsync(
            IReadOnlyList<string> spotifyAlbumIds,
            CancellationToken cancellationToken = default)
        {
            RequestedIds.AddRange(spotifyAlbumIds);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>
                {
                    ["spotify-covered"] = "https://i.scdn.co/image/cached-cover"
                });
        }
    }

    [Test]
    public async Task RefreshAddsMissingSpotifyCoversOnceAndLeavesExistingAlbumsUnchanged()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestFactory(options);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            db.Albums.AddRange(
                new Album { Title = "Covered by Spotify", SpotifyId = "spotify-covered" },
                new Album { Title = "Unavailable in Spotify", SpotifyId = "spotify-missing" },
                new Album
                {
                    Title = "Already cached",
                    SpotifyId = "spotify-existing",
                    CoverUrl = "https://i.scdn.co/image/original-cover"
                },
                new Album { Title = "No Spotify ID" });
            await db.SaveChangesAsync();
        }

        var spotify = new SpotifyCatalogStub();
        var service = new SpotifyAlbumCoverRefreshService(factory, spotify);
        var first = await service.RefreshMissingCoversAsync();
        var second = await service.RefreshMissingCoversAsync();

        Assert.That(first, Is.EqualTo(new SpotifyAlbumCoverRefreshResult(2, 1, 1)));
        Assert.That(second, Is.EqualTo(new SpotifyAlbumCoverRefreshResult(1, 0, 1)));
        Assert.That(spotify.RequestedIds, Is.EqualTo(new[] { "spotify-covered", "spotify-missing", "spotify-missing" }));
        await using var verify = factory.CreateDbContext();
        Assert.That(await verify.Albums.OrderBy(album => album.Title)
            .Select(album => album.CoverUrl).ToListAsync(), Is.EqualTo(new string?[]
            {
                "https://i.scdn.co/image/original-cover",
                "https://i.scdn.co/image/cached-cover",
                null,
                null
            }));
    }

    private sealed class TestFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateDbContext());
    }
}
