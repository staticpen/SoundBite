using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;
using SoundBite.Services;

namespace SoundBite.Tests;

public class ReviewServiceTests
{
    private SqliteConnection connection = null!;
    private TestDbContextFactory factory = null!;
    private ReviewService service = null!;
    private int trackId;

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
                new ApplicationUser { Id = "user-1", UserName = "alice", Email = "alice@example.com", DisplayName = "Alice" },
                new ApplicationUser { Id = "user-2", UserName = "bob", Email = "bob@example.com", DisplayName = "Bob" });
            db.Tracks.Add(new Track { Title = "Mock track" });
            await db.SaveChangesAsync();
            trackId = db.Tracks.Single().Id;
        }

        factory = new TestDbContextFactory(options);
        service = new ReviewService(factory);
    }

    [TearDown]
    public async Task TearDown() => await connection.DisposeAsync();

    [Test]
    public async Task CreateReviewPersistsReviewAndReviewer()
    {
        var review = await service.CreateReviewAsync("user-1", trackId, 5, "  Excellent track  ");

        Assert.Multiple(() =>
        {
            Assert.That(review.Id, Is.GreaterThan(0));
            Assert.That(review.UserId, Is.EqualTo("user-1"));
            Assert.That(review.TrackId, Is.EqualTo(trackId));
            Assert.That(review.Rating, Is.EqualTo(5));
            Assert.That(review.Comment, Is.EqualTo("Excellent track"));
        });

        var reviews = await service.GetReviewsForTrackAsync(trackId);
        Assert.That(reviews, Has.Count.EqualTo(1));
        Assert.That(reviews[0].User?.DisplayName, Is.EqualTo("Alice"));
        Assert.That(await service.GetUserReviewForTrackAsync("user-1", trackId), Is.Not.Null);
    }

    [Test]
    public async Task DuplicateReviewForSameUserAndTrackIsRejected()
    {
        await service.CreateReviewAsync("user-1", trackId, 5, "First");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateReviewAsync("user-1", trackId, 4, "Second"));

        Assert.That(exception!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task DatabaseUniqueConstraintRejectsDuplicateReview()
    {
        await service.CreateReviewAsync("user-1", trackId, 5, "First");
        await using var db = factory.CreateDbContext();
        db.Reviews.Add(new Review { UserId = "user-1", TrackId = trackId, Rating = 4, Comment = "Second" });

        var exception = Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
        Assert.That(exception!.InnerException, Is.TypeOf<SqliteException>());
        Assert.That(((SqliteException)exception.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(2067));
    }

    [Test]
    public async Task DatabaseCheckConstraintRejectsOutOfRangeRating()
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Reviews (Rating, Comment, UserId, TrackId, CreatedAt, UpdatedAt)
            VALUES (0, '', 'user-1', $trackId, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            """;
        command.Parameters.AddWithValue("$trackId", trackId);

        var exception = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.That(exception!.SqliteExtendedErrorCode, Is.EqualTo(275));
    }

    [TestCase(0)]
    [TestCase(6)]
    public void ServiceRejectsOutOfRangeRating(int rating) =>
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await service.CreateReviewAsync("user-1", trackId, rating, "Bad"));

    [Test]
    public async Task UpdateReviewChangesExistingRecordWithoutCreatingAnother()
    {
        var created = await service.CreateReviewAsync("user-1", trackId, 3, "Original");
        await Task.Delay(5);

        var updated = await service.UpdateReviewAsync(created.Id, "user-1", 4, "Updated comment");

        Assert.Multiple(() =>
        {
            Assert.That(updated.Id, Is.EqualTo(created.Id));
            Assert.That(updated.Rating, Is.EqualTo(4));
            Assert.That(updated.Comment, Is.EqualTo("Updated comment"));
            Assert.That(updated.CreatedAt, Is.EqualTo(created.CreatedAt));
            Assert.That(updated.UpdatedAt, Is.GreaterThan(created.UpdatedAt));
        });

        await using var db = factory.CreateDbContext();
        Assert.That(await db.Reviews.CountAsync(), Is.EqualTo(1));
        var persisted = await service.GetUserReviewForTrackAsync("user-1", trackId);
        Assert.That(persisted?.Comment, Is.EqualTo("Updated comment"));
    }

    [Test]
    public async Task UserCannotUpdateAnotherUsersReview()
    {
        var created = await service.CreateReviewAsync("user-1", trackId, 3, "Original");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.UpdateReviewAsync(created.Id, "user-2", 4, "Attempted edit"));

        Assert.That(await service.GetReviewsForTrackAsync(trackId), Has.Count.EqualTo(1));
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
