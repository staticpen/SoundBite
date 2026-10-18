using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class SpotifyCatalogService(
    HttpClient httpClient,
    SpotifyAccessTokenProvider tokenProvider) : ISpotifyCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SpotifyArtistMatch>> SearchArtistsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = query.Trim();
        if (term.Length == 0)
            return [];

        var payload = await GetApiResponseAsync<SpotifyArtistSearchResponse>(
            $"search?q={Uri.EscapeDataString(term)}&type=artist&limit=10",
            cancellationToken);
        if (payload?.Artists?.Items is null)
            throw new InvalidDataException("Spotify returned an invalid artist search response.");

        return payload.Artists.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) &&
                           !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new SpotifyArtistMatch(item.Id!, item.Name!))
            .ToArray();
    }

    public async Task<SpotifyArtistDiscography> GetArtistDiscographyAsync(
        SpotifyArtistMatch artist,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (string.IsNullOrWhiteSpace(artist.SpotifyId) || string.IsNullOrWhiteSpace(artist.Name))
            throw new ArgumentException("The Spotify artist metadata is incomplete.", nameof(artist));

        var albums = await GetAllPagesAsync<SpotifyAlbumItem>(
            $"artists/{Uri.EscapeDataString(artist.SpotifyId)}/albums?include_groups=album&limit=10",
            cancellationToken);
        var catalogueAlbums = new List<SpotifyAlbumCatalogue>(albums.Count);

        foreach (var album in albums)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(album.Id) || string.IsNullOrWhiteSpace(album.Name))
                continue;

            var albumArtists = MapArtists(album.Artists);
            if (albumArtists.Count == 0)
                albumArtists = [artist];

            var albumTracks = await GetAllPagesAsync<SpotifyTrackItem>(
                $"albums/{Uri.EscapeDataString(album.Id)}/tracks?limit=50",
                cancellationToken);
            var tracks = albumTracks
                .Where(track => !string.IsNullOrWhiteSpace(track.Id) &&
                                !string.IsNullOrWhiteSpace(track.Name))
                .Select(track =>
                {
                    var trackArtists = MapArtists(track.Artists);
                    return new SpotifyTrackMatch(
                        track.Id!,
                        track.Name!,
                        album.Id!,
                        album.Name!,
                        trackArtists.Count > 0 ? trackArtists : albumArtists);
                })
                .ToArray();

            catalogueAlbums.Add(new SpotifyAlbumCatalogue(
                album.Id,
                album.Name,
                albumArtists,
                tracks,
                SelectAlbumCoverUrl(album.Images)));
        }

        return new SpotifyArtistDiscography(artist, catalogueAlbums);
    }

    public async Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = query.Trim();
        if (term.Length == 0)
            return [];

        var payload = await GetApiResponseAsync<SpotifyTrackSearchResponse>(
            $"search?q={Uri.EscapeDataString(term)}&type=track&limit=10",
            cancellationToken);
        if (payload?.Tracks?.Items is null)
            throw new InvalidDataException("Spotify returned an invalid track search response.");

        return payload.Tracks.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) &&
                           !string.IsNullOrWhiteSpace(item.Name) &&
                           !string.IsNullOrWhiteSpace(item.Album?.Id) &&
                           !string.IsNullOrWhiteSpace(item.Album.Name) &&
                           MapArtists(item.Artists).Count > 0)
            .Select(item => new SpotifyTrackMatch(
                item.Id!,
                item.Name!,
                item.Album!.Id!,
                item.Album.Name!,
                MapArtists(item.Artists),
                SelectAlbumCoverUrl(item.Album.Images)))
            .ToArray();
    }

    private static string? SelectAlbumCoverUrl(IEnumerable<SpotifyImageItem>? images) =>
        (images ?? [])
            .Where(image => Uri.TryCreate(image.Url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                (uri.Host.EndsWith(".scdn.co", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith(".spotifycdn.com", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(image => Math.Max(image.Width ?? 0, image.Height ?? 0))
            .Select(image => image.Url)
            .FirstOrDefault();

    private async Task<List<T>> GetAllPagesAsync<T>(
        string initialUrl,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        string? nextUrl = initialUrl;
        while (nextUrl is not null)
        {
            var page = await GetApiResponseAsync<SpotifyPage<T>>(nextUrl, cancellationToken);
            if (page?.Items is null)
                throw new InvalidDataException("Spotify returned an invalid paged catalogue response.");

            items.AddRange(page.Items);
            nextUrl = page.Next;
        }

        return items;
    }

    private async Task<T?> GetApiResponseAsync<T>(string url, CancellationToken cancellationToken)
    {
        var requestUri = Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(httpClient.BaseAddress
                ?? throw new InvalidOperationException("Spotify API base address is not configured."), url);
        if (requestUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(requestUri.Host, "api.spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Spotify returned a pagination URL outside the Spotify API.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await tokenProvider.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static IReadOnlyList<SpotifyArtistMatch> MapArtists(IEnumerable<SpotifyArtistItem>? artists) =>
        (artists ?? [])
            .Where(artist => !string.IsNullOrWhiteSpace(artist.Id) &&
                             !string.IsNullOrWhiteSpace(artist.Name))
            .Select(artist => new SpotifyArtistMatch(artist.Id!, artist.Name!))
            .ToArray();

    private sealed class SpotifyArtistSearchResponse
    {
        public SpotifyPage<SpotifyArtistItem>? Artists { get; set; }
    }

    private sealed class SpotifyTrackSearchResponse
    {
        public SpotifyPage<SpotifyTrackItem>? Tracks { get; set; }
    }

    private sealed class SpotifyPage<T>
    {
        public List<T> Items { get; set; } = [];
        public string? Next { get; set; }
    }

    private sealed class SpotifyArtistItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class SpotifyAlbumItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<SpotifyArtistItem> Artists { get; set; } = [];
        public List<SpotifyImageItem> Images { get; set; } = [];
    }

    private sealed class SpotifyImageItem
    {
        public string? Url { get; set; }
        public int? Height { get; set; }
        public int? Width { get; set; }
    }

    private sealed class SpotifyTrackItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public SpotifyAlbumItem? Album { get; set; }
        public List<SpotifyArtistItem> Artists { get; set; } = [];
    }
}
