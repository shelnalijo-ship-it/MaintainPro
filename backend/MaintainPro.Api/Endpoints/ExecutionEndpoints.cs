using MaintainPro.Api.Security;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Reviews;
using MaintainPro.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MaintainPro.Api.Endpoints;

public static class ExecutionEndpoints
{
    public static void MapExecutionEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/v1/work-orders").WithTags("Maintenance execution")
            .RequireAuthorization(Policies.ReadWorkOrders);
        orders.MapPost("/{id:guid}/start", async (Guid id, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.StartAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPost("/{id:guid}/complete", async (Guid id, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CompleteAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPost("/{id:guid}/resume", async (Guid id, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ResumeAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapGet("/{id:guid}/execution", async (Guid id, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)));
        orders.MapPut("/{id:guid}/checklist-results", async (Guid id, ChecklistResultsRequest request, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SaveChecklistResultsAsync(id, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPut("/{id:guid}/execution-comments", async (Guid id, ExecutionCommentsRequest request, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SaveCommentsAsync(id, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPost("/{id:guid}/parts", async (Guid id, PartUsageRequest request, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AddPartAsync(id, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPut("/{id:guid}/parts/{partId:guid}", async (Guid id, Guid partId, PartUsageRequest request, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdatePartAsync(id, partId, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapDelete("/{id:guid}/parts/{partId:guid}", async (Guid id, Guid partId, WorkOrderExecutionService service, CancellationToken ct) =>
        { await service.DeletePartAsync(id, partId, ct); return TypedResults.NoContent(); }).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPost("/{id:guid}/defects", async (Guid id, DefectRequest request, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AddDefectAsync(id, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPut("/{id:guid}/defects/{defectId:guid}", async (Guid id, Guid defectId, DefectRequest request, WorkOrderExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateDefectAsync(id, defectId, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapDelete("/{id:guid}/defects/{defectId:guid}", async (Guid id, Guid defectId, WorkOrderExecutionService service, CancellationToken ct) =>
        { await service.DeleteDefectAsync(id, defectId, ct); return TypedResults.NoContent(); }).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile file, [FromForm] EvidenceType evidenceType,
            [FromForm] Guid? workOrderChecklistItemId, [FromForm] string? description, WorkOrderEvidenceService service, CancellationToken ct) =>
        {
            await using var content = file.OpenReadStream();
            return TypedResults.Ok(await service.UploadAsync(id, new EvidenceUploadRequest(file.FileName, file.ContentType,
                file.Length, evidenceType, workOrderChecklistItemId, description), content, ct));
        }).RequireAuthorization(Policies.RequireTechnician).DisableAntiforgery()
            .WithSummary("Upload private evidence using bearer authentication and multipart form data");
        orders.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, WorkOrderEvidenceService service, CancellationToken ct) =>
        { await service.DeleteAsync(id, attachmentId, ct); return TypedResults.NoContent(); }).RequireAuthorization(Policies.RequireTechnician);
        orders.MapPost("/{id:guid}/submit", async (Guid id, WorkOrderSubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SubmitAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        orders.MapGet("/{id:guid}/submissions", async (Guid id, WorkOrderSubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(id, ct)));
        orders.MapGet("/{id:guid}/submissions/{submissionId:guid}", async (Guid id, Guid submissionId, WorkOrderSubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, submissionId, ct)));
        orders.MapGet("/pending-approvals", async ([AsParameters] PendingApprovalQuery query, WorkOrderReviewService service, CancellationToken ct) =>
            TypedResults.Ok(await service.PendingAsync(query, ct)));
        orders.MapPost("/{id:guid}/approve", async (Guid id, WorkOrderReviewRequest request, WorkOrderReviewService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ApproveAsync(id, request, ct))).RequireAuthorization(Policies.RequireSupervisor);
        orders.MapPost("/{id:guid}/reject", async (Guid id, WorkOrderReviewRequest request, WorkOrderReviewService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RejectAsync(id, request, ct))).RequireAuthorization(Policies.RequireSupervisor);
        orders.MapGet("/{id:guid}/history", async (Guid id, WorkOrderHistoryService service, CancellationToken ct) =>
            TypedResults.Ok(await service.HistoryAsync(id, ct)));
        app.MapGet("/api/v1/machines/{id:guid}/maintenance-history", async (Guid id, [AsParameters] MachineMaintenanceHistoryQuery query, WorkOrderHistoryService service, CancellationToken ct) =>
            TypedResults.Ok(await service.MachineHistoryAsync(id, query, ct))).RequireAuthorization(Policies.ReadWorkOrders).WithTags("Maintenance history");
        app.MapGet("/api/v1/files/{id:guid}", async (Guid id, WorkOrderEvidenceService service, HttpContext http, CancellationToken ct) =>
        {
            var file = await service.DownloadAsync(id, ct);
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return TypedResults.File(file.Content, file.MimeType, file.OriginalFilename);
        }).RequireAuthorization(Policies.ReadWorkOrders).WithTags("Private evidence");
    }
}
