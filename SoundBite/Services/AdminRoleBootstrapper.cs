using Microsoft.AspNetCore.Identity;
using SoundBite.Data;

namespace SoundBite.Services;

public static class AdminRoleBootstrapper
{
    public static async Task EnsureConfiguredAdministratorsAsync(
        IServiceProvider services,
        IEnumerable<string> configuredUserIds)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuredUserIds);

        var userIds = configuredUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (userIds.Length == 0)
            return;

        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AdminService.AdministratorRole))
        {
            var roleResult = await roleManager.CreateAsync(
                new IdentityRole(AdminService.AdministratorRole));
            EnsureSucceeded(roleResult);
        }

        foreach (var userId in userIds)
        {
            var user = await userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException(
                    $"Configured administrator user ID '{userId}' does not exist.");
            if (!await userManager.IsInRoleAsync(user, AdminService.AdministratorRole))
            {
                var result = await userManager.AddToRoleAsync(user, AdminService.AdministratorRole);
                EnsureSucceeded(result);
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Unable to provision the administrator role: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}
