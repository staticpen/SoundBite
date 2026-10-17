using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SoundBite.Configuration;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class SpotifyCatalogService(
    HttpClient httpClient,
    SpotifyAccessTokenProvider tokenProvider,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider) : ISpotifyCatalog
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

    public async Task<int> ImportTrackAsync(
        SpotifyTrackMatch match,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (authenticationState.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Sign in before adding tracks to the catalogue.");

        if (string.IsNullOrWhiteSpace(match.SpotifyId) ||
            string.IsNullOrWhiteSpace(match.Title) ||
            string.IsNullOrWhiteSpace(match.AlbumSpotifyId) ||
            string.IsNullOrWhiteSpace(match.AlbumTitle) ||
            match.Artists.Count == 0 ||
            match.Artists.Any(artist =>
                string.IsNullOrWhiteSpace(artist.SpotifyId) ||
                string.IsNullOrWhiteSpace(artist.Name)))
        {
            throw new ArgumentException("The Spotify track metadata is incomplete.", nameof(match));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var artists = new List<Artist>(match.Artists.Count);
        foreach (var artistMatch in match.Artists)
        {
            var artist = await db.Artists.SingleOrDefaultAsync(
                item => item.SpotifyId == artistMatch.SpotifyId,
                cancellationToken);
            if (artist is null)
            {
                artist = new Artist
                {
                    Name = artistMatch.Name,
                    SpotifyId = artistMatch.SpotifyId
                };
                db.Artists.Add(artist);
            }
            else
            {
                artist.Name = artistMatch.Name;
            }

            artists.Add(artist);
        }

        var album = await db.Albums
            .Include(item => item.Artists)
            .SingleOrDefaultAsync(item => item.SpotifyId == match.AlbumSpotifyId, cancellationToken);
        if (album is null)
        {
            album = new Album
            {
                Title = match.AlbumTitle,
                SpotifyId = match.AlbumSpotifyId
            };
            db.Albums.Add(album);
        }
        else
        {
            album.Title = match.AlbumTitle;
        }

        foreach (var artist in artists)
            album.Artists.Add(artist);

        var track = await db.Tracks
            .Include(item => item.Artists)
            .SingleOrDefaultAsync(item => item.SpotifyId == match.SpotifyId, cancellationToken);
        if (track is null)
        {
            track = new Track { SpotifyId = match.SpotifyId };
            db.Tracks.Add(track);
        }

        track.Title = match.Title;
        track.Album = album;
        track.Artists.Clear();
        foreach (var artist in artists)
            track.Artists.Add(artist);

        await db.SaveChangesAsync(cancellationToken);
        return track.Id;
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
