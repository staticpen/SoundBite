using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SoundBite.Interfaces;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class SpotifyArtistCatalogService(
    ISpotifyCatalog spotifyCatalog,
    SpotifyTrackImporter trackImporter,
    AuthenticationStateProvider authenticationStateProvider)
{
    // Imports an artist's Spotify discography after confirming the user is signed in.
    public async Task<SpotifyArtistImportResult> AddArtistAsync(
        SpotifyArtistMatch artist,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artist);
        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(principal.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            throw new UnauthorizedAccessException("Sign in before adding an artist to the catalogue.");
        }

        var discography = await spotifyCatalog.GetArtistDiscographyAsync(artist, cancellationToken);
        return await trackImporter.ImportArtistAsync(discography, cancellationToken);
    }
}
