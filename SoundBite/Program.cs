using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoundBite.Components;
using SoundBite.Configuration;
using SoundBite.Data;
using SoundBite.Endpoints;
using SoundBite.Interfaces;
using SoundBite.Services;
using SoundBite.Strategies;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<PlaylistService>();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<SocialInteractionService>();
builder.Services.AddScoped<FeedService>();
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

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpClient();

var app = builder.Build();

await AdminRoleBootstrapper.EnsureConfiguredAdministratorsAsync(
    app.Services,
    builder.Configuration.GetSection("Admin:UserIds").Get<string[]>() ?? []);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

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
