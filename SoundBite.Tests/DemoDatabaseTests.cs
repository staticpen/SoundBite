using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoundBite.Data;
using SoundBite.Demo;
using SoundBite.Interfaces;
using SoundBite.Services;

namespace SoundBite.Tests;

[NonParallelizable]
public class DemoDatabaseTests
{
    private string directory = null!;
    private string root = null!;
    private string connection = null!;
    private WebApplicationFactory<Program> app = null!;
    private DemoLogin[] logins = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "SoundBite.sln"))) current = current.Parent;
        root = current!.FullName;
        directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".soundbite-demo-test-" + Guid.NewGuid());
        Assert.That(await DemoSetup.CreateAsync(directory, root), Is.True);
        logins = JsonSerializer.Deserialize<DemoLogin[]>(await File.ReadAllTextAsync(Path.Combine(directory, "credentials.json")))!;
        connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "soundbite-demo.db"), Pooling = false, ForeignKeys = true }.ToString();
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection }));
        });
    }

    [OneTimeTearDown]
    public async Task Cleanup()
    {
        if (app != null) await app.DisposeAsync();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public async Task SeedHasCompleteRelationshipsIntegrityAndRankingData()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        Assert.Multiple(() =>
        {
            Assert.That(db.Artists.Count(), Is.EqualTo(10));
            Assert.That(db.Albums.Count(), Is.EqualTo(15));
            Assert.That(db.Tracks.Count(), Is.EqualTo(40));
            Assert.That(db.Users.Count(), Is.EqualTo(3));
            Assert.That(db.Reviews.Count(), Is.EqualTo(90));
            Assert.That(db.Playlists.Count(), Is.EqualTo(6));
            Assert.That(db.PlaylistItems.Count(), Is.EqualTo(48));
            Assert.That(db.Follows.Count(), Is.EqualTo(6));
            Assert.That(db.Comments.Count(), Is.EqualTo(36));
            Assert.That(db.Likes.Count(), Is.EqualTo(60));
            Assert.That(db.Notifications.Count(), Is.EqualTo(84));
            Assert.That(db.Tracks.All(track => track.AlbumId != null && track.Artists.Any()), Is.True);
            Assert.That(db.Comments.Where(c => c.ParentCommentId != null).All(c => c.ParentComment!.ReviewId == c.ReviewId && c.ParentComment.ParentCommentId == null), Is.True);
        });
        Assert.That(await db.Database.GetPendingMigrationsAsync(), Is.Empty);
        Assert.That((await db.Database.GetAppliedMigrationsAsync()).Count(), Is.EqualTo(13));
        Assert.That(await db.DemoReviews.CountAsync(), Is.Zero, "Offline users retain their ordinary, social-enabled reviews.");
        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("ok"));
        command.CommandText = "PRAGMA foreign_key_check";
        using (var rows = await command.ExecuteReaderAsync()) Assert.That(await rows.ReadAsync(), Is.False);
        using var scope = app.Services.CreateScope();
        var rankings = scope.ServiceProvider.GetRequiredService<RankingService>();
        var topRated = await rankings.GetTopRatedTracksAsync();
        Assert.That(topRated, Has.Count.EqualTo(10));
        Assert.That(topRated.All(track => track.DemoReviewCount == 0 && track.ReviewCount >= 2), Is.True);
        Assert.That(await rankings.GetMostReviewedTracksAsync(), Has.Count.EqualTo(10));
        Assert.That(await scope.ServiceProvider.GetRequiredService<TrendService>().GetTrendingTracksAsync(), Has.Count.EqualTo(10));
        var results = await scope.ServiceProvider.GetRequiredService<IMusicCatalog>().SearchAsync("Harbour");
        Assert.That(results.Tracks, Is.Not.Empty);
        Assert.That(results.Artists, Is.Not.Empty);
    }

    [Test]
    public async Task RerunDoesNotChangeDatabaseOrCredentials()
    {
        // Other tests may log in; compare immediately before/after this setup invocation.
        var database = Path.Combine(directory, "soundbite-demo.db");
        var credentials = Path.Combine(directory, "credentials.json");
        var beforeDb = SHA256.HashData(await File.ReadAllBytesAsync(database));
        var beforeCredentials = SHA256.HashData(await File.ReadAllBytesAsync(credentials));
        Assert.That(await DemoSetup.CreateAsync(directory, root), Is.False);
        Assert.That(SHA256.HashData(await File.ReadAllBytesAsync(database)), Is.EqualTo(beforeDb));
        Assert.That(SHA256.HashData(await File.ReadAllBytesAsync(credentials)), Is.EqualTo(beforeCredentials));
        if (!OperatingSystem.IsWindows())
            Assert.That(File.GetUnixFileMode(credentials), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }

    [TestCase("demo.alex")]
    [TestCase("demo.sam")]
    [TestCase("demo.jamie")]
    public async Task GeneratedLoginAndMajorPagesDisplaySeededContent(string username)
    {
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = logins.Single(item => item.UserName == username);
        var html = await client.GetStringAsync("/login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
        var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["_handler"] = "login",
            ["loginModel.UserNameOrEmail"] = username, ["loginModel.Password"] = login.Password
        }));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(await client.GetStringAsync("/account"), Does.Contain("Signed in"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var track = await db.Tracks.SingleAsync(t => t.Title == "Harbour Lights");
        var album = await db.Albums.SingleAsync(a => a.Title == "Windows at Dusk");
        var artist = await db.Artists.SingleAsync(a => a.Name == "Harbour Lanterns");
        var playlist = await db.Playlists.SingleAsync(p => p.Name == "Evening Harbour Walk");
        foreach (var (path, expected) in new[]
        {
            ($"/tracks/{track.Id}", "Warm production"), ($"/albums/{album.Id}", "Harbour Lights"),
            ($"/artists/{artist.Id}", "Windows at Dusk"), ("/playlists", "Evening Harbour Walk"),
            ($"/playlists/{playlist.Id}", "Harbour Lights"), ("/rankings", "Harbour Lights"),
            ("/feed", "Harbour"), ("/notifications", "liked your playlist"),
            ($"/users/{username}", "Synthetic demo profile"), ("/catalogue/search", "Search the catalogue")
        })
        {
            var page = await client.GetAsync(path);
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
            Assert.That(await page.Content.ReadAsStringAsync(), Does.Contain(expected), path);
        }
    }

    [Test]
    public void RejectsRepositoryRelativeAndExistingDestinations()
    {
        Assert.Throws<ArgumentException>(() => DemoSetup.ValidateDirectory(Path.Combine(root, "demo"), root));
        Assert.Throws<ArgumentException>(() => DemoSetup.ValidateDirectory("demo", root));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await DemoSetup.CreateAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), root));
    }
    [Test]
    public async Task FreshDirectoryResetPreservesPreviousDemo()
    {
        var resetDirectory = directory + "-reset";
        var before = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(directory, "soundbite-demo.db")));
        try
        {
            Assert.That(await DemoSetup.CreateAsync(resetDirectory, root), Is.True);
            Assert.That(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(directory, "soundbite-demo.db"))), Is.EqualTo(before));
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = Path.Combine(resetDirectory, "soundbite-demo.db"), Pooling = false }.ToString()).Options);
            Assert.That(await db.Reviews.CountAsync(), Is.EqualTo(90));
            Assert.That(await db.Tracks.CountAsync(), Is.EqualTo(40));
        }
        finally { if (Directory.Exists(resetDirectory)) Directory.Delete(resetDirectory, true); }
    }

    [Test]
    public void ProductionAndSymbolicLinksAreRejectedBeforeWriting()
    {
        var destination = directory + "-blocked";
        var previous = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await DemoSetup.CreateAsync(destination, root));
            Assert.That(Directory.Exists(destination), Is.False);
        }
        finally { Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", previous); }
        if (OperatingSystem.IsWindows()) return; // Junction creation privileges vary on Windows.
        Directory.CreateSymbolicLink(destination, root);
        try { Assert.Throws<ArgumentException>(() => DemoSetup.ValidateDirectory(Path.Combine(destination, "demo"), root)); }
        finally { Directory.Delete(destination); }
    }

    [Test]
    public async Task ElevenMigrationDemoIsRejectedWithoutChangingFiles()
    {
        var legacy = directory + "-legacy";
        Directory.CreateDirectory(legacy);
        try
        {
            var legacyConnection = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(legacy, "soundbite-demo.db"), Pooling = false
            }.ToString();
            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(legacyConnection).Options))
            {
                await db.GetService<IMigrator>().MigrateAsync("20261017013935_AddNotifications");
                Assert.That((await db.Database.GetAppliedMigrationsAsync()).Count(), Is.EqualTo(11));
            }
            File.Copy(Path.Combine(directory, "demo-manifest.txt"), Path.Combine(legacy, "demo-manifest.txt"));
            File.Copy(Path.Combine(directory, "credentials.json"), Path.Combine(legacy, "credentials.json"));
            var hashes = Directory.GetFiles(legacy).ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
            var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await DemoSetup.CreateAsync(legacy, root));
            Assert.That(error!.Message, Does.Contain("11 recorded migrations; 13 expected")
                .And.Contain("AddNotificationOptOuts").And.Contain("AddDemoReviews")
                .And.Contain("NEW external directory").And.Contain("ConnectionStrings__DefaultConnection"));
            Assert.That(Directory.GetFiles(legacy), Is.EquivalentTo(hashes.Keys), "The read-only check must not leave new files.");
            foreach (var (path, hash) in hashes)
                Assert.That(SHA256.HashData(await File.ReadAllBytesAsync(path)), Is.EqualTo(hash), Path.GetFileName(path));
        }
        finally { Directory.Delete(legacy, true); }
    }

    [TestCase("-wal")]
    [TestCase("-journal")]
    public async Task PendingJournalIsRejectedWithoutIgnoringOrChangingIt(string suffix)
    {
        var pending = directory + "-pending";
        Directory.CreateDirectory(pending);
        try
        {
            foreach (var name in new[] { "soundbite-demo.db", "credentials.json", "demo-manifest.txt" })
                File.Copy(Path.Combine(directory, name), Path.Combine(pending, name));
            var journal = Path.Combine(pending, "soundbite-demo.db" + suffix);
            byte[] sentinel = [1, 2, 3, 4];
            await File.WriteAllBytesAsync(journal, sentinel);
            var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await DemoSetup.CreateAsync(pending, root));
            Assert.That(error!.Message, Does.Contain("uncheckpointed").And.Contain("Stop SoundBite"));
            Assert.That(await File.ReadAllBytesAsync(journal), Is.EqualTo(sentinel));
        }
        finally { Directory.Delete(pending, true); }
    }

}
