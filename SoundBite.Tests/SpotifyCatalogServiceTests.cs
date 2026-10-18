using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoundBite.Configuration;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class SpotifyCatalogServiceTests
{
    private sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class SpotifyCatalogMustNotBeCalled : ISpotifyCatalog
    {
        public Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Spotify must not be called for an anonymous import.");

        public Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
            SpotifyArtistMatch artist,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Spotify must not be called for an anonymous import.");

        public Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Spotify must not be called for an anonymous import.");

        public Task<IReadOnlyDictionary<string, string>> GetAlbumCoverUrlsAsync(
            IReadOnlyList<string> spotifyAlbumIds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Spotify must not be called for an anonymous import.");
    }

    private sealed class SpotifyHandler : HttpMessageHandler
    {
        public int TokenRequests { get; private set; }
        public List<string> ApiRequests { get; } = [];
        public string? LastSearchPathAndQuery { get; private set; }
        public string? LastApiBearerToken { get; private set; }
        public HttpStatusCode? ApiFailureStatusCode { get; init; }
        public string ApiFailureBody { get; init; } = "";
        public bool BulkAlbumLookupForbidden { get; init; }

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

            ApiRequests.Add(request.RequestUri!.PathAndQuery);
            LastSearchPathAndQuery = request.RequestUri?.PathAndQuery;
            LastApiBearerToken = request.Headers.Authorization?.Parameter;
            if (ApiFailureStatusCode is { } failureStatusCode)
            {
                return Task.FromResult(new HttpResponseMessage(failureStatusCode)
                {
                    Content = new StringContent(ApiFailureBody, Encoding.UTF8, "application/json")
                });
            }

            if (LastSearchPathAndQuery?.StartsWith("/v1/albums?ids=", StringComparison.Ordinal) == true)
            {
                if (BulkAlbumLookupForbidden)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent(
                            """{"error":{"status":403,"message":"Batch lookup forbidden."}}""",
                            Encoding.UTF8,
                            "application/json")
                    });
                }

                return Task.FromResult(JsonResponse(
                    """
                    {"albums":[
                      {"id":"album-1","images":[{"url":"https://i.scdn.co/image/cover-small","height":64,"width":64},{"url":"https://i.scdn.co/image/cover-large","height":640,"width":640}]},
                      {"id":"album-unsafe","images":[{"url":"https://images.example.invalid/cover.jpg","height":640,"width":640}]},
                      null
                    ]}
                    """));
            }

            if (LastSearchPathAndQuery == "/v1/albums/album-1")
            {
                return Task.FromResult(JsonResponse(
                    """{"id":"album-1","images":[{"url":"https://i.scdn.co/image/individual-cover","height":640,"width":640}]}"""));
            }

            if (LastSearchPathAndQuery?.Contains("type=artist", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(
                    """{"artists":{"items":[{"id":"artist-1","name":"Artist one"}],"next":null}}"""));
            }

            if (LastSearchPathAndQuery?.StartsWith("/v1/artists/artist-1/albums", StringComparison.Ordinal) == true)
            {
                if (LastSearchPathAndQuery.Contains("offset=10", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(
                        """{"items":[{"id":"album-2","name":"Second album","artists":[{"id":"artist-1","name":"Artist one"}],"images":[{"url":"https://i.scdn.co/image/album-two-cover","height":640,"width":640}]}],"next":null}"""));
                }

                return Task.FromResult(JsonResponse(
                    """{"items":[{"id":"album-1","name":"First album","artists":[{"id":"artist-1","name":"Artist one"}],"images":[{"url":"https://i.scdn.co/image/album-one-small","height":64,"width":64},{"url":"https://i.scdn.co/image/album-one-large","height":640,"width":640}]}],"next":"https://api.spotify.com/v1/artists/artist-1/albums?include_groups=album&limit=10&offset=10"}"""));
            }

            if (LastSearchPathAndQuery?.StartsWith("/v1/albums/album-1/tracks", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(
                    """{"items":[{"id":"discography-track-1","name":"First track","artists":[{"id":"artist-1","name":"Artist one"},{"id":"artist-2","name":"Artist two"}]}],"next":null}"""));
            }

            if (LastSearchPathAndQuery?.StartsWith("/v1/albums/album-2/tracks", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(JsonResponse(
                    """{"items":[{"id":"discography-track-2","name":"Second track","artists":[{"id":"artist-1","name":"Artist one"}]}],"next":null}"""));
            }

            return Task.FromResult(JsonResponse(
                """
                {"tracks":{"items":[{"id":"track-1","name":"A track","album":{"id":"album-1","name":"An album","images":[{"url":"https://i.scdn.co/image/search-cover-small","height":64,"width":64},{"url":"https://i.scdn.co/image/search-cover-large","height":640,"width":640}]},"artists":[{"id":"artist-1","name":"Artist one"},{"id":"artist-2","name":"Artist two"}]}]}}
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
        Assert.That(results[0].AlbumCoverUrl, Is.EqualTo("https://i.scdn.co/image/search-cover-large"));
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
        Assert.That(await verify.Albums.Select(album => album.CoverUrl).SingleAsync(),
            Is.EqualTo("https://i.scdn.co/image/search-cover-large"));
        await importer.ImportAsync(results[0] with { AlbumCoverUrl = "https://i.scdn.co/image/replacement-cover" });
        Assert.That(await verify.Albums.Select(album => album.CoverUrl).SingleAsync(),
            Is.EqualTo("https://i.scdn.co/image/search-cover-large"));
        var storedTrack = await verify.Tracks.Include(track => track.Artists)
            .Include(track => track.Album).ThenInclude(album => album!.Artists)
            .SingleAsync();
        Assert.That(storedTrack.SpotifyId, Is.EqualTo("track-1"));
        Assert.That(storedTrack.Artists.Select(artist => artist.SpotifyId),
            Is.EquivalentTo(new[] { "artist-1", "artist-2" }));
        Assert.That(storedTrack.Album!.Artists, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SpotifyArtistDiscographyImportsAllAlbumsTracksAndArtistsIdempotently()
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
        var spotify = new SpotifyCatalogService(httpClient, tokenProvider);

        var artists = await spotify.SearchArtistsAsync("Artist one");
        Assert.That(artists, Is.EqualTo(new[] { new SpotifyArtistMatch("artist-1", "Artist one") }));
        var discography = await spotify.GetArtistDiscographyAsync(artists[0]);
        Assert.That(discography.Albums, Has.Count.EqualTo(2));
        Assert.That(discography.Albums[0].CoverUrl, Is.EqualTo("https://i.scdn.co/image/album-one-large"));
        Assert.That(discography.Albums[1].CoverUrl, Is.EqualTo("https://i.scdn.co/image/album-two-cover"));
        Assert.That(discography.Albums.SelectMany(album => album.Tracks).ToArray(), Has.Length.EqualTo(2));

        var importer = new SpotifyTrackImporter(factory);
        var first = await importer.ImportArtistAsync(discography);
        var second = await importer.ImportArtistAsync(discography);
        Assert.That(first, Is.EqualTo(new SpotifyArtistImportResult(2, 2)));
        Assert.That(second, Is.EqualTo(first));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.That(await verify.Artists.CountAsync(), Is.EqualTo(2));
        Assert.That(await verify.Albums.CountAsync(), Is.EqualTo(2));
        Assert.That(await verify.Tracks.CountAsync(), Is.EqualTo(2));
        var storedArtist = await verify.Artists
            .Include(artist => artist.Albums)
            .Include(artist => artist.Tracks)
            .SingleAsync(artist => artist.SpotifyId == "artist-1");
        Assert.That(storedArtist.Albums, Has.Count.EqualTo(2));
        Assert.That(storedArtist.Tracks, Has.Count.EqualTo(2));
        Assert.That(handler.ApiRequests.Count(path => path.Contains("/albums?include_groups=album", StringComparison.Ordinal)),
            Is.EqualTo(2));
        Assert.That(handler.ApiRequests
                .Where(path => path.Contains("/albums?include_groups=album", StringComparison.Ordinal))
                .All(path => path.Contains("limit=10", StringComparison.Ordinal)),
            Is.True);
    }

    [Test]
    public async Task SpotifyAlbumCoverLookupUsesTwentyIdBatchesAndOnlyAcceptsSpotifyCdnUrls()
    {
        var handler = new SpotifyHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var service = new SpotifyCatalogService(
            httpClient,
            new SpotifyAccessTokenProvider(
                new TestHttpClientFactory(handler),
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new SpotifyOptions { ClientId = "test-client", ClientSecret = "test-secret" })));
        var ids = Enumerable.Range(0, 41).Select(index => $"album-{index}").ToArray();

        var covers = await service.GetAlbumCoverUrlsAsync(ids);

        Assert.That(handler.ApiRequests, Has.Count.EqualTo(3));
        Assert.That(handler.ApiRequests.All(request =>
            Uri.UnescapeDataString(request).Split("ids=", StringSplitOptions.None)[1].Split(',').Length <= 20), Is.True);
        Assert.That(covers["album-1"], Is.EqualTo("https://i.scdn.co/image/cover-large"));
        Assert.That(covers.ContainsKey("album-unsafe"), Is.False);
        Assert.That(handler.TokenRequests, Is.EqualTo(1));
    }

    [Test]
    public async Task SpotifyApiFailuresIncludeTheResponseDetails()
    {
        var handler = new SpotifyHandler
        {
            ApiFailureStatusCode = HttpStatusCode.Forbidden,
            ApiFailureBody = """{"error":{"message":"Album catalog access is unavailable."}}"""
        };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var service = new SpotifyCatalogService(
            httpClient,
            new SpotifyAccessTokenProvider(
                new TestHttpClientFactory(handler),
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new SpotifyOptions { ClientId = "test-client", ClientSecret = "test-secret" })));

        var exception = Assert.ThrowsAsync<HttpRequestException>(
            async () => await service.GetAlbumCoverUrlsAsync(["album-1"]));

        Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(exception.Message, Does.Contain("Album catalog access is unavailable."));
    }

    [Test]
    public async Task SpotifyAlbumCoverLookupFallsBackToIndividualAlbumsWhenBatchLookupIsForbidden()
    {
        var handler = new SpotifyHandler { BulkAlbumLookupForbidden = true };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var service = new SpotifyCatalogService(
            httpClient,
            new SpotifyAccessTokenProvider(
                new TestHttpClientFactory(handler),
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new SpotifyOptions { ClientId = "test-client", ClientSecret = "test-secret" })));

        var covers = await service.GetAlbumCoverUrlsAsync(["album-1"]);

        Assert.That(covers["album-1"], Is.EqualTo("https://i.scdn.co/image/individual-cover"));
        Assert.That(handler.ApiRequests, Has.Count.EqualTo(2));
        Assert.That(handler.ApiRequests[0], Does.StartWith("/v1/albums?ids="));
        Assert.That(handler.ApiRequests[1], Is.EqualTo("/v1/albums/album-1"));
    }

    [Test]
    public async Task SpotifyArtistImportRejectsAnonymousUsersBeforeCallingSpotify()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = new ServiceCollection()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var service = new SpotifyArtistCatalogService(
            new SpotifyCatalogMustNotBeCalled(),
            new SpotifyTrackImporter(factory),
            new AnonymousAuthenticationStateProvider());

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.AddArtistAsync(new SpotifyArtistMatch("artist-1", "Artist one")));
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
