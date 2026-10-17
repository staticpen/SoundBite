using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoundBite.Data;
using SoundBite.Models;

namespace SoundBite.Services;

public sealed class AdminService(
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    AuthenticationStateProvider authenticationStateProvider)
{
    public const string AdministratorRole = "Administrator";
    public const int PageSize = 25;
    public const int MaximumPage = int.MaxValue / PageSize - 1;

    public async Task<AdminUserPage> GetUsersAsync(
        string? search = null,
        int page = 0,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page);
        await RequireAdministratorAsync();
        cancellationToken.ThrowIfCancellationRequested();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var users = db.Users.AsNoTracking();
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            users = users.Where(user =>
                (user.UserName != null && user.UserName.Contains(term)) ||
                (user.Email != null && user.Email.Contains(term)) ||
                user.DisplayName.Contains(term));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var usersOnPage = await users.OrderBy(user => user.UserName)
            .ThenBy(user => user.Id)
            .Skip(page * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var administratorRoleId = await db.Roles
            .Where(role => role.Name == AdministratorRole)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var administratorIds = administratorRoleId is null
            ? new HashSet<string>()
            : await db.Set<IdentityUserRole<string>>()
                .Where(userRole => userRole.RoleId == administratorRoleId)
                .Select(userRole => userRole.UserId)
                .ToHashSetAsync(cancellationToken);
        var results = new List<AdminUserSummary>(usersOnPage.Count);
        foreach (var user in usersOnPage)
        {
            results.Add(new AdminUserSummary(
                user.Id,
                user.UserName ?? string.Empty,
                user.DisplayName,
                user.Email,
                user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow,
                administratorIds.Contains(user.Id)));
        }

        return new AdminUserPage(results, totalCount, page, totalCount > (page + 1) * PageSize);
    }

    public async Task SetUserLockedAsync(
        string targetUserId,
        bool locked,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);
        var actorId = await RequireAdministratorAsync();
        if (actorId == targetUserId && locked)
            throw new InvalidOperationException("You cannot lock your own administrator account.");

        cancellationToken.ThrowIfCancellationRequested();
        var target = await userManager.FindByIdAsync(targetUserId)
            ?? throw new KeyNotFoundException("The requested user was not found.");

        if (locked)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var roleId = await GetAdministratorRoleIdAsync(db, cancellationToken);
            if (roleId is not null && await db.Set<IdentityUserRole<string>>()
                    .AnyAsync(userRole => userRole.UserId == target.Id &&
                                         userRole.RoleId == roleId, cancellationToken))
            {
                var administratorIds = await db.Set<IdentityUserRole<string>>()
                    .Where(userRole => userRole.RoleId == roleId && userRole.UserId != target.Id)
                    .Select(userRole => userRole.UserId)
                    .ToListAsync(cancellationToken);
                var otherAdministrators = await db.Users.AsNoTracking()
                    .Where(user => administratorIds.Contains(user.Id))
                    .Select(user => user.LockoutEnd)
                    .ToListAsync(cancellationToken);
                var hasOtherActiveAdministrator = otherAdministrators.Any(lockoutEnd =>
                    lockoutEnd is null || lockoutEnd <= DateTimeOffset.UtcNow);
                if (!hasOtherActiveAdministrator)
                    throw new InvalidOperationException("The last active administrator cannot be locked.");
            }
        }

        EnsureSucceeded(await userManager.SetLockoutEnabledAsync(target, true));
        EnsureSucceeded(await userManager.SetLockoutEndDateAsync(
            target,
            locked ? DateTimeOffset.MaxValue : null));
    }

    public async Task SetUserAdministratorAsync(
        string targetUserId,
        bool administrator,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserId);
        var actorId = await RequireAdministratorAsync();
        if (actorId == targetUserId && !administrator)
            throw new InvalidOperationException("You cannot remove your own administrator role.");

        cancellationToken.ThrowIfCancellationRequested();
        var target = await userManager.FindByIdAsync(targetUserId)
            ?? throw new KeyNotFoundException("The requested user was not found.");
        var isAdministrator = await userManager.IsInRoleAsync(target, AdministratorRole);
        if (isAdministrator == administrator)
            return;

        if (!administrator)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var roleId = await GetAdministratorRoleIdAsync(db, cancellationToken);
            var administratorCount = await db.Set<IdentityUserRole<string>>()
                .CountAsync(userRole => roleId != null && userRole.RoleId == roleId, cancellationToken);
            if (administratorCount <= 1)
                throw new InvalidOperationException("The last administrator cannot be demoted.");
        }

        var result = administrator
            ? await userManager.AddToRoleAsync(target, AdministratorRole)
            : await userManager.RemoveFromRoleAsync(target, AdministratorRole);
        EnsureSucceeded(result);
    }

    private async Task<string> RequireAdministratorAsync()
    {
        var principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        var actorId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
        if (string.IsNullOrWhiteSpace(actorId))
            throw new UnauthorizedAccessException("Administrator access is required.");

        await using var db = await contextFactory.CreateDbContextAsync();
        var roleId = await GetAdministratorRoleIdAsync(db, CancellationToken.None);
        var actor = await db.Users.AsNoTracking()
            .Where(user => user.Id == actorId)
            .Select(user => new { user.Id, user.LockoutEnd })
            .SingleOrDefaultAsync();
        var isAdministrator = roleId is not null && await db.Set<IdentityUserRole<string>>()
            .AnyAsync(userRole => userRole.UserId == actorId && userRole.RoleId == roleId);
        var isActiveAdministrator = actor is not null &&
                                    (actor.LockoutEnd is null || actor.LockoutEnd <= DateTimeOffset.UtcNow) &&
                                    isAdministrator;
        if (!isActiveAdministrator)
            throw new UnauthorizedAccessException("The administrator account is no longer available.");

        return actorId;
    }

    private static Task<string?> GetAdministratorRoleIdAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken) =>
        db.Roles.Where(role => role.Name == AdministratorRole)
            .Select(role => role.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private static void ValidatePage(int page)
    {
        if (page is < 0 or > MaximumPage)
            throw new ArgumentOutOfRangeException(nameof(page));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"The identity operation failed: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}

public sealed record AdminUserSummary(
    string Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsLocked,
    bool IsAdministrator);

public sealed record AdminUserPage(
    IReadOnlyList<AdminUserSummary> Users,
    int TotalCount,
    int Page,
    bool HasNextPage);
