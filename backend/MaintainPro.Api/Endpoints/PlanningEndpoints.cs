using MaintainPro.Api.Security;
using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;

namespace MaintainPro.Api.Endpoints;

public static class PlanningEndpoints
{
    public static void MapPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        var types = app.MapGroup("/api/v1/maintenance-types").WithTags("Maintenance types")
            .RequireAuthorization(Policies.ManagePlanning);
        types.MapGet("", async ([AsParameters] MaintenanceTypeQuery query, MaintenanceTypeService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)));
        types.MapPost("", async (MaintenanceTypeWriteRequest request, MaintenanceTypeService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateAsync(request, ct)))
            .ProducesProblem(400).ProducesProblem(409);
        types.MapPut("/{id:guid}", async (Guid id, MaintenanceTypeWriteRequest request, MaintenanceTypeService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(id, request, ct))).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        types.MapPatch("/{id:guid}/status", async (Guid id, MaintenanceTypeStatusRequest request, MaintenanceTypeService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetStatusAsync(id, request, ct)));

        var plans = app.MapGroup("/api/v1/maintenance-plans").WithTags("Maintenance plans");
        plans.MapGet("", async ([AsParameters] PlanQuery query, MaintenancePlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct))).RequireAuthorization(Policies.ReadPlanning)
            .WithSummary("Search and page permitted maintenance plans");
        plans.MapGet("/{id:guid}", async (Guid id, MaintenancePlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct))).RequireAuthorization(Policies.ReadPlanning).ProducesProblem(404);
        plans.MapGet("/{id:guid}/checklist", async (Guid id, int? version, MaintenancePlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetChecklistAsync(id, version, ct))).RequireAuthorization(Policies.ReadPlanning)
            .WithSummary("Read the current checklist or a retained version").ProducesProblem(404);
        var manage = plans.MapGroup("").RequireAuthorization(Policies.ManagePlanning);
        manage.MapPost("", async (PlanWriteRequest request, MaintenancePlanService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/v1/maintenance-plans/{result.Id}", result);
        }).ProducesProblem(400).ProducesProblem(409);
        manage.MapPut("/{id:guid}", async (Guid id, PlanWriteRequest request, MaintenancePlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(id, request, ct)))
            .WithSummary("Edit future occurrences without changing generated work").ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPost("/{id:guid}/duplicate", async (Guid id, DuplicatePlanRequest request, MaintenancePlanService service, CancellationToken ct) =>
        {
            var result = await service.DuplicateAsync(id, request, ct);
            return TypedResults.Created($"/api/v1/maintenance-plans/{result.Id}", result);
        }).WithSummary("Duplicate a plan and current checklist into a new schedule");
        manage.MapPatch("/{id:guid}/status", async (Guid id, PlanStatusRequest request, MaintenancePlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetStatusAsync(id, request, ct)));
        manage.MapPut("/{id:guid}/checklist", async (Guid id, ChecklistWriteRequest request, MaintenancePlanService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetChecklistAsync(id, request, ct)))
            .WithSummary("Create a new immutable checklist version").ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPost("/generate-due", async (GenerationRequest? request, WorkOrderGenerationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GenerateDueAsync(request, ct)))
            .WithSummary("Generate due occurrences with bounded catch-up; safe to repeat").ProducesProblem(400).ProducesProblem(409);
    }
}
