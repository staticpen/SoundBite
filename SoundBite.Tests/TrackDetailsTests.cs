using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundBite.Components.Pages;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Tests;

public class TrackDetailsTests
{
    public sealed class ObservedTrackDetails : TrackDetails
    {
        public Task PendingLoad { get; private set; } = Task.CompletedTask;
        protected override Task OnParametersSetAsync() => PendingLoad = base.OnParametersSetAsync();
    }

    public sealed class NavigationHost : ComponentBase
    {
        [Parameter] public Action<NavigationHost> Capture { get; set; } = null!;
        public ObservedTrackDetails Page { get; private set; } = null!;
        private int id = 1;
        protected override void OnInitialized() => Capture(this);
        public void Navigate(int nextId)
        {
            id = nextId;
            StateHasChanged();
        }
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<ObservedTrackDetails>(0);
            builder.AddAttribute(1, nameof(TrackDetails.Id), id);
            builder.AddComponentReferenceCapture(2, component => Page = (ObservedTrackDetails)component);
            builder.CloseComponent();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NavigationIgnoresStaleSuccessAndFailure(bool failOldRequest)
    {
        var oldRequest = new TaskCompletionSource<Track?>();
        var newRequest = new TaskCompletionSource<Track?>();
        var catalog = new CatalogStub { Load = id => id == 1 ? oldRequest.Task : newRequest.Task };
        await RenderAsync(catalog, async renderer =>
        {
            NavigationHost host = null!;
            var root = renderer.BeginRenderingComponent<NavigationHost>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(NavigationHost.Capture)] = (Action<NavigationHost>)(value => host = value) }));
            var oldLoad = host.Page.PendingLoad;
            host.Navigate(2);
            var newLoad = host.Page.PendingLoad;
            Assert.That(root.ToHtmlString(), Does.Contain("Loading track..."));
            newRequest.SetResult(new Track { Title = "Current track" });
            await newLoad;
            if (failOldRequest) oldRequest.SetException(new InvalidOperationException("Stale failure"));
            else oldRequest.SetResult(new Track { Title = "Obsolete track" });
            await oldLoad;
            await root.QuiescenceTask;
            Assert.That(root.ToHtmlString(), Does.Contain("Current track").And.Not.Contain("Obsolete track").And.Not.Contain("couldn't load"));
            host.Navigate(0);
            Assert.That(root.ToHtmlString(), Does.Contain("Invalid track ID").And.Not.Contain("Current track"));
            Assert.That(catalog.RequestedIds, Is.EqualTo(new[] { 1, 2 }));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task MissingSpotifyIdsHideLinksButKeepAlbum(string? spotifyId)
    {
        await RenderAsync(new CatalogStub { Load = _ => Task.FromResult<Track?>(new Track
        {
            Title = "Track", SpotifyId = spotifyId,
            Album = new Album { Title = "Album without Spotify", SpotifyId = spotifyId }
        }) }, async renderer =>
        {
            var root = await renderer.RenderComponentAsync<TrackDetails>(Parameters(1));
            Assert.That(root.ToHtmlString(), Does.Contain("Album without Spotify").And.Not.Contain("open.spotify.com"));
        });
    }

    private sealed class CatalogStub : IMusicCatalog
    {
        public Func<int, Task<Track?>> Load { get; set; } = _ => Task.FromResult<Track?>(null);
        public List<int> RequestedIds { get; } = [];
        public Task<CatalogueSearchResults> SearchAsync(string? query, CancellationToken cancellationToken = default)
            => Task.FromResult(new CatalogueSearchResults());
        public Task<Track?> GetTrackAsync(int id, CancellationToken cancellationToken = default)
        {
            RequestedIds.Add(id);
            return Load(id);
        }
        public Task<Artist?> GetArtistAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Album?> GetAlbumAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Artist>> GetArtistsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Album>> GetAlbumsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static async Task RenderAsync(CatalogStub catalog, Func<HtmlRenderer, Task> assertion)
    {
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IMusicCatalog>(catalog).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => assertion(renderer));
    }

    private static ParameterView Parameters(int id) => ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(TrackDetails.Id)] = id });

    [Test]
    public async Task ShowsTrackCollaboratorsAlbumAndStoredSpotifyMetadata()
    {
        var catalog = new CatalogStub { Load = _ => Task.FromResult<Track?>(new Track
        {
            Title = "Track <one>", SpotifyId = "track/id",
            Artists = [new() { Name = "First artist" }, new() { Name = "Second artist" }],
            Album = new() { Title = "Album title", SpotifyId = "album-id" }
        }) };
        await RenderAsync(catalog, async renderer =>
        {
            var root = await renderer.RenderComponentAsync<TrackDetails>(Parameters(42));
            var html = root.ToHtmlString();
            foreach (var expected in new[] { "Track &lt;one&gt;", "First artist", "Second artist", "Album title", "https://open.spotify.com/track/track%2Fid", "https://open.spotify.com/album/album-id" })
                Assert.That(html, Does.Contain(expected));
            Assert.That(catalog.RequestedIds, Is.EqualTo(new[] { 42 }));
        });
    }

    [Test]
    public async Task HandlesMissingOptionalInformation()
    {
        var catalog = new CatalogStub { Load = _ => Task.FromResult<Track?>(new Track { Title = "Single" }) };
        await RenderAsync(catalog, async renderer =>
        {
            var root = await renderer.RenderComponentAsync<TrackDetails>(Parameters(1));
            Assert.That(root.ToHtmlString(), Does.Contain("No artist information available.").And.Contain("No album information available.").And.Not.Contain("open.spotify.com"));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task InvalidIdsDoNotQueryDatabase(int id)
    {
        var catalog = new CatalogStub();
        await RenderAsync(catalog, async renderer =>
        {
            var root = await renderer.RenderComponentAsync<TrackDetails>(Parameters(id));
            Assert.That(root.ToHtmlString(), Does.Contain("Invalid track ID"));
            Assert.That(catalog.RequestedIds, Is.Empty);
        });
    }

    [Test]
    public async Task MissingTrackShowsNotFound() => await RenderAsync(new CatalogStub(), async renderer =>
    {
        var root = await renderer.RenderComponentAsync<TrackDetails>(Parameters(999));
        Assert.That(root.ToHtmlString(), Does.Contain("Track not found."));
    });

    [Test]
    public async Task DatabaseFailureShowsSafeMessage() => await RenderAsync(new CatalogStub
    {
        Load = _ => Task.FromException<Track?>(new Microsoft.Data.Sqlite.SqliteException("private database details", 1))
    }, async renderer =>
    {
        var root = await renderer.RenderComponentAsync<TrackDetails>(Parameters(1));
        Assert.That(root.ToHtmlString(), Does.Contain("couldn't load this track").And.Not.Contain("private database details"));
    });

    [Test]
    public async Task LoadingIsVisibleUntilQueryCompletes()
    {
        var completion = new TaskCompletionSource<Track?>();
        await RenderAsync(new CatalogStub { Load = _ => completion.Task }, async renderer =>
        {
            var root = renderer.BeginRenderingComponent<TrackDetails>(Parameters(1));
            Assert.That(root.ToHtmlString(), Does.Contain("Loading track...").And.Contain("role=\"status\""));
            completion.SetResult(new Track { Title = "Loaded track" });
            await root.QuiescenceTask;
            Assert.That(root.ToHtmlString(), Does.Contain("Loaded track").And.Not.Contain("Loading track..."));
        });
    }
}
