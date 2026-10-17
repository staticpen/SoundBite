using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundBite.Components.Pages;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class CatalogueIntegrationTests
{
    // Drive the existing search handler without changing the production component's API.
    private sealed class TestRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, loggerFactory)
    {
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);

        public async Task<Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent> RenderComponentAsync<T>(ParameterView parameters) where T : IComponent
        {
            var root = BeginRenderingComponent(typeof(T), parameters);
            await root.QuiescenceTask;
            return root;
        }
    }

    public sealed class SearchPage : CatalogueSearch
    {
        [Parameter] public string Query { get; set; } = "";
        protected override async Task OnInitializedAsync()
        {
            typeof(CatalogueSearch).GetField("query", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(this, Query);
            await (Task)typeof(CatalogueSearch).GetMethod("SearchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null)!;
        }
    }

    private sealed class UnusedSpotifyCatalog : ISpotifyCatalog
    {
        public Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpotifyTrackMatch>>([]);

        public Task<int> ImportTrackAsync(
            SpotifyTrackMatch match,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This test does not import Spotify tracks.");
    }

    [TestCase(" eChO ")]
    [TestCase("echo")]
    public async Task SearchLinksUseTrackPrimaryKeysAndResolveToMatchingDetails(string query)
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<SearchPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SearchPage.Query)] = query }));
            var html = search.ToHtmlString();
            Assert.That(html, Does.Contain("Echo artist").And.Contain("Echo album"));
            var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
            Assert.That(links, Is.EqualTo(new[] { "/artists/11", "/albums/23", "/tracks/42", "/tracks/907" }));
            Assert.That(typeof(ArtistDetails).GetCustomAttributes<RouteAttribute>().Select(r => r.Template),
                Does.Contain("/artists/{Id:int}"));
            Assert.That(typeof(AlbumDetails).GetCustomAttributes<RouteAttribute>().Select(r => r.Template),
                Does.Contain("/albums/{Id:int}"));
            Assert.That(html, Does.Contain("Echo &lt;track&gt;"));
            Assert.That(typeof(TrackDetails).GetCustomAttributes<RouteAttribute>().Select(r => r.Template),
                Does.Contain("/tracks/{Id:int}"));
            foreach (var link in links.Where(link => link.StartsWith("/tracks/", StringComparison.Ordinal)))
            {
                var id = int.Parse(link.Split('/').Last());
                var details = await renderer.RenderComponentAsync<TrackDetails>(ParameterView.FromDictionary(
                    new Dictionary<string, object?> { [nameof(TrackDetails.Id)] = id }));
                Assert.That(details.ToHtmlString(), Does.Contain("Echo &lt;track&gt;"));
                var detailHtml = details.ToHtmlString();
                Assert.That(detailHtml, Does.Contain(id == 42 ? "spotify-first" : "spotify-second"));
                Assert.That(detailHtml, Does.Contain("Reviews (1)").And.Contain("Integration reviewer")
                    .And.Contain("Log in</a> to rate and review this track."));
                Assert.That(detailHtml, Does.Contain(id == 42 ? "First track review" : "Second track review")
                    .And.Not.Contain(id == 42 ? "Second track review" : "First track review"));
                Assert.That(detailHtml, Does.Contain(id == 42 ? "4 / 5 stars" : "2 / 5 stars"));
            }
        });
    }

    [Test]
    public async Task ArtistAndAlbumDetailsLinkThroughTheirRelatedCatalogueRecords()
    {
        await WithCatalogue(async renderer =>
        {
            var artist = await renderer.RenderComponentAsync<ArtistDetails>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(ArtistDetails.Id)] = 11 }));
            Assert.That(artist.ToHtmlString(), Does.Contain("Echo artist")
                .And.Contain("href=\"/albums/23\"")
                .And.Contain("href=\"/tracks/42\""));

            var album = await renderer.RenderComponentAsync<AlbumDetails>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(AlbumDetails.Id)] = 23 }));
            Assert.That(album.ToHtmlString(), Does.Contain("Echo album")
                .And.Contain("href=\"/artists/11\"")
                .And.Contain("href=\"/tracks/42\""));
        });
    }

    [Test]
    public async Task HomePageProvidesCatalogueAndCommunityEntryPoints()
    {
        await WithCatalogue(async renderer =>
        {
            var home = await renderer.RenderComponentAsync<Home>(ParameterView.Empty);
            var html = home.ToHtmlString();
            Assert.That(html, Does.Contain("Discover music")
                .And.Contain("href=\"/catalogue/search\"")
                .And.Contain("href=\"/playlists\"")
                .And.Contain("href=\"/feed\"")
                .And.Contain("href=\"/rankings\""));
        });
    }

    [Test]
    public async Task CatalogueSearchOffersSeparateSpotifyImportSearch()
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<CatalogueSearch>(ParameterView.Empty);
            Assert.That(search.ToHtmlString(), Does.Contain("Find tracks on Spotify")
                .And.Contain("Search Spotify")
                .And.Contain("Add a track and its artist and album details"));
        });
    }

    [TestCase("", "Enter a name or title to search.")]
    [TestCase("missing", "No results found")]
    public async Task EmptyAndUnmatchedSearchesDoNotRenderLinks(string query, string message)
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<SearchPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SearchPage.Query)] = query }));
            Assert.That(search.ToHtmlString(), Does.Contain(message).And.Not.Contain("href="));
        });
    }

    private sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private static async Task WithCatalogue(Func<TestRenderer, Task> assertion)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection().AddLogging()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IMusicCatalog, CatalogueService>()
            .AddSingleton<ISpotifyCatalog, UnusedSpotifyCatalog>()
            .AddScoped<ReviewService>()
            .AddSingleton<NotificationEventDispatcher>()
            .AddScoped<SocialInteractionService>()
            .AddSingleton<AuthenticationStateProvider>(new AnonymousAuthenticationStateProvider())
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await using (var db = await services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            var artist = new Artist { Id = 11, Name = "Echo artist" };
            var album = new Album { Id = 23, Title = "Echo album", Artists = [artist] };
            db.AddRange(artist, album,
                new Track { Id = 42, Title = "Echo <track>", SpotifyId = "spotify-first", Album = album, Artists = [artist] },
                new Track { Id = 907, Title = "Echo <track>", SpotifyId = "spotify-second" });
            db.Users.Add(new ApplicationUser { Id = "reviewer", UserName = "reviewer", DisplayName = "Integration reviewer" });
            db.Reviews.Add(new Review { UserId = "reviewer", TrackId = 42, Rating = 4, Comment = "First track review" });
            db.Reviews.Add(new Review { UserId = "reviewer", TrackId = 907, Rating = 2, Comment = "Second track review" });
            await db.SaveChangesAsync();
        }
        await using var renderer = new TestRenderer(scope.ServiceProvider, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => assertion(renderer));
    }
}
