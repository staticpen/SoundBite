namespace SoundBite.Models;

public class CatalogueSearchResults
{
    public IReadOnlyList<Artist> Artists { get; init; } = [];
    public IReadOnlyList<Album> Albums { get; init; } = [];
    public IReadOnlyList<Track> Tracks { get; init; } = [];
    public int TotalCount => Artists.Count + Albums.Count + Tracks.Count;
}
