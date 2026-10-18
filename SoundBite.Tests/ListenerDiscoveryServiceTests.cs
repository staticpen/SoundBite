using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class ListenerDiscoveryServiceTests
{
    private string directory = null!;
    private Factory factory = null!;
    private ListenerDiscoveryService service = null!;

    [SetUp]
    public async Task SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "soundbite-listeners-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        factory = new Factory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={directory}/test.db;Pooling=False").Options);
        service = new ListenerDiscoveryService(factory);

        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(
            NewUser("alice", "alice"),
            NewUser("listener-one", "listenerOne"),
            NewUser("listener-two", "listenerTwo"));
        db.Users.AddRange(Enumerable.Range(0, 25)
            .Select(index => NewUser($"listener-{index:D2}", $"listener{index:D2}")));
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public async Task UsernameSearchIsCaseInsensitiveExcludesCurrentUserAndHasAResultLimit()
    {
        var results = await service.FindUsersAsync("  LiStEnEr ", "alice");

        Assert.That(results, Has.Count.EqualTo(ListenerDiscoveryService.UserSearchLimit));
        Assert.That(results.All(user => user.UserName.StartsWith("listener", StringComparison.OrdinalIgnoreCase)), Is.True);
        Assert.That(results.Any(user => user.UserName == "alice"), Is.False);
        Assert.That(await service.FindUsersAsync("l", "alice"), Is.Empty);
        Assert.That(await service.FindUsersAsync("  ", "alice"), Is.Empty);
    }

    [Test]
    public async Task ActiveListenersShowEachUsersLatestRecentReviewAndExcludeCurrentUser()
    {
        var now = DateTime.UtcNow;
        await using (var db = factory.CreateDbContext())
        {
            db.Tracks.AddRange(
                new Track { Id = 1, Title = "Earlier track" },
                new Track { Id = 2, Title = "Latest track" },
                new Track { Id = 3, Title = "Carol's track" },
                new Track { Id = 4, Title = "Old track" });
            db.Reviews.AddRange(
                NewReview("listener-one", 1, now.AddDays(-1)),
                NewReview("listener-one", 2, now.AddMinutes(-30)),
                NewReview("listener-two", 3, now.AddHours(-1)),
                NewReview("alice", 1, now.AddMinutes(-10)),
                NewReview("listener-00", 4, now.AddDays(-(ListenerDiscoveryService.ActiveReviewWindowDays + 1))));
            await db.SaveChangesAsync();
        }

        var results = await service.GetActiveListenersAsync("alice");

        Assert.That(results.Select(listener => listener.UserName), Is.EqualTo(new[] { "listenerOne", "listenerTwo" }));
        Assert.That(results[0].TrackTitle, Is.EqualTo("Latest track"));
        Assert.That(results[0].TrackId, Is.EqualTo(2));
        Assert.That(results[0].ReviewedAt, Is.EqualTo(now.AddMinutes(-30)));
    }

    private static ApplicationUser NewUser(string id, string userName) => new()
    {
        Id = id,
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant()
    };

    private static Review NewReview(string userId, int trackId, DateTime createdAt) => new()
    {
        UserId = userId,
        TrackId = trackId,
        Rating = 5,
        CreatedAt = createdAt
    };

    private sealed class Factory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}
