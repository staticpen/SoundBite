using System.Net;
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

    public sealed class FavouritesSearchPage : Favourites
    {
        [Parameter] public string Query { get; set; } = "";
        [Parameter] public Action<FavouritesSearchPage>? Capture { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            Capture?.Invoke(this);
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            typeof(Favourites).GetField("query", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(this, Query);
            typeof(Favourites).GetField("showAddTracks", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(this, true);
            await InvokePageMethodAsync("SearchTracksAsync");
        }

        public async Task AddFirstArtistAsync()
        {
            var artists = (IReadOnlyList<SpotifyArtistMatch>)typeof(Favourites)
                .GetField("spotifyArtists", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this)!;
            await InvokePageMethodAsync("AddArtistAsync", artists[0]);
            StateHasChanged();
        }

        public async Task AddFirstMatchingTrackAsync()
        {
            var tracks = (IReadOnlyList<Track>)typeof(Favourites)
                .GetField("searchResults", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this)!;
            await InvokePageMethodAsync("AddAsync", tracks[0]);
            StateHasChanged();
        }

        private Task InvokePageMethodAsync(string name, params object[]? arguments) =>
            (Task)typeof(Favourites).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, arguments)!;
    }

    private sealed class TestSpotifyCatalog : ISpotifyCatalog
    {
        public Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpotifyArtistMatch>>([new("spotify-artist", "Spotify Artist")]);

        public Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
            SpotifyArtistMatch artist,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpotifyArtistDiscography(
                artist,
                [
                    new SpotifyAlbumCatalogue(
                        "spotify-album",
                        "Spotify album",
                        [artist],
                        [new SpotifyTrackMatch(
                            "spotify-track",
                            "Spotify track",
                            "spotify-album",
                            "Spotify album",
                            [artist])])
                ]));

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
                "/artists/11", "/albums/23", "/tracks/42", "/login",
                "/tracks/907", "/login", "/artists/11"
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
                if (id == 42)
                {
                    Assert.That(detailHtml, Does.Contain("href=\"/artists/11\"")
                        .And.Contain("https://i.scdn.co/image/echo-album-cover")
                        .And.Contain("catalogue-cover-detail"));
                }
                Assert.That(detailHtml, Does.Contain(id == 42 ? "4 / 5 stars" : "2 / 5 stars"));
            }
        });
    }

    [Test]
    public async Task SearchResultsOfferAddingTracksToFavouritesWithoutAReview()
    {
        await WithCatalogue(async renderer =>
        {
            var search = await renderer.RenderComponentAsync<SearchPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(SearchPage.Query)] = "echo" }));
            Assert.That(search.ToHtmlString(), Does.Contain("Add to Favourites")
                .And.Contain("href=\"/tracks/42\""));
        }, new SignedInAuthenticationStateProvider());
    }

    [Test]
    public async Task FavouritesSearchCanImportArtistCatalogueThenAddMatchingTrack()
    {
        await WithCatalogue(async renderer =>
        {
            FavouritesSearchPage pageInstance = null!;
            var page = await renderer.RenderComponentAsync<FavouritesSearchPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(FavouritesSearchPage.Query)] = "Spotify",
                    [nameof(FavouritesSearchPage.Capture)] = (Action<FavouritesSearchPage>)(value => pageInstance = value)
                }));
            Assert.That(page.ToHtmlString(), Does.Contain("Artists available to add")
                .And.Contain("Spotify Artist")
                .And.Contain("Add artist and tracks"));

            await renderer.Dispatcher.InvokeAsync(pageInstance.AddFirstArtistAsync);

            Assert.That(page.ToHtmlString(), Does.Contain("Added Spotify Artist: 1 albums and 1 tracks.")
                .And.Contain("Spotify track")
                .And.Contain("Add to Favourites"));

            await renderer.Dispatcher.InvokeAsync(pageInstance.AddFirstMatchingTrackAsync);

            Assert.That(page.ToHtmlString(), Does.Contain("Added Spotify track to Favourites.")
                .And.Contain("Spotify track")
                .And.Contain("Added"));
        }, new SignedInAuthenticationStateProvider());
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
                .And.Contain("href=\"/tracks/42\"")
                .And.Contain("https://i.scdn.co/image/echo-album-cover")
                .And.Contain("catalogue-cover-thumb"));

            var album = await renderer.RenderComponentAsync<AlbumDetails>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(AlbumDetails.Id)] = 23 }));
            Assert.That(album.ToHtmlString(), Does.Contain("Echo album")
                .And.Contain("href=\"/artists/11\"")
                .And.Contain("href=\"/tracks/42\""));
        });
    }

    private static string HeroOf(string html)
    {
        var hero = Regex.Match(html, "<section id=\"home-hero\".*?</section>", RegexOptions.Singleline);
        Assert.That(hero.Success, Is.True, "The homepage must render the #home-hero section.");
        return hero.Value;
    }

    // Matches primary buttons by class token so attribute order and extra attributes don't matter.
    private static string[] PrimaryActions(string html) =>
        Regex.Matches(html, "<a\\b([^>]*)>(.*?)</a>", RegexOptions.Singleline)
            .Where(match => Regex.IsMatch(match.Groups[1].Value, "class=\"[^\"]*\\bbtn-primary\\b"))
            .Select(match => Regex.Match(match.Groups[1].Value, "href=\"([^\"]*)\"").Groups[1].Value + "|" +
                WebUtility.HtmlDecode(match.Groups[2].Value).Trim())
            .ToArray();

    private static bool IsCentred(string hero) =>
        Regex.IsMatch(hero, "^<section id=\"home-hero\"[^>]*class=\"[^\"]*\\btext-center\\b");

    private static async Task<string> RenderHomeAsync(TestRenderer renderer) =>
        (await renderer.RenderComponentAsync<Home>(ParameterView.Empty)).ToHtmlString();

    [Test]
    public async Task SignedOutHomePageHeroInvitesVisitorsToGetStarted()
    {
        await WithCatalogue(async renderer =>
        {
            var html = await RenderHomeAsync(renderer);
            var hero = HeroOf(html);
            var text = WebUtility.HtmlDecode(hero);
            Assert.That(IsCentred(hero), Is.True, "The anonymous hero must be centre-aligned.");
            Assert.That(PrimaryActions(hero), Is.EqualTo(new[] { "/register|Get Started" }));
            Assert.That(text, Does.Contain("Your music taste deserves a place of its own.</h1>")
                .And.Contain("Discover new favourites, rate the tracks you love, and share your taste with people who get it.")
                .And.Contain("<strong>Every track has a story. What's yours?</strong>")
                .And.Contain("⭐</span> <strong>Rate</strong> the music that moves you.")
                .And.Contain("✍️</span> <strong>Review</strong> tracks worth talking about.")
                .And.Contain("👥</span> <strong>Follow</strong> listeners who share your taste.")
                .And.Contain("\u2764\uFE0F</span> <strong>Save</strong> your favourites, all in one place.")
                .And.Contain("<em>Your music. Your ratings. Your SoundBite.</em>"));
            Assert.That(Regex.Matches(hero, "<span aria-hidden=\"true\">").Count, Is.EqualTo(4),
                "Decorative feature emoji must be hidden from assistive technology.");
            Assert.That(hero, Does.Not.Contain("href=\"/login\"").And.Not.Contain("&nbsp;")
                .And.Not.Contain("href=\"/search\"").And.Not.Contain("Welcome back"));
            Assert.That(html, Does.Not.Contain("activity-feed-heading")
                .And.Not.Contain("to see recent community activity"));
        });
    }

    [Test]
    public async Task HomePageWelcomesTheSignedInUserByDisplayName()
    {
        await WithCatalogue(async renderer =>
        {
            var hero = HeroOf(await RenderHomeAsync(renderer));
            var text = WebUtility.HtmlDecode(hero);
            Assert.That(IsCentred(hero), Is.True, "The signed-in hero must be centre-aligned like the anonymous hero.");
            Assert.That(text, Does.Contain("Welcome back, Listener Display! Your music journey starts here.</h1>")
                .And.Contain("SoundBite is a community for discovering, rating, and reviewing music.")
                .And.Contain("share your own opinions through ratings and reviews.")
                .And.Contain("<strong>Ready to get started?</strong> Search for your favourite artist")
                .And.Contain("connect with other music lovers through shared experiences."));
            Assert.That(PrimaryActions(hero), Is.EqualTo(new[] { "/search|Search the catalogue" }));
            Assert.That(hero, Does.Not.Contain("href=\"/login\"").And.Not.Contain("href=\"/register\""));
        }, new SignedInAuthenticationStateProvider());
    }

    [TestCase("plainlistener", "Claim name", "Welcome back, plainlistener! Your music journey starts here.")]
    [TestCase("missing-user", "Claim name", "Welcome back, Claim name! Your music journey starts here.")]
    [TestCase(null, "Claim name", "Welcome back, Claim name! Your music journey starts here.")]
    [TestCase(null, null, "Welcome back! Your music journey starts here.")]
    public async Task HomePageWelcomeFallsBackFromDisplayNameToUsernameToClaimNameToGenericGreeting(
        string? userId, string? claimName, string expectedHeading)
    {
        await WithCatalogue(async renderer =>
        {
            var hero = WebUtility.HtmlDecode(HeroOf(await RenderHomeAsync(renderer)));
            Assert.That(hero, Does.Contain(expectedHeading + "</h1>"));
            Assert.That(PrimaryActions(hero), Is.EqualTo(new[] { "/search|Search the catalogue" }));
        }, new SignedInAuthenticationStateProvider(userId, claimName));
    }

    [Test]
    public async Task HomePageStillRendersWhenThePreferredNameLookupFails()
    {
        await WithCatalogue(async renderer =>
        {
            var html = await RenderHomeAsync(renderer);
            Assert.That(WebUtility.HtmlDecode(HeroOf(html)), Does.Contain("Welcome back, Listener! Your music journey starts here."));
            Assert.That(html, Does.Not.Contain("Simulated name lookup failure")
                .And.Not.Contain("id=\"home-rankings\"")
                .And.Contain("activity-feed-heading"));
        }, new SignedInAuthenticationStateProvider(), services =>
            services.AddScoped(_ => new UserService(new FailingContextFactory())));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HomePageLeavesRatingsAndTrendsOnTheirSeparatePage(bool signedIn)
    {
        await WithCatalogue(async renderer =>
        {
            var html = await RenderHomeAsync(renderer);
            Assert.That(html, Does.Not.Contain("id=\"home-rankings\"")
                .And.Not.Contain("top-rated-heading")
                .And.Not.Contain("most-reviewed-heading")
                .And.Not.Contain("trending-heading"));

            var feed = html.IndexOf("activity-feed-heading", StringComparison.Ordinal);
            if (signedIn)
                Assert.That(feed, Is.GreaterThan(html.IndexOf("id=\"home-hero\"", StringComparison.Ordinal)),
                    "The activity feed belongs below the signed-in hero.");
            else
                Assert.That(feed, Is.EqualTo(-1));
        }, signedIn ? new SignedInAuthenticationStateProvider() : null);
    }

    [Test]
    public async Task SignedInHomePageCentresTheActivityFeedInTheSharedContentColumn()
    {
        await WithCatalogue(async renderer =>
        {
            var html = await RenderHomeAsync(renderer);
            Assert.That(Regex.IsMatch(html,
                    "<div id=\"home-activity\"[^>]*class=\"content-column\"[^>]*>\\s*<section[^>]*aria-labelledby=\"activity-feed-heading\""),
                Is.True, "The feed must sit in the centred, 1024px-capped content column.");
            Assert.That(html, Does.Not.Contain("col-lg-6"));
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

    private sealed class SignedInAuthenticationStateProvider(string? userId = "listener", string? name = "Listener")
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var claims = new List<Claim>();
            if (userId is not null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            if (name is not null)
                claims.Add(new Claim(ClaimTypes.Name, name));
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))));
        }
    }

    private sealed class FailingContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            throw new InvalidOperationException("Simulated name lookup failure");
    }

    private static async Task WithCatalogue(
        Func<TestRenderer, Task> assertion,
        AuthenticationStateProvider? authenticationStateProvider = null,
        Action<IServiceCollection>? configureServices = null)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var serviceCollection = new ServiceCollection().AddLogging()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IMusicCatalog, CatalogueService>()
            .AddSingleton<ISpotifyCatalog, TestSpotifyCatalog>()
            .AddScoped<SpotifyTrackImporter>()
            .AddScoped<SpotifyArtistCatalogService>()
            .AddScoped<ReviewService>()
            .AddScoped<DemoReviewService>()
            .AddScoped<FavouriteService>()
            .AddScoped<FeedService>()
            .AddSingleton<NotificationEventDispatcher>()
            .AddScoped<SocialInteractionService>()
            .AddScoped<UserService>()
            .AddSingleton<AuthenticationStateProvider>(
                authenticationStateProvider ?? new AnonymousAuthenticationStateProvider());
        configureServices?.Invoke(serviceCollection);
        await using var services = serviceCollection.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await using (var db = await services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            var artist = new Artist { Id = 11, Name = "Echo artist" };
            var album = new Album
            {
                Id = 23,
                Title = "Echo album",
                CoverUrl = "https://i.scdn.co/image/echo-album-cover",
                Artists = [artist]
            };
            db.AddRange(artist, album,
                new Track { Id = 42, Title = "Echo <track>", SpotifyId = "spotify-first", Album = album, Artists = [artist] },
                new Track { Id = 907, Title = "Echo <track>", SpotifyId = "spotify-second" });
            db.Users.AddRange(
                new ApplicationUser { Id = "reviewer", UserName = "reviewer", DisplayName = "Integration reviewer" },
                new ApplicationUser { Id = "listener", UserName = "listener", DisplayName = "Listener Display" },
                new ApplicationUser { Id = "plainlistener", UserName = "plainlistener" });
            db.Reviews.Add(new Review { UserId = "reviewer", TrackId = 42, Rating = 4, Comment = "First track review" });
            db.Reviews.Add(new Review { UserId = "reviewer", TrackId = 907, Rating = 2, Comment = "Second track review" });
            await db.SaveChangesAsync();
        }
        await using var renderer = new TestRenderer(scope.ServiceProvider, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => assertion(renderer));
    }
}
