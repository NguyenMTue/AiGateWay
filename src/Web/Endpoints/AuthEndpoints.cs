using AiGateway.Infrastructure.Identity;
using AiGateway.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Web.Endpoints;

public record AuthLoginRequest(string Email, string Password);

public class AuthEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/auth";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Maps /auth/login, /auth/register, /auth/refresh, etc.
        groupBuilder.MapIdentityApi<ApplicationUser>();

        // Direct POST /auth endpoint for device / user authentication
        groupBuilder.MapPost("/", Authenticate);
    }

    public static async Task<IResult> Authenticate(
        [FromBody] AuthLoginRequest request,
        [FromServices] SignInManager<ApplicationUser> signInManager,
        [FromServices] UserManager<ApplicationUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest(new { error = "Email/Username and Password are required." });
        }

        var user = await userManager.FindByEmailAsync(request.Email)
                   ?? await userManager.FindByNameAsync(request.Email);

        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }
}

public class ApiAuthEndpoints : IEndpointGroup
{
    public static string? RoutePrefix => "/api/auth";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapIdentityApi<ApplicationUser>();
        groupBuilder.MapPost("/", AuthEndpoints.Authenticate);
    }
}
