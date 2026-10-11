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
                UserManager<ApplicationUser> userManager) =>
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

                return Results.Ok(new
                {
                    message = "Account created successfully."
                });
            });

        return endpoints;
    }
}

public sealed record RegisterRequest(
    string UserName,
    string Email,
    string Password);

