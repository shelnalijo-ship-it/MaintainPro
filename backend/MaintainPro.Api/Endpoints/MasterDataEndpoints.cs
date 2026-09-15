using MaintainPro.Application.MasterData;
using MaintainPro.Api.Security;

namespace MaintainPro.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static void MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var departments = app.MapGroup("/api/v1/departments").WithTags("Departments").RequireAuthorization(Policies.ManageMachines);
        departments.MapGet("", async (bool? isActive, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListDepartmentsAsync(isActive, ct)));
        departments.MapPost("", async (MasterDataWriteRequest request, MasterDataService service, CancellationToken ct) =>
        {
            var result = await service.CreateDepartmentAsync(request, ct);
            return TypedResults.Created((string?)null, result);
        });
        departments.MapPut("/{id:guid}", async (Guid id, MasterDataWriteRequest request, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateDepartmentAsync(id, request, ct)));
        departments.MapPatch("/{id:guid}/status", async (Guid id, MasterDataStatusRequest request, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetDepartmentStatusAsync(id, request, ct)));

        var locations = app.MapGroup("/api/v1/locations").WithTags("Locations").RequireAuthorization(Policies.ManageMachines);
        locations.MapGet("", async (bool? isActive, Guid? departmentId, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListLocationsAsync(isActive, departmentId, ct)));
        locations.MapPost("", async (LocationWriteRequest request, MasterDataService service, CancellationToken ct) =>
        {
            var result = await service.CreateLocationAsync(request, ct);
            return TypedResults.Created((string?)null, result);
        });
        locations.MapPut("/{id:guid}", async (Guid id, LocationWriteRequest request, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateLocationAsync(id, request, ct)));
        locations.MapPatch("/{id:guid}/status", async (Guid id, MasterDataStatusRequest request, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetLocationStatusAsync(id, request, ct)));

        var categories = app.MapGroup("/api/v1/machine-categories").WithTags("Machine categories").RequireAuthorization(Policies.ManageMachines);
        categories.MapGet("", async (bool? isActive, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListMachineCategoriesAsync(isActive, ct)));
        categories.MapPost("", async (MasterDataWriteRequest request, MasterDataService service, CancellationToken ct) =>
        {
            var result = await service.CreateMachineCategoryAsync(request, ct);
            return TypedResults.Created((string?)null, result);
        });
        categories.MapPut("/{id:guid}", async (Guid id, MasterDataWriteRequest request, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateMachineCategoryAsync(id, request, ct)));
        categories.MapPatch("/{id:guid}/status", async (Guid id, MasterDataStatusRequest request, MasterDataService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SetMachineCategoryStatusAsync(id, request, ct)));
    }
}
