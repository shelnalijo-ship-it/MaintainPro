using MaintainPro.Application.Identity;
using MaintainPro.Application.Users;

namespace MaintainPro.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapPost("/login", async (LoginRequest request, AuthService service, CancellationToken ct) =>
            TypedResults.Ok(await service.LoginAsync(request, ct)))
            .AllowAnonymous().RequireRateLimiting("auth").WithSummary("Sign in with email or employee ID")
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(503);
        group.MapPost("/refresh", async (RefreshRequest request, AuthService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RefreshAsync(request, ct)))
            .AllowAnonymous().RequireRateLimiting("auth").WithSummary("Rotate a refresh token")
            .ProducesProblem(401).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/logout", async (RefreshRequest request, AuthService service, CancellationToken ct) =>
        {
            await service.LogoutAsync(request, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization().WithSummary("Revoke the supplied session").ProducesProblem(401);
        group.MapGet("/me", async (AuthService service, CancellationToken ct) =>
            TypedResults.Ok(await service.MeAsync(ct))).RequireAuthorization()
            .WithSummary("Read the current user and roles").ProducesProblem(401);
        group.MapPost("/change-password", async (ChangePasswordRequest request, AuthService service, CancellationToken ct) =>
        {
            await service.ChangePasswordAsync(request, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization().RequireRateLimiting("auth")
            .WithSummary("Change password and revoke all sessions").ProducesProblem(400).ProducesProblem(401);
    }
}
