using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SoundBite.Configuration;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class SpotifyCatalogService(
    HttpClient httpClient,
    SpotifyAccessTokenProvider tokenProvider) : ISpotifyCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SpotifyTrackMatch>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = query.Trim();
        if (term.Length == 0)
            return [];

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"search?q={Uri.EscapeDataString(term)}&type=track&limit=10");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await tokenProvider.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SpotifySearchResponse>(
            JsonOptions,
            cancellationToken);
        if (payload?.Tracks?.Items is null)
            throw new InvalidDataException("Spotify returned an invalid track search response.");

        return payload.Tracks.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) &&
                           !string.IsNullOrWhiteSpace(item.Name) &&
                           !string.IsNullOrWhiteSpace(item.Album?.Id) &&
                           !string.IsNullOrWhiteSpace(item.Album.Name) &&
                           item.Artists.Any(artist =>
                               !string.IsNullOrWhiteSpace(artist.Id) &&
                               !string.IsNullOrWhiteSpace(artist.Name)))
            .Select(item => new SpotifyTrackMatch(
                item.Id!,
                item.Name!,
                item.Album!.Id!,
                item.Album.Name!,
                item.Artists
                    .Where(artist => !string.IsNullOrWhiteSpace(artist.Id) &&
                                     !string.IsNullOrWhiteSpace(artist.Name))
                    .Select(artist => new SpotifyArtistMatch(artist.Id!, artist.Name!))
                    .ToArray()))
            .ToArray();
    }

    private sealed class SpotifySearchResponse
    {
        public SpotifyTrackPage? Tracks { get; set; }
    }

    private sealed class SpotifyTrackPage
    {
        public List<SpotifyTrackItem> Items { get; set; } = [];
    }

    private sealed class SpotifyTrackItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public SpotifyAlbum? Album { get; set; }
        public List<SpotifyArtist> Artists { get; set; } = [];
    }

    private sealed class SpotifyAlbum
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class SpotifyArtist
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
