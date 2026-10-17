using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class FollowServiceTests
{
    private string directory = null!;
    private Factory factory = null!;
    private TestAuthentication authentication = null!;
    private FollowService service = null!;

    [SetUp]
    public async Task SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "soundbite-follow-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        factory = new Factory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={directory}/test.db;Pooling=False").Options);
        authentication = new TestAuthentication("alice");
        service = new FollowService(factory, authentication);
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(Enumerable.Range(0, 23).Select(i => new ApplicationUser
        {
            Id = $"user{i:D2}", UserName = $"user{i:D2}", DisplayName = $"Person {i}"
        }));
        db.Users.AddRange(new ApplicationUser { Id = "alice", UserName = "alice" },
            new ApplicationUser { Id = "bob", UserName = "bob" });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public async Task FollowAndUnfollowAreIdempotentAndCountsAreDirectional()
    {
        await service.FollowAsync("bob");
        await service.FollowAsync("bob");
        Assert.That(await service.GetSummaryAsync("bob"), Is.EqualTo(new FollowSummary(1, 0, true, true)));
        Assert.That(await service.GetSummaryAsync("alice"), Is.EqualTo(new FollowSummary(0, 1, false, false)));
        await service.UnfollowAsync("bob");
        await service.UnfollowAsync("bob");
        Assert.That((await service.GetSummaryAsync("bob")).Followers, Is.Zero);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AnonymousOperationsAreRejected(bool follow)
    {
        authentication.Set(null);
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            if (follow) await service.FollowAsync("bob"); else await service.UnfollowAsync("bob");
        });
    }

    [Test]
    public void UnauthenticatedIdentityWithForgedIdIsRejected()
    {
        authentication.Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alice")]));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.FollowAsync("bob"));
    }

    [Test]
    public void MissingClaimAndDeletedActorAreRejected()
    {
        authentication.Principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.FollowAsync("bob"));
        authentication.Set("deleted");
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.FollowAsync("bob"));
    }

    [TestCase("alice")]
    [TestCase("")]
    [TestCase(" ")]
    public void InvalidTargetsAreRejected(string target)
    {
        Assert.ThrowsAsync<ArgumentException>(() => service.FollowAsync(target));
        Assert.ThrowsAsync<ArgumentException>(() => service.UnfollowAsync(target));
    }

    [Test]
    public void MissingTargetIsRejected()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => service.FollowAsync("missing"));
        Assert.ThrowsAsync<InvalidOperationException>(() => service.UnfollowAsync("missing"));
    }

    [Test]
    public async Task AuthenticationIsResolvedForEveryOperationAndCannotRemoveAnotherActorsFollow()
    {
        await service.FollowAsync("bob");
        authentication.Set("user00");
        await service.UnfollowAsync("bob");
        Assert.That((await service.GetSummaryAsync("bob")).Followers, Is.EqualTo(1));
        await service.FollowAsync("bob");
        authentication.Set("alice");
        await service.UnfollowAsync("bob");
        await using var db = factory.CreateDbContext();
        Assert.That((await db.Follows.SingleAsync()).FollowerId, Is.EqualTo("user00"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task PublicListsArePaginatedStableAndDoNotExposeIdentityData(bool followers)
    {
        await using (var db = factory.CreateDbContext())
        {
            db.Follows.AddRange(Enumerable.Range(0, 23).Select(i => new Follow
            {
                FollowerId = followers ? $"user{i:D2}" : "alice",
                FollowingId = followers ? "alice" : $"user{i:D2}"
            }));
            await db.SaveChangesAsync();
        }
        authentication.Set(null);
        var first = await service.GetPageAsync("alice", followers);
        var second = await service.GetPageAsync("alice", followers, 1);
        var last = await service.GetPageAsync("alice", followers, 2);
        Assert.That(first.Users.Count, Is.EqualTo(10));
        Assert.That(first.HasNext && second.HasNext, Is.True);
        Assert.That(last.HasNext, Is.False);
        Assert.That(first.Users.Concat(second.Users).Concat(last.Users).Select(u => u.UserName),
            Is.EqualTo(Enumerable.Range(0, 23).Select(i => $"user{i:D2}")));
        Assert.That((await service.GetPageAsync("alice", followers, 3)).Users, Is.Empty);
        Assert.That(typeof(FollowProfile).GetProperties().Select(p => p.Name), Is.EquivalentTo(new[] { "UserName", "DisplayName" }));
        var summary = await service.GetSummaryAsync("alice");
        Assert.That(followers ? summary.Followers : summary.Following, Is.EqualTo(23));
        Assert.That(summary.CanFollow, Is.False);
    }

    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public void InvalidPageIsRejected(int page) =>
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPageAsync("alice", true, page));

    [TestCase("alice", "alice", 275)]
    [TestCase("alice", "missing", 787)]
    [TestCase("missing", "alice", 787)]
    [TestCase("alice", "bob", 1555)]
    public async Task DatabaseEnforcesCheckForeignKeysAndUniquePair(string actor, string target, int code)
    {
        await service.FollowAsync("bob");
        await using var db = factory.CreateDbContext();
        db.Follows.Add(new Follow { FollowerId = actor, FollowingId = target });
        var error = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(((SqliteException)error!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(code));
    }

    [Test]
    public async Task ConcurrentFollowsAndUnfollowsUseIndependentConnectionsSafely()
    {
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => service.FollowAsync("bob"))));
        Assert.That((await service.GetSummaryAsync("bob")).Followers, Is.EqualTo(1));
        await using (var db = factory.CreateDbContext())
            Assert.That(await db.Notifications.CountAsync(notification =>
                notification.RecipientUserId == "bob" && notification.Type == NotificationType.Follow), Is.EqualTo(1));
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => service.UnfollowAsync("bob"))));
        Assert.That((await service.GetSummaryAsync("bob")).Followers, Is.Zero);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() => i % 2 == 0 ? service.FollowAsync("bob") : service.UnfollowAsync("bob"))));
        Assert.That((await service.GetSummaryAsync("bob")).Followers, Is.InRange(0, 1));
        await service.UnfollowAsync("bob");
        Assert.That((await service.GetSummaryAsync("bob")).Followers, Is.Zero);
    }

    [Test]
    public async Task AccountDeletionRemovesBothDirectionsButPreservesOtherAccountsAndRelationships()
    {
        await service.FollowAsync("bob");
        authentication.Set("bob");
        await service.FollowAsync("alice");
        await service.FollowAsync("user00");
        await using var db = factory.CreateDbContext();
        await db.Users.Where(u => u.Id == "alice").ExecuteDeleteAsync();
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(24));
        var remaining = await db.Follows.SingleAsync();
        Assert.That(remaining.FollowerId, Is.EqualTo("bob"));
        Assert.That(remaining.FollowingId, Is.EqualTo("user00"));
    }

    [Test]
    public async Task MigrationUpgradeAndRollbackPreserveExistingDataAndModelMatchesSnapshot()
    {
        await using var db = factory.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015084204_AddPlaylists");
        db.Tracks.Add(new Track { Title = "Preserved track" });
        db.Playlists.Add(new Playlist { OwnerId = "alice", Name = "Preserved playlist" });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
        await service.FollowAsync("bob");
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(25));
        Assert.That((await db.Tracks.SingleAsync()).Title, Is.EqualTo("Preserved track"));
        Assert.That((await db.Playlists.SingleAsync()).Name, Is.EqualTo("Preserved playlist"));
        await migrator.MigrateAsync("20261015084204_AddPlaylists");
        Assert.That(await db.Users.CountAsync(), Is.EqualTo(25));
        await db.Database.MigrateAsync();
        Assert.That(await db.Follows.CountAsync(), Is.Zero);
    }

    private sealed class Factory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
    private sealed class TestAuthentication : AuthenticationStateProvider
    {
        public ClaimsPrincipal Principal { get; set; } = null!;
        public TestAuthentication(string? id) => Set(id);
        public void Set(string? id) => Principal = id is null ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Principal));
    }
}
