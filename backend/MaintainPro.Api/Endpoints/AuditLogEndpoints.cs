using MaintainPro.Api.Security;
using MaintainPro.Application.Audit;
using Microsoft.AspNetCore.Mvc;

namespace MaintainPro.Api.Endpoints;

public static class AuditLogEndpoints
{
    public static void MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/audit-logs", async ([AsParameters] AuditLogQuery query,
            AuditLogQueryService service, CancellationToken cancellationToken) =>
            TypedResults.Ok(await service.ListAsync(query, cancellationToken)))
            .WithTags("Audit logs")
            .WithSummary("Filter and page immutable audit records")
            .RequireAuthorization(Policies.ViewAuditLogs)
            .ProducesProblem(400);
    }
}
