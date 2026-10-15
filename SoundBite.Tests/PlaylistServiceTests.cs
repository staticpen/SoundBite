using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class PlaylistServiceTests
{
    private SqliteConnection connection = null!;
    private TestDbContextFactory factory = null!;
    private PlaylistService service = null!;
    private int firstTrackId;
    private int secondTrackId;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new ApplicationUser { Id = "user-1", UserName = "alice", DisplayName = "Alice" },
                new ApplicationUser { Id = "user-2", UserName = "bob", DisplayName = "Bob" });
            db.Tracks.AddRange(
                new Track { Title = "First track" },
                new Track { Title = "Second track" });
            await db.SaveChangesAsync();
            firstTrackId = db.Tracks.Single(track => track.Title == "First track").Id;
            secondTrackId = db.Tracks.Single(track => track.Title == "Second track").Id;
        }

        factory = new TestDbContextFactory(options);
        service = new PlaylistService(factory);
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public async Task CreatePlaylistPersistsPublicPlaylistAndOwner()
    {
        var playlist = await service.CreateAsync("user-1", "  Favorites  ", "  Great songs  ");

        Assert.Multiple(() =>
        {
            Assert.That(playlist.Id, Is.GreaterThan(0));
            Assert.That(playlist.Name, Is.EqualTo("Favorites"));
            Assert.That(playlist.Description, Is.EqualTo("Great songs"));
            Assert.That(playlist.OwnerId, Is.EqualTo("user-1"));
            Assert.That(playlist.IsPublic, Is.True);
        });

        var publicPlaylists = await service.GetPublicPlaylistsAsync();
        Assert.That(publicPlaylists, Has.Count.EqualTo(1));
        Assert.That(publicPlaylists[0].Owner?.DisplayName, Is.EqualTo("Alice"));
        Assert.That(await service.GetPublicPlaylistAsync(playlist.Id), Is.Not.Null);
    }

    [Test]
    public async Task PublicListingsAndDetailsDoNotExposePrivatePlaylists()
    {
        await service.CreateAsync("user-1", "Public playlist");
        await using (var db = factory.CreateDbContext())
        {
            db.Playlists.Add(new Playlist
            {
                OwnerId = "user-1",
                Name = "Private playlist",
                IsPublic = false
            });
            await db.SaveChangesAsync();
        }

        Assert.That(await service.GetPublicPlaylistsAsync(), Has.Count.EqualTo(1));
        Assert.That(await service.GetPublicPlaylistAsync(2), Is.Null);
    }

    [Test]
    public async Task PlaylistNameIsRequiredAndBounded()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateAsync("user-1", "  "));
        Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateAsync("user-1", new string('x', 101)));
        Assert.ThrowsAsync<ArgumentException>(async () => await service.CreateAsync("user-1", "Name", new string('x', 1001)));
    }

    [Test]
    public async Task TracksAreAddedAndReturnedInPlaylistOrder()
    {
        var playlist = await service.CreateAsync("user-1", "Listening");
        await service.AddTrackAsync("user-1", playlist.Id, firstTrackId);
        await service.AddTrackAsync("user-1", playlist.Id, secondTrackId);

        var persisted = await service.GetPublicPlaylistAsync(playlist.Id);
        Assert.That(persisted!.Items.Select(item => item.Track?.Title),
            Is.EqualTo(new[] { "First track", "Second track" }));
        Assert.That(persisted.Items.Select(item => item.Position), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public async Task DuplicateTrackIsRejectedByServiceAndDatabase()
    {
        var playlist = await service.CreateAsync("user-1", "Listening");
        await service.AddTrackAsync("user-1", playlist.Id, firstTrackId);

        var serviceError = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AddTrackAsync("user-1", playlist.Id, firstTrackId));
        Assert.That(serviceError!.Message, Does.Contain("already in the playlist"));

        await using var db = factory.CreateDbContext();
        db.PlaylistItems.Add(new PlaylistItem
        {
            PlaylistId = playlist.Id,
            TrackId = firstTrackId,
            Position = 1
        });
        var databaseError = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(databaseError!.InnerException, Is.TypeOf<SqliteException>());
        Assert.That(((SqliteException)databaseError.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(2067));
    }

    [Test]
    public async Task OtherUsersCannotEditDeleteOrChangeTracks()
    {
        var playlist = await service.CreateAsync("user-1", "Alice's playlist");
        await service.AddTrackAsync("user-1", playlist.Id, firstTrackId);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.UpdateAsync("user-2", playlist.Id, "Changed", null));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AddTrackAsync("user-2", playlist.Id, secondTrackId));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RemoveTrackAsync("user-2", playlist.Id, firstTrackId));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.DeleteAsync("user-2", playlist.Id));

        var unchanged = await service.GetPublicPlaylistAsync(playlist.Id);
        Assert.That(unchanged!.Name, Is.EqualTo("Alice's playlist"));
        Assert.That(unchanged.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OwnerCanUpdateAndRemoveTrackWithoutDeletingTrack()
    {
        var playlist = await service.CreateAsync("user-1", "Original");
        await service.AddTrackAsync("user-1", playlist.Id, firstTrackId);
        var updated = await service.UpdateAsync("user-1", playlist.Id, "Updated", "Description");
        await service.RemoveTrackAsync("user-1", playlist.Id, firstTrackId);

        Assert.That(updated.Name, Is.EqualTo("Updated"));
        var reloaded = await service.GetPublicPlaylistAsync(playlist.Id);
        Assert.That(reloaded!.Items, Is.Empty);

        await using var db = factory.CreateDbContext();
        Assert.That(await db.Tracks.AnyAsync(track => track.Id == firstTrackId), Is.True);
    }

    [Test]
    public async Task DeletingPlaylistDeletesItemsButPreservesTracks()
    {
        var playlist = await service.CreateAsync("user-1", "Remove me");
        await service.AddTrackAsync("user-1", playlist.Id, firstTrackId);

        await service.DeleteAsync("user-1", playlist.Id);

        await using var db = factory.CreateDbContext();
        Assert.That(await db.Playlists.AnyAsync(), Is.False);
        Assert.That(await db.PlaylistItems.AnyAsync(), Is.False);
        Assert.That(await db.Tracks.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task MissingTrackAndOwnerAreRejected()
    {
        var playlist = await service.CreateAsync("user-1", "Listening");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.AddTrackAsync("user-1", playlist.Id, 999));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateAsync("missing-user", "Invalid owner"));
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
