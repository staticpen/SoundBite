using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
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
        private const int PageComponentId = 0;

        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);

        public async Task<Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent> RenderComponentAsync<T>(ParameterView parameters) where T : IComponent
        {
            var root = BeginRenderingComponent(typeof(T), parameters);
            await root.QuiescenceTask;
            return root;
        }

        #pragma warning disable BL0006
        public Task RaiseFirstButtonClickAsync() =>
            Dispatcher.InvokeAsync(() =>
            {
                var frames = GetCurrentRenderTreeFrames(PageComponentId);
                for (var index = 0; index < frames.Count; index++)
                {
                    var element = frames.Array[index];
                    if (element.FrameType != RenderTreeFrameType.Element || element.ElementName != "button")
                        continue;

                    for (var next = index + 1; next < frames.Count &&
                         frames.Array[next].FrameType == RenderTreeFrameType.Attribute; next++)
                    {
                        var attribute = frames.Array[next];
                        if (attribute.AttributeName == "onclick" && attribute.AttributeEventHandlerId != 0)
                            return DispatchEventAsync(attribute.AttributeEventHandlerId, null, new MouseEventArgs());
                    }
                }

                throw new InvalidOperationException("The page has no button with an onclick handler.");
            });
        #pragma warning restore BL0006
    }

    public sealed class SearchPage : CatalogueSearch
    {
        [Parameter] public string Query { get; set; } = "";
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            typeof(CatalogueSearch).GetField("query", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(this, Query);
            await (Task)typeof(CatalogueSearch).GetMethod("SearchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null)!;
        }
    }

    private sealed class UnusedSpotifyCatalog : ISpotifyCatalog
    {
        public Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpotifyArtistMatch>>([new("spotify-artist", "Spotify Artist")]);

        public Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
            SpotifyArtistMatch artist,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This test does not import Spotify artists.");

        public Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpotifyTrackMatch>>([]);
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
            Assert.That(links, Is.EqualTo(new[]
            {
                "/artists/11", "/albums/23", "/tracks/42", "/tracks/907", "/artists/11"
            }));
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
                .And.Contain("href=\"/search\"")
                .And.Contain("href=\"/playlists\"")
                .And.Contain("href=\"/feed\"")
                .And.Contain("href=\"/rankings\""));
        });
    }

    [Test]
    public async Task HomePageWelcomesTheSignedInUserByName()
    {
        await WithCatalogue(async renderer =>
        {
            var home = await renderer.RenderComponentAsync<Home>(ParameterView.Empty);
            Assert.That(home.ToHtmlString(), Does.Contain("Welcome, Listener."));
        }, new SignedInAuthenticationStateProvider());
    }

    [Test]
    public async Task SearchPageUsesNewRouteAndOmitsSpotifySearchControls()
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<CatalogueSearch>(ParameterView.Empty);
            var html = search.ToHtmlString();
            Assert.That(html, Does.Contain("<h1>Search</h1>")
                .And.Contain("Search the catalogue")
                .And.Not.Contain("Find tracks on Spotify")
                .And.Not.Contain("Search Spotify")
                .And.Not.Contain("Add to catalogue"));
            Assert.That(typeof(CatalogueSearch).GetCustomAttributes<RouteAttribute>()
                    .Select(route => route.Template),
                Is.EquivalentTo(new[] { "/search", "/catalogue/search" }));
        });
    }

    [TestCase("", "Enter a name or title to search.")]
    [TestCase("missing", "No local results found")]
    public async Task EmptyAndUnmatchedSearchesDoNotRenderMusicDetailLinks(string query, string message)
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<SearchPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SearchPage.Query)] = query }));
            Assert.That(search.ToHtmlString(), Does.Contain(message)
                .And.Not.Contain("href=\"/tracks/")
                .And.Not.Contain("href=\"/albums/"));
        });
    }

    [Test]
    public async Task UnmatchedSearchOffersSpotifyArtistAndPromptsAnonymousUserToCreateAccount()
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<SearchPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SearchPage.Query)] = "unmatched artist" }));
            Assert.That(search.ToHtmlString(), Does.Contain("Artists available to add")
                .And.Contain("Spotify Artist")
                .And.Contain("Add artist"));

            await renderer.RaiseFirstButtonClickAsync();

            Assert.That(search.ToHtmlString(), Does.Contain("Create an account or log in")
                .And.Contain("href=\"/register\"")
                .And.Contain("href=\"/login\""));
        });
    }

    private sealed class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class SignedInAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "listener"),
                new Claim(ClaimTypes.Name, "Listener")
            ],
            "test"))));
    }

    private static async Task WithCatalogue(
        Func<TestRenderer, Task> assertion,
        AuthenticationStateProvider? authenticationStateProvider = null)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var services = new ServiceCollection().AddLogging()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IMusicCatalog, CatalogueService>()
            .AddSingleton<ISpotifyCatalog, UnusedSpotifyCatalog>()
            .AddScoped<SpotifyTrackImporter>()
            .AddScoped<SpotifyArtistCatalogService>()
            .AddScoped<ReviewService>()
            .AddScoped<DemoReviewService>()
            .AddSingleton<NotificationEventDispatcher>()
            .AddScoped<SocialInteractionService>()
            .AddSingleton<AuthenticationStateProvider>(
                authenticationStateProvider ?? new AnonymousAuthenticationStateProvider())
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
