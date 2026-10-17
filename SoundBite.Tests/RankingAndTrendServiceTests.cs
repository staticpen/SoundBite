using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundBite.Components.Pages;
using SoundBite.Data;
using SoundBite.Interfaces;
using SoundBite.Models;
using SoundBite.Services;
using SoundBite.Strategies;

namespace SoundBite.Tests;

public sealed class RankingAndTrendServiceTests
{
    private sealed class TestRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer(services, loggerFactory)
    {
        protected override IComponent ResolveComponentForRenderMode(
            Type componentType,
            int? parentComponentId,
            IComponentActivator componentActivator,
            IComponentRenderMode renderMode) =>
            componentActivator.CreateInstance(componentType);

        public async Task<Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent>
            RenderComponentAsync<T>(ParameterView parameters) where T : IComponent
        {
            var root = BeginRenderingComponent(typeof(T), parameters);
            await root.QuiescenceTask;
            return root;
        }
    }

    private SqliteConnection connection = null!;
    private ServiceProvider provider = null!;
    private IDbContextFactory<ApplicationDbContext> contextFactory = null!;
    private RankingService rankings = null!;
    private TrendService trends = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .AddScoped<IRankingStrategy, RatingRankingStrategy>()
            .AddScoped<IRankingStrategy, PopularityRankingStrategy>()
            .AddScoped<RankingService>()
            .AddScoped<TrendService>()
            .BuildServiceProvider();
        contextFactory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        rankings = new RankingService(contextFactory,
        [
            new RatingRankingStrategy(),
            new PopularityRankingStrategy()
        ]);
        trends = new TrendService(contextFactory);

        await using var db = await contextFactory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        var users = Enumerable.Range(1, 7)
            .Select(id => new ApplicationUser
            {
                Id = $"user-{id}",
                UserName = $"user-{id}",
                DisplayName = $"User {id}"
            })
            .ToArray();
        var tracks = new[]
        {
            new Track { Id = 1, Title = "Widely reviewed" },
            new Track { Id = 2, Title = "Highest rated" },
            new Track { Id = 3, Title = "Single review" },
            new Track { Id = 4, Title = "Old favourite" }
        };
        db.Users.AddRange(users);
        db.Tracks.AddRange(tracks);

        var now = DateTime.UtcNow;
        AddReview(db, users[0], tracks[0], 5, now.AddDays(-20));
        AddReview(db, users[1], tracks[0], 3, now.AddDays(-6));
        AddReview(db, users[2], tracks[0], 4, now.AddDays(-4));
        AddReview(db, users[3], tracks[0], 4, now.AddDays(-1));
        AddReview(db, users[4], tracks[1], 5, now.AddDays(-3));
        AddReview(db, users[5], tracks[1], 5, now.AddDays(-1));
        AddReview(db, users[6], tracks[2], 5, now.AddDays(-1));
        AddReview(db, users[0], tracks[3], 5, now.AddDays(-10));
        AddReview(db, users[1], tracks[3], 4, now.AddDays(-9));
        AddReview(db, users[2], tracks[3], 5, now.AddDays(-8));
        await db.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task TopRatedUsesAverageWithMinimumReviewCountAndStableTieBreaks()
    {
        var result = await rankings.GetTopRatedTracksAsync();

        Assert.That(result.Select(entry => entry.Title),
            Is.EqualTo(new[] { "Highest rated", "Old favourite", "Widely reviewed" }));
        Assert.That(result.Select(entry => entry.ReviewCount), Is.EqualTo(new[] { 2, 3, 4 }));
        Assert.That(result[0].AverageRating, Is.EqualTo(5));
        Assert.That(result.Select(entry => entry.Title), Does.Not.Contain("Single review"));
    }

    [Test]
    public async Task MostReviewedRanksByAllTimeReviewCountAndAppliesLimit()
    {
        var result = await rankings.GetMostReviewedTracksAsync(limit: 2);

        Assert.That(result.Select(entry => entry.Title),
            Is.EqualTo(new[] { "Widely reviewed", "Old favourite" }));
        Assert.That(result.Select(entry => entry.ReviewCount), Is.EqualTo(new[] { 4, 3 }));
    }

    [Test]
    public async Task TrendingCountsOnlyReviewsWithinTheRequestedRecentWindow()
    {
        var result = await trends.GetTrendingTracksAsync(windowDays: 7);

        Assert.That(result.Select(entry => entry.Title),
            Is.EqualTo(new[] { "Widely reviewed", "Highest rated", "Single review" }));
        Assert.That(result.Select(entry => entry.RecentReviewCount), Is.EqualTo(new[] { 3, 2, 1 }));
        Assert.That(result[0].RecentAverageRating, Is.EqualTo(11d / 3).Within(0.001));
    }

    [Test]
    public async Task RankingsPageExplainsMetricsAndLinksRankedTracks()
    {
        await using var renderer = new TestRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var page = await renderer.RenderComponentAsync<Rankings>(ParameterView.Empty);
            var html = page.ToHtmlString();
            Assert.That(html, Does.Contain("Top rated")
                .And.Contain("Most reviewed")
                .And.Contain("Trending this week")
                .And.Contain("at least 2 reviews")
                .And.Contain("href=\"/tracks/1\"")
                .And.Contain("href=\"/tracks/2\"")
                .And.Contain("Widely reviewed")
                .And.Contain("Highest rated"));
        });
    }

    [Test]
    public void RankingAndTrendBoundsRejectInvalidInputs()
    {
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await rankings.GetTopRatedTracksAsync(0));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await rankings.GetMostReviewedTracksAsync(RankingService.MaximumLimit + 1));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await trends.GetTrendingTracksAsync(windowDays: TrendService.MaximumWindowDays + 1));
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await trends.GetTrendingTracksAsync(limit: 0));
    }

    private static void AddReview(
        ApplicationDbContext db,
        ApplicationUser user,
        Track track,
        int rating,
        DateTime createdAt) =>
        db.Reviews.Add(new Review
        {
            User = user,
            Track = track,
            Rating = rating,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });
}
