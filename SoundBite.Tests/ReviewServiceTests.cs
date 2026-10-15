using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class ReviewServiceTests
{
    [Test]
    public async Task CreateReviewPersistsReviewAndKeepsLatestMetadata()
    {
        var (factory, trackId) = await CreateFactoryAsync();
        var service = new ReviewService(factory);

        var review = await service.CreateReviewAsync("user-1", trackId, 5, "Excellent track");

        Assert.Multiple(() =>
        {
            Assert.That(review.Id, Is.GreaterThan(0));
            Assert.That(review.UserId, Is.EqualTo("user-1"));
            Assert.That(review.TrackId, Is.EqualTo(trackId));
            Assert.That(review.Rating, Is.EqualTo(5));
            Assert.That(review.Comment, Is.EqualTo("Excellent track"));
        });

        var persisted = await service.GetUserReviewForTrackAsync("user-1", trackId);
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.Comment, Is.EqualTo("Excellent track"));
    }

    [Test]
    public async Task DuplicateReviewForSameUserAndTrackIsRejected()
    {
        var (factory, trackId) = await CreateFactoryAsync();
        var service = new ReviewService(factory);

        await service.CreateReviewAsync("user-1", trackId, 5, "First");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateReviewAsync("user-1", trackId, 4, "Second"));

        Assert.That(exception!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task InvalidRatingsAreRejected()
    {
        var (factory, trackId) = await CreateFactoryAsync();
        var service = new ReviewService(factory);

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await service.CreateReviewAsync("user-1", trackId, 0, "Bad"));

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await service.CreateReviewAsync("user-1", trackId, 6, "Bad"));
    }

    private static async Task<(TestDbContextFactory factory, int trackId)> CreateFactoryAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new ApplicationUser { Id = "user-1", UserName = "alice", Email = "alice@example.com" });
            db.Tracks.Add(new Track { Title = "Mock track" });
            await db.SaveChangesAsync();
        }

        var factory = new TestDbContextFactory(options);
        var trackId = factory.CreateDbContext().Tracks.Single().Id;
        return (factory, trackId);
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => new(new ApplicationDbContext(options));
    }
}
