using System.Reflection;
using Microsoft.AspNetCore.Components;
using SoundBite.Components;
using SoundBite.Components.Pages;

namespace SoundBite.Tests;

public class PageRouteTests
{
    [Test]
    public void PageRoutes_HaveNoDuplicateTemplates()
    {
        var routes = typeof(App).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>()
                .Select(route => new { route.Template, Component = type.FullName }))
            .ToArray();

        Assert.That(routes, Is.Not.Empty, "The application must expose page routes.");
        var duplicates = routes
            .GroupBy(route => route.Template.TrimEnd('/'), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(route => route.Component))}");

        Assert.That(duplicates, Is.Empty, "Blazor page routes must be unique.");
    }

    [TestCase("/register", typeof(Register))]
    [TestCase("/login", typeof(Account))]
    [TestCase("/account", typeof(Account))]
    [TestCase("/favourites", typeof(Favourites))]
    [TestCase("/notifications", typeof(Notifications))]
    [TestCase("/notifications/settings", typeof(NotificationSettings))]
    [TestCase("/rankings", typeof(Rankings))]
    [TestCase("/admin/users", typeof(AdminUsers))]
    [TestCase("/artists/{Id:int}", typeof(ArtistDetails))]
    [TestCase("/albums/{Id:int}", typeof(AlbumDetails))]
    public void AuthenticationRoute_HasExpectedOwner(string template, Type expectedComponent)
    {
        var owners = typeof(App).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes<RouteAttribute>()
                .Any(route => string.Equals(route.Template, template, StringComparison.OrdinalIgnoreCase)));

        Assert.That(owners, Is.EqualTo(new[] { expectedComponent }));
    }
}
