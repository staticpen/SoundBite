using System.ComponentModel.DataAnnotations;
using SoundBite.Models;

namespace SoundBite.Tests;

public class ArtistTests
{
    [Test]
    public void Artist_WithValidName_PassesValidation()
    {
        var artist = new Artist { Name = "Nina Simone" };

        Assert.That(artist.Name, Is.EqualTo("Nina Simone"));
        Assert.DoesNotThrow(() => Validator.ValidateObject(
            artist, new ValidationContext(artist), validateAllProperties: true));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\r\n")]
    public void Artist_WithMissingOrWhitespaceName_FailsValidation(string? name)
    {
        var artist = new Artist { Name = name! };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            artist, new ValidationContext(artist), validationResults,
            validateAllProperties: true);

        Assert.That(isValid, Is.False);
        Assert.That(validationResults, Has.Count.EqualTo(1));
        Assert.That(validationResults[0].MemberNames, Does.Contain(nameof(Artist.Name)));
    }

    [Test]
    public void Artist_WithoutSpotifyId_PassesValidation()
    {
        var artist = new Artist { Name = "Nina Simone" };

        Assert.That(artist.SpotifyId, Is.Null);
        Assert.DoesNotThrow(() => Validator.ValidateObject(
            artist, new ValidationContext(artist), validateAllProperties: true));
    }

    [Test]
    public void Artist_ByDefault_HasInitializedEmptyNavigationCollections()
    {
        var artist = new Artist();

        Assert.That(artist.Albums, Is.Not.Null.And.Empty);
        Assert.That(artist.Tracks, Is.Not.Null.And.Empty);
    }

    [Test]
    public void Artist_CanHaveMultipleAlbumsAndTracks()
    {
        var artist = new Artist { Name = "Nina Simone" };
        var firstAlbum = new Album();
        var secondAlbum = new Album();
        var firstTrack = new Track();
        var secondTrack = new Track();

        artist.Albums.Add(firstAlbum);
        artist.Albums.Add(secondAlbum);
        artist.Tracks.Add(firstTrack);
        artist.Tracks.Add(secondTrack);

        Assert.That(artist.Albums, Is.EquivalentTo(new[] { firstAlbum, secondAlbum }));
        Assert.That(artist.Tracks, Is.EquivalentTo(new[] { firstTrack, secondTrack }));
    }
}
