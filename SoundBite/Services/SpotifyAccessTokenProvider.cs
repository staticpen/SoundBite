using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SoundBite.Configuration;

namespace SoundBite.Services;

public sealed class SpotifyAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<SpotifyOptions> options)
{
    private const string CacheKey = "Spotify:AccessToken";
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (memoryCache.TryGetValue(CacheKey, out string? cachedToken))
            return cachedToken!;

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (memoryCache.TryGetValue(CacheKey, out cachedToken))
                return cachedToken!;

            var spotify = options.Value;
            if (!spotify.IsConfigured)
                throw new InvalidOperationException(
                    "Spotify is not configured. Set the Spotify__ClientId and Spotify__ClientSecret environment variables.");

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/token");
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{spotify.ClientId}:{spotify.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

            using var response = await httpClientFactory.CreateClient("SpotifyAuth")
                .SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (!payload.RootElement.TryGetProperty("access_token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(tokenElement.GetString()))
            {
                throw new InvalidDataException("Spotify returned an invalid access-token response.");
            }

            var token = tokenElement.GetString()!;
            var expiresIn = payload.RootElement.TryGetProperty("expires_in", out var expirationElement) &&
                            expirationElement.TryGetInt32(out var seconds)
                ? Math.Max(1, seconds - 60)
                : 300;
            memoryCache.Set(CacheKey, token, TimeSpan.FromSeconds(expiresIn));
            return token;
        }
        finally
        {
            refreshLock.Release();
        }
    }
}
