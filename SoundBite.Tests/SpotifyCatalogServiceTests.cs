using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoundBite.Configuration;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class SpotifyCatalogServiceTests
{
    private sealed class SpotifyHandler : HttpMessageHandler
    {
        public int TokenRequests { get; private set; }
        public string? LastSearchPathAndQuery { get; private set; }
        public string? LastApiBearerToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "accounts.spotify.com")
            {
                TokenRequests++;
                Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Basic"));
                return Task.FromResult(JsonResponse(
                    """{"access_token":"test-access-token","token_type":"Bearer","expires_in":3600}"""));
            }

            LastSearchPathAndQuery = request.RequestUri?.PathAndQuery;
            LastApiBearerToken = request.Headers.Authorization?.Parameter;
            return Task.FromResult(JsonResponse(
                """
                {"tracks":{"items":[{"id":"track-1","name":"A track","album":{"id":"album-1","name":"An album"},"artists":[{"id":"artist-1","name":"Artist one"},{"id":"artist-2","name":"Artist two"}]}]}}
                """));
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://accounts.spotify.com/")
            };
    }

    [Test]
    public async Task SpotifySeedImportsRelatedRecordsIdempotentlyUsingCachedCredentials()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = new ServiceCollection()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
            await db.Database.MigrateAsync();

        var handler = new SpotifyHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var tokenProvider = new SpotifyAccessTokenProvider(
            new TestHttpClientFactory(handler),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SpotifyOptions { ClientId = "test-client", ClientSecret = "test-secret" }));
        var service = new SpotifyCatalogService(httpClient, tokenProvider);

        var results = await service.SearchTracksAsync("  A & B ");
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].SpotifyId, Is.EqualTo("track-1"));
        Assert.That(results[0].Title, Is.EqualTo("A track"));
        Assert.That(results[0].AlbumSpotifyId, Is.EqualTo("album-1"));
        Assert.That(results[0].AlbumTitle, Is.EqualTo("An album"));
        Assert.That(results[0].Artists, Is.EqualTo(new[]
        {
            new SpotifyArtistMatch("artist-1", "Artist one"),
            new SpotifyArtistMatch("artist-2", "Artist two")
        }));
        Assert.That(handler.LastSearchPathAndQuery, Does.Contain("q=A%20%26%20B")
            .And.Contain("type=track")
            .And.Contain("limit=10"));
        Assert.That(handler.LastApiBearerToken, Is.EqualTo("test-access-token"));

        var importer = new SpotifyTrackImporter(factory);
        var seeder = new SpotifyCatalogueSeeder(service, importer);
        var firstSeed = await seeder.SeedAsync(["The Beatles", "The Beatles"]);
        var secondSeed = await seeder.SeedAsync(["The Beatles"]);
        Assert.That(firstSeed, Is.EqualTo(new SpotifyCatalogueSeedResult(2, 1)));
        Assert.That(secondSeed, Is.EqualTo(new SpotifyCatalogueSeedResult(1, 1)));
        Assert.That(handler.TokenRequests, Is.EqualTo(1));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.That(await verify.Tracks.CountAsync(), Is.EqualTo(1));
        Assert.That(await verify.Albums.CountAsync(), Is.EqualTo(1));
        Assert.That(await verify.Artists.CountAsync(), Is.EqualTo(2));
        var storedTrack = await verify.Tracks.Include(track => track.Artists)
            .Include(track => track.Album).ThenInclude(album => album!.Artists)
            .SingleAsync();
        Assert.That(storedTrack.SpotifyId, Is.EqualTo("track-1"));
        Assert.That(storedTrack.Artists.Select(artist => artist.SpotifyId),
            Is.EquivalentTo(new[] { "artist-1", "artist-2" }));
        Assert.That(storedTrack.Album!.Artists, Has.Count.EqualTo(2));
    }

    [Test]
    public void MissingSpotifyCredentialsFailExplicitly()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new SpotifyAccessTokenProvider(
            new TestHttpClientFactory(new SpotifyHandler()),
            cache,
            Options.Create(new SpotifyOptions()));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetAccessTokenAsync());
    }
}
