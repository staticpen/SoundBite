using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SoundBite.Data;
using SoundBite.Services;

namespace SoundBite.Tests;

public sealed class AdminServiceTests
{
    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public string? UserId { get; set; }
        public bool IsAdministrator { get; set; }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var claims = new List<Claim>();
            if (UserId is not null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, UserId));
            if (IsAdministrator)
                claims.Add(new Claim(ClaimTypes.Role, AdminService.AdministratorRole));

            return Task.FromResult(new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity(claims, UserId is null ? null : "test"))));
        }
    }

    private SqliteConnection connection = null!;
    private ServiceProvider provider = null!;
    private TestAuthenticationStateProvider authentication = null!;

    [SetUp]
    public async Task SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        authentication = new TestAuthenticationStateProvider();
        provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection))
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .Services
            .AddSingleton<AuthenticationStateProvider>(authentication)
            .AddScoped<AdminService>()
            .BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            db.Users.AddRange(
                new ApplicationUser { Id = "admin-1", UserName = "admin", NormalizedUserName = "ADMIN", Email = "admin@example.com", DisplayName = "Admin" },
                new ApplicationUser { Id = "user-1", UserName = "listener", NormalizedUserName = "LISTENER", Email = "listener@example.com", DisplayName = "Listener" });
            await db.SaveChangesAsync();
        }

        await AdminRoleBootstrapper.EnsureConfiguredAdministratorsAsync(provider, ["admin-1"]);
        authentication.UserId = "admin-1";
        authentication.IsAdministrator = true;
    }

    [TearDown]
    public async Task TearDown()
    {
        await provider.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task ConfiguredAdministratorBootstrapIsIdempotentAndRejectsUnknownIds()
    {
        await AdminRoleBootstrapper.EnsureConfiguredAdministratorsAsync(provider, [" admin-1 ", "admin-1"]);
        using (var scope = provider.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await users.FindByIdAsync("admin-1");
            Assert.That(await roles.RoleExistsAsync(AdminService.AdministratorRole), Is.True);
            Assert.That(await users.IsInRoleAsync(admin!, AdminService.AdministratorRole), Is.True);
        }

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await AdminRoleBootstrapper.EnsureConfiguredAdministratorsAsync(provider, ["missing-user"]));
    }

    [Test]
    public async Task AdminCanSearchUsersAndLockAndUnlockAccounts()
    {
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();

        var page = await admin.GetUsersAsync("listener");
        Assert.That(page.TotalCount, Is.EqualTo(1));
        Assert.That(page.Users.Single().Email, Is.EqualTo("listener@example.com"));
        Assert.That(page.Users.Single().IsLocked, Is.False);

        await admin.SetUserLockedAsync("user-1", true);
        Assert.That((await admin.GetUsersAsync("listener")).Users.Single().IsLocked, Is.True);
        await admin.SetUserLockedAsync("user-1", false);
        Assert.That((await admin.GetUsersAsync("listener")).Users.Single().IsLocked, Is.False);
    }

    [Test]
    public async Task AdminCannotLockSelfOrTheLastActiveAdministrator()
    {
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await admin.SetUserLockedAsync("admin-1", true));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await admin.SetUserAdministratorAsync("admin-1", false));

        await admin.SetUserAdministratorAsync("user-1", true);
        authentication.UserId = "user-1";
        using (var otherAdminScope = provider.CreateScope())
        {
            var otherAdmin = otherAdminScope.ServiceProvider.GetRequiredService<AdminService>();
            await otherAdmin.SetUserLockedAsync("admin-1", true);
        }
        var usersAfterLock = (await admin.GetUsersAsync()).Users;
        Assert.That(usersAfterLock.Single(user => user.Id == "admin-1").IsLocked, Is.True);
        Assert.That(usersAfterLock.Single(user => user.Id == "user-1").IsAdministrator, Is.True);
        authentication.UserId = "admin-1";
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await admin.SetUserLockedAsync("user-1", true));
    }

    [Test]
    public async Task NonAdministratorsCannotReadOrChangeUsers()
    {
        authentication.UserId = "user-1";
        authentication.IsAdministrator = false;
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();

        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await admin.GetUsersAsync());
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await admin.SetUserLockedAsync("admin-1", true));
    }

    [Test]
    public async Task AdminCanGrantAndRevokeRolesButCannotDemoteSelf()
    {
        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminService>();

        await admin.SetUserAdministratorAsync("user-1", true);
        Assert.That((await admin.GetUsersAsync()).Users.Single(user => user.Id == "user-1").IsAdministrator, Is.True);
        await admin.SetUserAdministratorAsync("user-1", false);
        Assert.That((await admin.GetUsersAsync()).Users.Single(user => user.Id == "user-1").IsAdministrator, Is.False);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await admin.SetUserAdministratorAsync("admin-1", false));
    }
}
