using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Tests;

public class CataloguePersistenceTests
{
    private SqliteConnection connection = null!;
    private ApplicationDbContext db = null!;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options);
        // Exercise the shipped migrations, not a separately generated EnsureCreated schema.
        db.Database.Migrate();
    }

    [TearDown]
    public void TearDown()
    {
        db.Dispose();
        connection.Dispose();
    }

    private static object Entity(string kind, string? id) => kind switch
    {
        "Artist" => new Artist { Name = "Same name", SpotifyId = id },
        "Album" => new Album { Title = "Same title", SpotifyId = id },
        _ => new Track { Title = "Same title", SpotifyId = id }
    };

    [TestCase("Artist")]
    [TestCase("Album")]
    [TestCase("Track")]
    public void DuplicateSpotifyIdInsertIsRejected(string kind)
    {
        db.Add(Entity(kind, "external-id"));
        db.SaveChanges();
        db.Add(Entity(kind, "external-id"));
        AssertConstraint(() => db.SaveChanges(), 2067);
    }

    [TestCase("Artist")]
    [TestCase("Album")]
    [TestCase("Track")]
    public void DuplicateSpotifyIdUpdateIsRejected(string kind)
    {
        var second = Entity(kind, "second");
        db.AddRange(Entity(kind, "first"), second);
        db.SaveChanges();
        db.Entry(second).Property("SpotifyId").CurrentValue = "first";
        AssertConstraint(() => db.SaveChanges(), 2067);
    }

    [TestCase("Artist")]
    [TestCase("Album")]
    [TestCase("Track")]
    public void NullIdsDuplicateTitlesAndCaseDistinctIdsAreAllowed(string kind)
    {
        db.AddRange(Entity(kind, null), Entity(kind, null), Entity(kind, "AbC"), Entity(kind, "abc"));
        Assert.That(db.SaveChanges(), Is.EqualTo(4));
    }

    [Test]
    public void IdsAreUniqueWithinEachEntityType()
    {
        db.AddRange(Entity("Artist", "same"), Entity("Album", "same"), Entity("Track", "same"));
        Assert.That(db.SaveChanges(), Is.EqualTo(3));
    }

    [Test]
    public void CollaborationsAndOptionalAlbumRoundTrip()
    {
        var artists = new[] { new Artist { Name = "First" }, new Artist { Name = "Second" } };
        var album = new Album { Title = "Collaboration", Artists = artists.ToList() };
        var anotherAlbum = new Album { Title = "Another", Artists = artists.ToList() };
        var track = new Track { Title = "Album track", Album = album, Artists = artists.ToList() };
        var single = new Track { Title = "Single", Artists = artists.ToList() };
        db.AddRange(album, anotherAlbum, track, single);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var reloaded = db.Artists.Include(a => a.Albums).Include(a => a.Tracks).ToList();
        Assert.That(reloaded, Has.Count.EqualTo(2));
        Assert.That(reloaded.All(a => a.Albums.Count == 2 && a.Tracks.Count == 2), Is.True);
        var albums = db.Albums.Include(a => a.Artists).Include(a => a.Tracks).ToList();
        Assert.That(albums.All(a => a.Artists.Count == 2), Is.True);
        Assert.That(albums.Single(a => a.Id == album.Id).Tracks.Single().Id, Is.EqualTo(track.Id));
        var tracks = db.Tracks.Include(t => t.Album).Include(t => t.Artists).ToList();
        Assert.That(tracks.All(t => t.Artists.Count == 2), Is.True);
        Assert.That(tracks.Single(t => t.Id == single.Id).AlbumId, Is.Null);
        Assert.That(tracks.Single(t => t.Id == single.Id).Album, Is.Null);
        Assert.That(tracks.Single(t => t.Id == track.Id).Album!.Id, Is.EqualTo(album.Id));
    }

    [Test]
    public void MissingAlbumForeignKeyIsRejected()
    {
        db.Tracks.Add(new Track { Title = "Orphan", AlbumId = 999 });
        AssertConstraint(() => db.SaveChanges(), 787);
    }

    [Test]
    public void ReferencedAlbumCannotBeDeleted()
    {
        var album = new Album { Title = "Keep" };
        db.Tracks.Add(new Track { Title = "Keep track", Album = album });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        db.Albums.Remove(db.Albums.Single());
        // SQLite implements RESTRICT with a trigger constraint.
        AssertConstraint(() => db.SaveChanges(), 1811);
    }

    [TestCase("ArtistAlbums", "AlbumsId")]
    [TestCase("ArtistTracks", "TracksId")]
    public void JoinPrimaryKeysRejectDuplicateAssociations(string table, string otherKey)
    {
        var artist = new Artist { Name = "Artist" };
        var album = new Album { Title = "Album", Artists = new List<Artist> { artist } };
        var track = new Track { Title = "Track", Artists = new List<Artist> { artist } };
        db.AddRange(album, track);
        db.SaveChanges();
        using var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {table} (ArtistsId, {otherKey}) VALUES ($artist, $other)";
        command.Parameters.AddWithValue("$artist", artist.Id);
        command.Parameters.AddWithValue("$other", table == "ArtistAlbums" ? album.Id : track.Id);
        var error = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.That(error!.SqliteExtendedErrorCode, Is.EqualTo(1555));
    }

    [TestCase("Artist")]
    [TestCase("Album")]
    [TestCase("Track")]
    public void DeletionCascadesOnlyToJoinRows(string kind)
    {
        var artist = new Artist { Name = "Artist" };
        var album = new Album { Title = "Album", Artists = new List<Artist> { artist } };
        var track = new Track { Title = "Single", Artists = new List<Artist> { artist } };
        db.AddRange(album, track);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        db.Remove(kind switch { "Artist" => (object)db.Artists.Single(), "Album" => db.Albums.Single(), _ => db.Tracks.Single() });
        db.SaveChanges();
        Assert.That(db.Artists.Count(), Is.EqualTo(kind == "Artist" ? 0 : 1));
        Assert.That(db.Albums.Count(), Is.EqualTo(kind == "Album" ? 0 : 1));
        Assert.That(db.Tracks.Count(), Is.EqualTo(kind == "Track" ? 0 : 1));
        foreach (var table in new[] { "ArtistAlbums", "ArtistTracks" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table}";
            var deleted = kind == "Artist" || (kind == "Album" && table == "ArtistAlbums") || (kind == "Track" && table == "ArtistTracks");
            Assert.That((long)command.ExecuteScalar()!, Is.EqualTo(deleted ? 0 : 1));
        }
    }

    [Test]
    public void CatalogueMigrationUpgradesAndRollsBackWithoutLosingIdentityData()
    {
        var migrator = db.GetService<IMigrator>();
        migrator.Migrate("20261009035437_InitialCreate");
        db.Database.ExecuteSqlRaw("""
            INSERT INTO AspNetUsers
                (Id, UserName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES
                ('existing-user', 'existing-user', 0, 0, 0, 0, 0)
            """);
        migrator.Migrate();
        Assert.That(db.Users.Count(), Is.EqualTo(1));
        Assert.That(db.Database.GetPendingMigrations(), Is.Empty);
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        db.Tracks.Add(new Track { Title = "Migrated" });
        db.SaveChanges();
        migrator.Migrate("20261009035437_InitialCreate");
        Assert.That(db.Users.Count(), Is.EqualTo(1));
        migrator.Migrate();
        Assert.That(db.Tracks.Count(), Is.Zero);
    }

    [Test]
    public void UserProfileFieldsPersistAndMigrationHasNoPendingModelChanges()
    {
        var user = new ApplicationUser
        {
            UserName = "profile-user",
            DisplayName = "Profile Name",
            Bio = "Music fan"
        };
        db.Users.Add(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var reloaded = db.Users.Single();
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.DisplayName, Is.EqualTo("Profile Name"));
            Assert.That(reloaded.Bio, Is.EqualTo("Music fan"));
            Assert.That(db.Database.GetPendingMigrations(), Is.Empty);
            Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        });
    }

    private static void AssertConstraint(TestDelegate action, int extendedCode)
    {
        var error = Assert.Throws<DbUpdateException>(action);
        Assert.That(error!.InnerException, Is.TypeOf<SqliteException>());
        Assert.That(((SqliteException)error.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(extendedCode));
    }
}
