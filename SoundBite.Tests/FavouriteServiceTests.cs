using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class FavouriteServiceTests
{
    private SqliteConnection connection = null!;
    private Factory factory = null!;
    private TestAuthentication authentication = null!;
    private FavouriteService service = null!;
    private int firstTrackId;
    private int secondTrackId;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        factory = new Factory(options);
        authentication = new TestAuthentication("alice");
        service = new FavouriteService(factory, authentication);

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice", UserName = "alice" },
            new ApplicationUser { Id = "bob", UserName = "bob" });
        db.Tracks.AddRange(new Track { Title = "First track" }, new Track { Title = "Second track" });
        await db.SaveChangesAsync();
        firstTrackId = await db.Tracks.Where(track => track.Title == "First track")
            .Select(track => track.Id).SingleAsync();
        secondTrackId = await db.Tracks.Where(track => track.Title == "Second track")
            .Select(track => track.Id).SingleAsync();
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public async Task UserCanCreateOneOrderedFavouriteListAndCannotAddDuplicates()
    {
        await service.AddAsync(firstTrackId);
        await service.AddAsync(secondTrackId);

        var favourites = await service.GetMineAsync();
        Assert.That(favourites.Select(item => item.Track?.Title),
            Is.EqualTo(new[] { "First track", "Second track" }));
        Assert.That(await service.ContainsAsync(firstTrackId), Is.True);
        Assert.That(await service.ContainsAsync(secondTrackId), Is.True);
        var duplicate = Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(firstTrackId));
        Assert.That(duplicate!.Message, Does.Contain("already in your favourites"));
    }

    [Test]
    public async Task FavouriteListIsPrivateToEachUser()
    {
        await service.AddAsync(firstTrackId);
        authentication.Set("bob");

        Assert.That(await service.GetMineAsync(), Is.Empty);
        Assert.That(await service.ContainsAsync(firstTrackId), Is.False);
        await service.AddAsync(firstTrackId);
        Assert.That(await service.GetMineAsync(), Has.Count.EqualTo(1));

        authentication.Set("alice");
        Assert.That(await service.GetMineAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UserCanRemoveFavouriteWithoutDeletingTrack()
    {
        await service.AddAsync(firstTrackId);
        await service.RemoveAsync(firstTrackId);

        Assert.That(await service.GetMineAsync(), Is.Empty);
        await using var db = factory.CreateDbContext();
        Assert.That(await db.Tracks.AnyAsync(track => track.Id == firstTrackId), Is.True);
    }

    [Test]
    public void MissingTrackAndAnonymousUserAreRejected()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(int.MaxValue));
        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetMineAsync());
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddAsync(firstTrackId));
    }

    [Test]
    public async Task DatabaseEnforcesUniqueUserAndTrackPair()
    {
        await service.AddAsync(firstTrackId);
        await using var db = factory.CreateDbContext();
        db.FavouriteTracks.Add(new FavouriteTrack { UserId = "alice", TrackId = firstTrackId });

        var exception = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)exception!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(1555));
    }

    private sealed class Factory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateDbContext());
    }

    private sealed class TestAuthentication(string? userId) : AuthenticationStateProvider
    {
        private string? currentUserId = userId;

        public void Set(string? id) => currentUserId = id;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = currentUserId is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, currentUserId)], "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
