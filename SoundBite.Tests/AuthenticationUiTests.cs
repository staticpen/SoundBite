using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoundBite.Data;
using SoundBite.Services;

namespace SoundBite.Tests;

public class AuthenticationUiTests
{
    private WebApplicationFactory<Program> app = null!;
    private HttpClient client = null!;
    private string directory = null!;

    [SetUp]
    public async Task SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "soundbite-auth-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = $"Data Source={directory}/test.db;Pooling=False" }));
        });
        client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        db.Tracks.Add(new SoundBite.Models.Track { Title = "Authentication test track" });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await app.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    private async Task<HttpResponseMessage> PostForm(string path, string handler, params (string Key, string Value)[] values)
    {
        var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.That(token, Is.Not.Empty, "SSR forms must carry an antiforgery token.");
        var fields = values.ToDictionary(v => v.Key, v => v.Value);
        fields["_handler"] = handler;
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private Task<HttpResponseMessage> Register(string confirm = "TestPass123!") => PostForm("/register", "register",
        ("model.UserName", "authuser"), ("model.Email", "authuser@example.com"),
        ("model.Password", "TestPass123!"), ("model.ConfirmPassword", confirm));

    [Test]
    public async Task LoginPageUsesCenteredCardLayoutLikeRegistration()
    {
        var login = await client.GetStringAsync("/login");
        var register = await client.GetStringAsync("/register");

        Assert.That(login, Does.Contain("class=\"login-container\"")
            .And.Contain("class=\"login-card\"")
            .And.Not.Contain("<h1>Account</h1>"));
        Assert.That(register, Does.Contain("class=\"register-container\"")
            .And.Contain("class=\"register-card\""));
    }

    private async Task<HttpResponseMessage> Logout()
    {
        var home = await client.GetStringAsync("/");
        var token = Regex.Match(home, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.That(token, Is.Not.Empty, "The navbar logout form must carry an antiforgery token.");
        return await client.PostAsync("/account/logout", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
            }));
    }

    // Scope navbar assertions to the header so homepage content (hero, feature cards) cannot satisfy them.
    private static string NavbarOf(string html)
    {
        var navbar = Regex.Match(html, "<header class=\"site-header\"[^>]*>.*?</header>", RegexOptions.Singleline);
        Assert.That(navbar.Success, Is.True, "The page must render the site header navbar.");
        return navbar.Value;
    }

    private static string HeroOf(string html)
    {
        var hero = Regex.Match(html, "<section id=\"home-hero\".*?</section>", RegexOptions.Singleline);
        Assert.That(hero.Success, Is.True, "The homepage must render the #home-hero section.");
        return hero.Value;
    }

    private static void AssertInOrder(string html, params string[] fragments)
    {
        var position = -1;
        foreach (var fragment in fragments)
        {
            var next = html.IndexOf(fragment, position + 1, StringComparison.Ordinal);
            Assert.That(next, Is.GreaterThan(position), $"Expected \"{fragment}\" after the previous navbar item.");
            position = next;
        }
    }

    [Test]
    public async Task NavbarShowsDestinationsForSignedOutSignedInAndAdministratorStates()
    {
        var signedOut = await client.GetStringAsync("/");
        var signedOutNavbar = NavbarOf(signedOut);
        Assert.That(signedOutNavbar, Does.Contain("<a href=\"/\" class=\"nav-link active\" aria-current=\"page\">Home</a>")
            .And.Contain("<a href=\"search\" class=\"nav-link\">Search</a>")
            .And.Contain("<a href=\"register\" class=\"nav-link\">Create account</a>")
            .And.Contain("<a href=\"login\" class=\"btn btn-primary nav-cta\">Log in</a>")
            .And.Contain("<a class=\"brand\" href=\"/\" aria-label=\"SoundBite home\"")
            .And.Contain("aria-controls=\"site-nav-menu\"").And.Contain("aria-expanded=\"false\"")
            .And.Not.Contain("href=\"feed\"")
            .And.Not.Contain("href=\"rankings\"").And.Not.Contain("href=\"playlists\"")
            .And.Not.Contain("href=\"favourites\"").And.Not.Contain("/account/logout").And.Not.Contain("admin/users")
            .And.Not.Contain("href=\"notifications\"").And.Not.Contain("soundbite-logo.svg"));
        Assert.That(Regex.Matches(signedOutNavbar, "class=\"nav-link(?: active)?\"").Count, Is.EqualTo(3),
            "Only Home, Search and Create account are secondary links; Log in is the primary button.");
        AssertInOrder(signedOutNavbar, ">Home</a>", ">Search</a>", ">Create account</a>", ">Log in</a>");
        Assert.That(signedOut, Does.Not.Contain("/account/logout"));

        foreach (var (pattern, description) in new[]
        {
            ("src=\"([^\"]*soundbite-logo[^\"]*\\.png)\"", "logo"),
            ("src=\"([^\"]*site-nav[^\"]*\\.js)\"", "navbar script")
        })
        {
            var url = Regex.Match(signedOut, pattern).Groups[1].Value;
            Assert.That(url, Is.Not.Empty, $"The navbar {description} must be referenced by the page.");
            var asset = await client.GetAsync(WebUtility.HtmlDecode(url));
            Assert.That(asset.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"The navbar {description} must be served.");
        }

        await Register();
        var signedInHome = await client.GetStringAsync("/");
        var signedIn = NavbarOf(signedInHome);
        Assert.That(signedIn, Does.Contain("href=\"/\" class=\"nav-link active\" aria-current=\"page\">Home</a>")
            .And.Contain("href=\"search\" class=\"nav-link\">Search</a>")
            .And.Contain("href=\"rankings\" class=\"nav-link\">Ratings and Trends</a>")
            .And.Contain("href=\"favourites\" class=\"nav-link\">Favourites</a>")
            .And.Not.Contain("href=\"feed\"").And.Not.Contain("href=\"playlists\"")
            .And.Contain(">Account</summary>")
            .And.Contain("href=\"account\" class=\"nav-link\">Edit Profile</a>").And.Contain("href=\"notifications\"")
            .And.Contain("action=\"/account/logout\"").And.Contain(">Log out</button>")
            .And.Contain("btn-outline-secondary").And.Not.Contain("btn-outline-light")
            .And.Not.Contain("href=\"register\"")
            .And.Not.Contain("href=\"login\"").And.Not.Contain("admin/users"));
        AssertInOrder(signedIn, ">Home</a>", ">Search</a>",
            ">Ratings and Trends</a>", ">Favourites</a>", ">Account</summary>");

        using (var scope = app.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await roles.CreateAsync(new IdentityRole(AdminService.AdministratorRole));
            Assert.That(created.Succeeded, Is.True, string.Join("; ", created.Errors.Select(e => e.Description)));
            var user = await users.FindByNameAsync("authuser");
            Assert.That(user, Is.Not.Null);
            var added = await users.AddToRoleAsync(user!, AdminService.AdministratorRole);
            Assert.That(added.Succeeded, Is.True, string.Join("; ", added.Errors.Select(e => e.Description)));
        }

        await Logout();
        var login = await PostForm("/login", "login", ("loginModel.UserNameOrEmail", "authuser"), ("loginModel.Password", "TestPass123!"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(NavbarOf(await client.GetStringAsync("/")), Does.Contain("href=\"admin/users\"")
            .And.Contain("action=\"/account/logout\""));
    }

    [Test]
    public async Task HomepageHeroPromotesLogInWhenSignedOutAndCatalogueSearchWhenSignedIn()
    {
        var signedOutHome = await client.GetStringAsync("/");
        Assert.That(HeroOf(signedOutHome), Does.Contain("<a class=\"btn btn-primary btn-lg\" href=\"/login\">Log in</a>")
            .And.Contain("href=\"/register\">Create account</a>")
            .And.Not.Contain("Search the catalogue"));
        Assert.That(signedOutHome, Does.Not.Contain("activity-feed-heading")
            .And.Not.Contain("to see recent community activity"),
            "The anonymous homepage must show only the login-first hero, not the activity feed prompt.");

        await Register();
        var signedInHero = HeroOf(await client.GetStringAsync("/"));
        Assert.That(signedInHero, Does.Contain("Welcome, authuser.")
            .And.Contain("<a class=\"btn btn-primary btn-lg\" href=\"/search\">Search the catalogue</a>")
            .And.Not.Contain("href=\"/login\""));
    }

    [TestCase("/search")]
    [TestCase("/rankings")]
    public async Task DestinationsHiddenFromTheSignedOutNavbarRemainPublic(string path)
    {
        var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("/search")]
    [TestCase("/favourites")]
    public async Task SingleColumnPagesUseTheSharedCentredContentColumn(string path)
    {
        Assert.That(await client.GetStringAsync(path), Does.Contain("<div class=\"content-column\">"));
    }

    [Test]
    public async Task LoginFormUsesThePrimaryActionStyle()
    {
        Assert.That(await client.GetStringAsync("/login"), Does.Contain("class=\"btn btn-primary w-100\"")
            .And.Contain(">Log in</button>")
            .And.Not.Contain("btn-success"));
    }

    [Test]
    public async Task AnonymousFavouritesPageOnlyPromptsToLogIn()
    {
        var response = await client.GetAsync("/favourites");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var html = await response.Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("<a href=\"/login\">Log in</a> or <a href=\"/register\">sign up</a> to view and manage your favourites.")
            .And.Not.Contain("Add Tracks").And.Not.Contain("add-tracks-heading")
            .And.Not.Contain("favourite-search").And.Not.Contain("Add to Favourites")
            .And.Not.Contain(">Remove<").And.Not.Contain("Remove</button>"));
    }

    [TestCase("/missing-net9-page")]
    [TestCase("/missing/deep/link")]
    [TestCase("/feed")]
    [TestCase("/playlists")]
    public async Task UnknownRouteReturns404AndApplicationNotFoundContent(string path)
    {
        var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await response.Content.ReadAsStringAsync(),
            Does.Contain("Sorry, the content you are looking for does not exist."));
    }

    [Test]
    public async Task WebsiteRegistrationProfileLogoutAndLoginPersistCookie()
    {
        var registration = await Register();
        Assert.That(registration.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        var cookie = registration.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.That(cookie.ToLowerInvariant(), Does.Contain("secure").And.Contain("httponly").And.Contain("samesite=lax"));
        Assert.That(registration.Headers.Location!.OriginalString, Does.EndWith("/account"));
        Assert.That((await client.GetAsync("/api/account/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await client.GetStringAsync("/account"), Does.Contain("Signed in"));
        Assert.That(await client.GetStringAsync("/notifications"), Does.Contain("You don't have any notifications yet.")
            .And.Contain("Notifications"));
        var saved = await PostForm("/account", "profile", ("profileModel.DisplayName", "Updated name"), ("profileModel.Bio", "My music biography"));
        Assert.That(await saved.Content.ReadAsStringAsync(), Does.Contain("Profile saved."));
        Assert.That(await client.GetStringAsync("/account"), Does.Contain("Updated name").And.Contain("My music biography"));
        Assert.That(await client.GetStringAsync("/users/authuser"), Does.Contain("Updated name").And.Not.Contain("authuser@example.com"));
        Assert.That(await client.GetStringAsync("/favourites"), Does.Contain("Favourites").And.Contain("Add Tracks"));
        var home = await client.GetStringAsync("/");
        Assert.That(NavbarOf(home), Does.Contain("href=\"favourites\" class=\"nav-link\">Favourites</a>"));
        Assert.That(home, Does.Contain("Activity Feed")
            .And.Not.Contain("href=\"feed\"")
            .And.Not.Contain("href=\"playlists\"")
            .And.Not.Contain("Browse music")
            .And.Not.Contain("Discover playlists")
            .And.Not.Contain("Join the community")
            .And.Not.Contain("Ratings and trends"));
        Assert.That(await client.GetStringAsync("/tracks/1"), Does.Contain("Write a review"));
        var logout = await Logout();
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That((await client.GetAsync("/api/account/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await client.GetStringAsync("/notifications"), Does.Contain("Log in")
            .And.Not.Contain("You don't have any notifications yet."));
        Assert.That(await client.GetStringAsync("/favourites"), Does.Contain("Log in").And.Not.Contain("Remove"));
        Assert.That(await client.GetStringAsync("/tracks/1"), Does.Contain("to rate and review this track").And.Not.Contain("Submit review"));
        foreach (var identifier in new[] { "authuser", "authuser@example.com" })
        {
            var login = await PostForm("/login", "login", ("loginModel.UserNameOrEmail", identifier), ("loginModel.Password", "TestPass123!"));
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(await client.GetStringAsync("/account"), Does.Contain("Signed in"));
            await Logout();
        }
    }

    [Test]
    public async Task NavbarLogoutUsesAntiforgeryProtectedHttpPostAndClearsAuthenticationCookie()
    {
        await Register();
        var home = await client.GetStringAsync("/");
        Assert.That(home, Does.Contain("action=\"/account/logout\"")
            .And.Contain(">Log out</button>"));

        var token = Regex.Match(home, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.That(token, Is.Not.Empty, "The navbar logout form must carry an antiforgery token.");
        var logout = await client.PostAsync(
            "/account/logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
            }));

        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(logout.Headers.Location?.OriginalString, Is.EqualTo("/"));
        Assert.That((await client.GetAsync("/api/account/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var missingToken = await client.PostAsync("/account/logout", new FormUrlEncodedContent([]));
        Assert.That(missingToken.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PublicProfileFollowPanelUsesActualProfileIdAndHidesSelfAndAnonymousActions()
    {
        await Register();
        string actorId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            actorId = (await db.Users.SingleAsync()).Id;
            db.Users.Add(new ApplicationUser { Id = "other", UserName = "other", NormalizedUserName = "OTHER" });
            db.Follows.Add(new SoundBite.Models.Follow { FollowerId = actorId, FollowingId = "other" });
            await db.SaveChangesAsync();
        }
        var own = await client.GetStringAsync("/users/authuser");
        Assert.That(own, Does.Not.Contain(">Follow</button>").And.Not.Contain(">Unfollow</button>"));
        var other = await client.GetStringAsync("/users/other");
        Assert.That(other, Does.Contain("Followers (1)").And.Contain(">Unfollow</button>"));
        Assert.That(other, Does.Not.Contain("authuser@example.com"));
        await Logout();
        var anonymous = await client.GetStringAsync("/users/other");
        Assert.That(anonymous, Does.Contain("Followers (1)").And.Contain("Log in to follow")
            .And.Not.Contain(">Follow</button>").And.Not.Contain(">Unfollow</button>"));
    }

    [Test]
    public async Task NotificationsPageShowsRecipientActivityAndUnreadNavigationCount()
    {
        await Register();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recipient = await db.Users.SingleAsync();
            var actor = new ApplicationUser { Id = "other", UserName = "other", NormalizedUserName = "OTHER" };
            db.Users.Add(actor);
            db.Notifications.Add(new SoundBite.Models.Notification
            {
                RecipientUserId = recipient.Id,
                ActorUserId = actor.Id,
                Type = SoundBite.Models.NotificationType.Follow
            });
            await db.SaveChangesAsync();
        }

        var notifications = await client.GetStringAsync("/notifications");
        Assert.That(notifications, Does.Contain("other").And.Contain("followed you.")
            .And.Contain("1 unread")
            .And.Contain("Mark all as read"));
    }

    [Test]
    public async Task InvalidCredentialsAndRegistrationAreRejected()
    {
        var mismatch = await Register("different");
        Assert.That(await mismatch.Content.ReadAsStringAsync(), Does.Contain("Your passwords do not match").And.Not.Contain("TestPass123!"));
        Assert.That((await client.GetAsync("/api/account/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await Register();
        await Logout();
        var duplicate = await Register();
        Assert.That(await duplicate.Content.ReadAsStringAsync(), Does.Contain("already taken").And.Not.Contain("TestPass123!"));
        var login = await PostForm("/login", "login", ("loginModel.UserNameOrEmail", "authuser"), ("loginModel.Password", "WrongPassword!"));
        Assert.That(await login.Content.ReadAsStringAsync(), Does.Contain("Invalid username or password").And.Not.Contain("WrongPassword!"));
        Assert.That((await client.GetAsync("/api/account/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task LoginIgnoresExternalReturnUrl()
    {
        await Register();
        await Logout();
        var response = await PostForm("/login?ReturnUrl=https%3A%2F%2Fevil.example", "login",
            ("loginModel.UserNameOrEmail", "authuser"), ("loginModel.Password", "TestPass123!"),
            ("ReturnUrl", "//evil.example"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location!.OriginalString, Is.EqualTo("https://localhost/account"));
    }

    [Test]
    public async Task AnonymousVisitorCannotReadAdminUserDirectory()
    {
        var response = await client.GetAsync("/admin/users");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var html = await response.Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("Administrator access is required")
            .And.Not.Contain("authuser@example.com")
            .And.Not.Contain("Search by username, display name, or email"));
    }

    [Test]
    public async Task InvalidProfileDoesNotOverwriteSavedValues()
    {
        await Register();
        await PostForm("/account", "profile", ("profileModel.DisplayName", "Original"), ("profileModel.Bio", "Original bio"));
        var response = await PostForm("/account", "profile", ("profileModel.DisplayName", new string('x', 81)), ("profileModel.Bio", "Changed"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("maximum length"));
        Assert.That(await client.GetStringAsync("/account"), Does.Contain("Original bio"));
    }

    [Test]
    public async Task AnonymousProfilePostCannotChangeAnAccount()
    {
        await Register();
        await Logout();
        var response = await PostForm("/account", "profile", ("profileModel.DisplayName", "Forged name"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await client.GetStringAsync("/users/authuser"), Does.Not.Contain("Forged name"));
    }

    [TestCase("/register", "register")]
    [TestCase("/login", "login")]
    [TestCase("/account/logout", "logout")]
    [TestCase("/account", "profile")]
    public async Task FormsRejectMissingAntiforgeryToken(string path, string handler)
    {
        var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> { ["_handler"] = handler }));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
