using MaintainPro.Api.Security;
using MaintainPro.Application.WorkOrders;

namespace MaintainPro.Api.Endpoints;

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/v1/work-orders").WithTags("Work orders")
            .RequireAuthorization(Policies.ReadWorkOrders);
        orders.MapGet("", async ([AsParameters] WorkOrderQuery query, WorkOrderService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)))
            .WithSummary("Search, filter and page visible work orders").ProducesProblem(400);
        orders.MapGet("/calendar", async ([AsParameters] WorkOrderCalendarQuery query, WorkOrderService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CalendarAsync(query, ct)))
            .WithSummary("Read calendar events within an inclusive planned-date range").ProducesProblem(400);
        orders.MapGet("/{id:guid}", async (Guid id, WorkOrderService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithSummary("Read a work order and its immutable definition").ProducesProblem(404);
    }
}
