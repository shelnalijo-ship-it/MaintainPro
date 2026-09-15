using MaintainPro.Application.Machines;
using MaintainPro.Api.Security;

namespace MaintainPro.Api.Endpoints;

public static class MachineEndpoints
{
    public static void MapMachineEndpoints(this IEndpointRouteBuilder app)
    {
        var machines = app.MapGroup("/api/v1/machines").WithTags("Machines");
        machines.MapGet("", async ([AsParameters] MachineQuery query, MachineService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)))
            .RequireAuthorization(Policies.ReadMachines).WithSummary("Search, filter, and page visible machines");
        machines.MapGet("/{id:guid}", async (Guid id, MachineService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)))
            .RequireAuthorization(Policies.ReadMachines).ProducesProblem(404);
        machines.MapGet("/{id:guid}/assignment-history", async (Guid id, MachineService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAssignmentHistoryAsync(id, ct)))
            .RequireAuthorization(Policies.ReadMachines).WithSummary("Read retained assignment history").ProducesProblem(404);
        machines.MapPost("", async (MachineWriteRequest request, MachineService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/v1/machines/{result.Id}", result);
        }).RequireAuthorization(Policies.ManageMachines).ProducesProblem(400).ProducesProblem(409);
        machines.MapPut("/{id:guid}", async (Guid id, MachineWriteRequest request, MachineService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageMachines).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        machines.MapPatch("/{id:guid}/status", async (Guid id, MachineStatusRequest request, MachineService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetStatusAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageMachines).WithSummary("Change status or deactivate a machine");
        machines.MapPost("/{id:guid}/assign-owner", async (Guid id, MachineAssignmentRequest request, MachineService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AssignOwnerAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageMachines)
            .WithSummary("Replace owner and supervisor; null clears that assignment");
    }
}
