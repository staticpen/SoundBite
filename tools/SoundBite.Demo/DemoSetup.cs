using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SoundBite.Data;

namespace SoundBite.Demo;

public static class DemoSetup
{
    private const string Marker = "SoundBite synthetic demo v1";

    public static async Task<bool> CreateAsync(string directory, string repositoryRoot)
    {
        if (new[] { "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT" }
            .Any(key => string.Equals(Environment.GetEnvironmentVariable(key), "Production", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Demo setup refuses an explicitly configured Production environment.");
        ValidateDirectory(directory, repositoryRoot);
        var database = Path.Combine(directory, "soundbite-demo.db");
        var credentials = Path.Combine(directory, "credentials.json");
        var marker = Path.Combine(directory, "demo-manifest.txt");
        if (Directory.Exists(directory))
        {
            if (File.Exists(marker) && await File.ReadAllTextAsync(marker) == Marker &&
                File.Exists(database) && File.Exists(credentials) &&
                !File.Exists(Path.Combine(directory, ".setup.lock")))
            {
                await CheckMigrationsAsync(database);
                return false;
            }
            throw new InvalidOperationException("Destination already exists and is not a completed demo. Choose a new directory; nothing was overwritten.");
        }
        // A separate directory is the reset boundary. Never delete or adopt an existing database.
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var lockPath = Path.Combine(directory, ".setup.lock");
        using var setupLock = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = database, Pooling = false, ForeignKeys = true }.ToString()));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
        }).AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Uses the app's unchanged migrations and pinned EF Core 9.0.21 under SDK 9.0.319.
        await db.Database.MigrateAsync();
        var logins = await DemoSeed.PopulateAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var output = new FileStream(credentials, options))
            await JsonSerializer.SerializeAsync(output, logins, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(marker, Marker);
        setupLock.Dispose();
        File.Delete(lockPath);
        return true;
    }

    private static async Task CheckMigrationsAsync(string database)
    {
        // Never migrate, seed, or open in read/write mode on a rerun.
        // Even SQLite read-only mode can create WAL/SHM files. Immutable mode avoids
        // that, but must not ignore uncheckpointed transactions in a live database.
        if (new[] { database + "-wal", database + "-journal" }
            .Any(path => File.Exists(path) && new FileInfo(path).Length > 0))
            throw new InvalidOperationException(
                "Cannot safely inspect an active or uncheckpointed demo. Stop SoundBite cleanly and retry, " +
                "or use --directory with a NEW external directory. The existing files were not changed.");
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = new Uri(Path.GetFullPath(database)).AbsoluteUri + "?immutable=1",
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        var expected = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(applied))
        {
            var missing = expected.Except(applied).ToArray();
            var unknown = applied.Except(expected).ToArray();
            throw new InvalidOperationException(
                $"Existing demo database is outdated or incompatible ({applied.Count} recorded migrations; {expected.Count} expected). " +
                $"Missing: {string.Join(", ", missing)}. Unknown: {string.Join(", ", unknown)}. " +
                "No migrations or seed changes were applied. Stop SoundBite and rerun this command with --directory pointing to a NEW external directory. " +
                "Then update ConnectionStrings__DefaultConnection to its soundbite-demo.db and use its new credentials.json. Keep the old directory as a backup.");
        }
    }

    public static void ValidateDirectory(string directory, string repositoryRoot)
    {
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("Use an absolute destination outside the repository.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Demo databases and credentials must remain outside the repository.");
        // Reject aliases, including symlink/junction ancestors, before creating any files.
        for (var current = new DirectoryInfo(full); current != null; current = current.Parent)
            if (current.LinkTarget != null || (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new ArgumentException("Use a physical path without symbolic links or junctions.");
    }
}
