using MaintainPro.Application.Users;
using MaintainPro.Api.Security;
using MaintainPro.Domain.Security;

namespace MaintainPro.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/v1/users").WithTags("Users");
        users.MapGet("/technicians", async (UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.LookupAsync(RoleNames.Technician, ct)))
            .RequireAuthorization(Policies.ManageMachines).WithSummary("List active technician choices");
        users.MapGet("/supervisors", async (UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.LookupAsync(RoleNames.Supervisor, ct)))
            .RequireAuthorization(Policies.ManageMachines).WithSummary("List active supervisor choices");
        var admin = users.MapGroup("").RequireAuthorization(Policies.ManageUsers);
        admin.MapGet("", async ([AsParameters] UserListQuery query, UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct))).WithSummary("Search and page users");
        admin.MapGet("/{id:guid}", async (Guid id, UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct))).ProducesProblem(404);
        admin.MapPost("", async (CreateUserRequest request, UserService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/v1/users/{result.Id}", result);
        }).WithSummary("Create a user with one or more roles").ProducesProblem(400).ProducesProblem(409);
        admin.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(id, request, ct))).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        admin.MapPatch("/{id:guid}/status", async (Guid id, UserStatusRequest request, UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetStatusAsync(id, request, ct))).WithSummary("Activate or deactivate a user");
        admin.MapPut("/{id:guid}/roles", async (Guid id, UserRolesRequest request, UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetRolesAsync(id, request, ct))).WithSummary("Replace the user's role memberships");
    }
}
