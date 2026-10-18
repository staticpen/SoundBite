using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoundBite.Components;
using SoundBite.Configuration;
using SoundBite.Data;
using SoundBite.Endpoints;
using SoundBite.Interfaces;
using SoundBite.Services;
using SoundBite.Strategies;

var seedCatalogue = false;
var seedQueries = new List<string>();
var applicationArgs = new List<string>();
for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--seed-catalogue")
    {
        seedCatalogue = true;
    }
    else if (args[index] == "--seed-query")
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
            throw new ArgumentException("--seed-query requires a non-empty search term.");

        seedQueries.Add(args[index]);
    }
    else
    {
        applicationArgs.Add(args[index]);
    }
}

if (!seedCatalogue && seedQueries.Count > 0)
    throw new ArgumentException("--seed-query can only be used with --seed-catalogue.");

var builder = WebApplication.CreateBuilder(applicationArgs.ToArray());

var externalConfigurationFile =
    Environment.GetEnvironmentVariable("SOUNDBITE_CONFIG_FILE");
if (!string.IsNullOrWhiteSpace(externalConfigurationFile))
{
    builder.Configuration.AddJsonFile(
        externalConfigurationFile,
        optional: false,
        reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(applicationArgs.ToArray());
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IMusicCatalog, CatalogueService>();
builder.Services.Configure<SpotifyOptions>(
    builder.Configuration.GetSection(SpotifyOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SpotifyAccessTokenProvider>();
builder.Services.AddHttpClient("SpotifyAuth", client =>
    client.BaseAddress = new Uri("https://accounts.spotify.com/"));
builder.Services.AddHttpClient<ISpotifyCatalog, SpotifyCatalogService>(client =>
    client.BaseAddress = new Uri("https://api.spotify.com/v1/"));
builder.Services.AddScoped<SpotifyTrackImporter>();
builder.Services.AddScoped<SpotifyCatalogueSeeder>();
builder.Services.AddScoped<DemoReviewSeeder>();
builder.Services.AddScoped<DemoReviewService>();
builder.Services.AddScoped<SpotifyArtistCatalogService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<FavouriteService>();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<SocialInteractionService>();
builder.Services.AddScoped<FeedService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton<NotificationEventDispatcher>();
builder.Services.AddScoped<NotificationCircuitHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler>(
    services => services.GetRequiredService<NotificationCircuitHandler>());
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<IRankingStrategy, RatingRankingStrategy>();
builder.Services.AddScoped<IRankingStrategy, PopularityRankingStrategy>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddScoped<TrendService>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme =
            IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme =
            IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseForwardedHeaders();

if (seedCatalogue)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Catalogue seeding requires an explicitly configured SQLite connection string.");

    var databasePath = new SqliteConnectionStringBuilder(connectionString).DataSource;
    if (string.IsNullOrWhiteSpace(databasePath) ||
        databasePath == ":memory:" ||
        !Path.IsPathRooted(databasePath))
    {
        throw new InvalidOperationException(
            "Catalogue seeding requires an absolute SQLite database path. Set ConnectionStrings__DefaultConnection explicitly to protect the repository database.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<SpotifyCatalogueSeeder>();
    var result = await seeder.SeedAsync(seedQueries.Count == 0 ? null : seedQueries);
    Console.WriteLine(
        $"Spotify catalogue seed complete: {result.QueriesCompleted} queries, {result.TracksImported} unique tracks imported or refreshed.");
    var demoReviewResult = await scope.ServiceProvider
        .GetRequiredService<DemoReviewSeeder>()
        .SeedAsync();
    Console.WriteLine(
        $"Demonstration rankings seeded: {demoReviewResult.TracksSeeded} tracks, {demoReviewResult.ReviewsSeeded} sample reviews.");
    return;
}

await AdminRoleBootstrapper.EnsureConfiguredAdministratorsAsync(
    app.Services,
    builder.Configuration.GetSection("Admin:UserIds").Get<string[]>() ?? []);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapAccountEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Expose the entry point to HTTP integration tests.
public partial class Program { }
