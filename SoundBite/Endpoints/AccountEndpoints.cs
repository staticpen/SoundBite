using Microsoft.AspNetCore.Identity;
using SoundBite.Data;

namespace SoundBite.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/account/register",
            async (
                RegisterRequest request,
                UserManager<ApplicationUser> userManager,
                SignInManager<ApplicationUser> signInManager) =>
            {
                if (string.IsNullOrWhiteSpace(request.UserName) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest(new
                    {
                        message = "All fields are required."
                    });
                }

                var user = new ApplicationUser
                {
                    UserName = request.UserName.Trim(),
                    Email = request.Email.Trim()
                };

                var result = await userManager.CreateAsync(
                    user, request.Password);

                if (!result.Succeeded)
                {
                    return Results.ValidationProblem(
                        result.Errors
                            .GroupBy(error => error.Code)
                            .ToDictionary(
                                group => group.Key,
                                group => group
                                    .Select(error => error.Description)
                                    .ToArray()));
                }

                await signInManager.SignInAsync(user, isPersistent: false);

                return Results.Ok(new
                {
                    message = "Account created successfully.",
                    userName = user.UserName,
                    email = user.Email
                });
            });

        endpoints.MapPost("/api/account/login",
            async (
                LoginRequest request,
                SignInManager<ApplicationUser> signInManager,
                UserManager<ApplicationUser> userManager) =>
            {
                if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest(new
                    {
                        message = "Username/email and password are required."
                    });
                }

                var user = await userManager.FindByNameAsync(request.UserNameOrEmail)
                    ?? await userManager.FindByEmailAsync(request.UserNameOrEmail);

                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var result = await signInManager.PasswordSignInAsync(
                    user,
                    request.Password,
                    isPersistent: false,
                    lockoutOnFailure: false);

                if (!result.Succeeded)
                    return Results.Unauthorized();

                return Results.Ok(new
                {
                    message = "Signed in successfully.",
                    userName = user.UserName,
                    email = user.Email
                });
            });

        endpoints.MapPost("/api/account/logout",
            async (SignInManager<ApplicationUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.Ok(new { message = "Signed out successfully." });
            });

        endpoints.MapGet("/api/account/me",
            async (
                HttpContext context,
                UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.GetUserAsync(context.User);

                if (user is null)
                    return Results.Unauthorized();

                return Results.Ok(new
                {
                    userName = user.UserName,
                    email = user.Email
                });
            });

        return endpoints;
    }
}

public sealed record RegisterRequest(
    string UserName,
    string Email,
    string Password);

public sealed record LoginRequest(
    string UserNameOrEmail,
    string Password);

