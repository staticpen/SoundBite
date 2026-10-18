using SoundBite.Interfaces;

namespace SoundBite.Services;

public sealed record SpotifyCatalogueSeedResult(int QueriesCompleted, int TracksImported);

public sealed class SpotifyCatalogueSeeder(
    ISpotifyCatalog spotifyCatalog,
    SpotifyTrackImporter trackImporter)
{
    public static IReadOnlyList<string> DefaultQueries { get; } =
    [
        "The Beatles",
        "Taylor Swift",
        "Miles Davis",
        "Daft Punk",
        "Beyonce"
    ];

    public async Task<SpotifyCatalogueSeedResult> SeedAsync(
        IReadOnlyList<string>? queries = null,
        CancellationToken cancellationToken = default)
    {
        queries ??= DefaultQueries;
        if (queries.Count == 0 || queries.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty Spotify search query is required.", nameof(queries));

        var importedSpotifyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var query in queries)
        {
            var matches = await spotifyCatalog.SearchTracksAsync(query.Trim(), cancellationToken);
            foreach (var match in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!importedSpotifyIds.Add(match.SpotifyId))
                    continue;

                await trackImporter.ImportAsync(match, cancellationToken);
            }
        }

        return new SpotifyCatalogueSeedResult(queries.Count, importedSpotifyIds.Count);
    }
}
