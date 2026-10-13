using System.ComponentModel.DataAnnotations;
using SoundBite.Models;

namespace SoundBite.Tests;

[TestFixture(typeof(Artist), nameof(Artist.Name))]
[TestFixture(typeof(Album), nameof(Album.Title))]
[TestFixture(typeof(Track), nameof(Track.Title))]
public class CatalogueValidationTests(Type modelType, string titleProperty)
{
    private object Create(string? title = "Title", string? spotifyId = null)
    {
        var entity = Activator.CreateInstance(modelType)!;
        modelType.GetProperty(titleProperty)!.SetValue(entity, title);
        modelType.GetProperty("SpotifyId")!.SetValue(entity, spotifyId);
        return entity;
    }

    private static List<ValidationResult> Validate(object entity)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(entity, new ValidationContext(entity), results, true);
        return results;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t\r\n")]
    public void MissingTitleIsInvalid(string? title) =>
        Assert.That(Validate(Create(title)).SelectMany(r => r.MemberNames), Does.Contain(titleProperty));

    [TestCase(200, true)]
    [TestCase(201, false)]
    public void TitleLengthBoundary(int length, bool valid) =>
        Assert.That(Validate(Create(new string('a', length))).Count == 0, Is.EqualTo(valid));

    [TestCase(null, true)]
    [TestCase("AbC123", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    [TestCase("\t\r\n", false)]
    [TestCase(" abc", false)]
    [TestCase("abc ", false)]
    [TestCase("ab cd", false)]
    public void OptionalSpotifyIdValidation(string? id, bool valid)
    {
        var entity = Create(spotifyId: id);
        Assert.That(Validate(entity).Count == 0, Is.EqualTo(valid));
        Assert.That(modelType.GetProperty("SpotifyId")!.GetValue(entity), Is.EqualTo(id));
    }

    [Test]
    public void NavigationCollectionsStartEmpty()
    {
        var entity = Create();
        foreach (var property in modelType.GetProperties().Where(p => p.PropertyType.IsGenericType &&
                     p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)))
            Assert.That(property.GetValue(entity), Is.Not.Null.And.Empty);
        if (entity is Track track)
        {
            Assert.That(track.AlbumId, Is.Null);
            Assert.That(track.Album, Is.Null);
        }
    }
}
