using MaintainPro.Api.Security;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MaintainPro.Api.Endpoints;

public static class BreakdownEndpoints
{
    public static void MapBreakdownEndpoints(this IEndpointRouteBuilder app)
    {
        var breakdowns = app.MapGroup("/api/v1/breakdowns").WithTags("Breakdowns and corrective maintenance")
            .RequireAuthorization(Policies.ReadBreakdowns);
        breakdowns.MapPost("/process-notifications", async (BreakdownNotificationProcessor service, CancellationToken ct) =>
            TypedResults.Ok(await service.ProcessPendingAsync(ct))).RequireAuthorization(Policies.ManageNotifications);
        breakdowns.MapPost("", async (BreakdownReportRequest request, BreakdownService service, CancellationToken ct) =>
        {
            var result = await service.ReportAsync(request, ct);
            return TypedResults.Created($"/api/v1/breakdowns/{result.Id}", result);
        });
        breakdowns.MapGet("", async ([AsParameters] BreakdownQuery query, BreakdownService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)));
        breakdowns.MapGet("/{id:guid}", async (Guid id, BreakdownService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)));
        breakdowns.MapPost("/{id:guid}/assign", async (Guid id, BreakdownAssignmentRequest request, BreakdownService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AssignAsync(id, request, ct))).RequireAuthorization(Policies.ManageBreakdowns);
        breakdowns.MapGet("/{id:guid}/assignments", async (Guid id, BreakdownService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AssignmentsAsync(id, ct)));
        breakdowns.MapGet("/{id:guid}/history", async (Guid id, BreakdownService service, CancellationToken ct) =>
            TypedResults.Ok(await service.HistoryAsync(id, ct)));
        breakdowns.MapPost("/{id:guid}/return-to-service", async (Guid id, BreakdownReturnRequest request, BreakdownService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ReturnToServiceAsync(id, request, ct))).RequireAuthorization(Policies.ManageBreakdowns);
        app.MapGet("/api/v1/machines/{id:guid}/breakdown-history",
            async (Guid id, [AsParameters] BreakdownQuery query, BreakdownService service, CancellationToken ct) =>
                TypedResults.Ok(await service.MachineHistoryAsync(id, query, ct)))
            .RequireAuthorization(Policies.ReadBreakdowns).WithTags("Breakdown history");

        breakdowns.MapGet("/{id:guid}/corrective-action", async (Guid id, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)));
        breakdowns.MapPost("/{id:guid}/start", async (Guid id, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.StartAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapPost("/{id:guid}/complete", async (Guid id, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.CompleteAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapPost("/{id:guid}/resume", async (Guid id, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ResumeAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapPut("/{id:guid}/corrective-action", async (Guid id, CorrectiveActionRequest request, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SaveAsync(id, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapPost("/{id:guid}/parts", async (Guid id, CorrectivePartRequest request, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AddPartAsync(id, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapPut("/{id:guid}/parts/{partId:guid}", async (Guid id, Guid partId, CorrectivePartRequest request, CorrectiveExecutionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdatePartAsync(id, partId, request, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapDelete("/{id:guid}/parts/{partId:guid}", async (Guid id, Guid partId, CorrectiveExecutionService service, CancellationToken ct) =>
        {
            await service.DeletePartAsync(id, partId, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(Policies.RequireTechnician);

        breakdowns.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile file,
            [FromForm] BreakdownEvidenceType evidenceType, [FromForm] string? description,
            BreakdownEvidenceService service, CancellationToken ct) =>
        {
            await using var content = file.OpenReadStream();
            return TypedResults.Ok(await service.UploadAsync(id,
                new BreakdownEvidenceUploadRequest(file.FileName, file.ContentType, file.Length, evidenceType, description),
                content, ct));
        }).DisableAntiforgery().WithSummary("Upload private breakdown evidence using bearer authentication and multipart form data");
        breakdowns.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, BreakdownEvidenceService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, attachmentId, ct);
            return TypedResults.NoContent();
        });

        breakdowns.MapPost("/{id:guid}/submit", async (Guid id, CorrectiveSubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.SubmitAsync(id, ct))).RequireAuthorization(Policies.RequireTechnician);
        breakdowns.MapGet("/{id:guid}/submissions", async (Guid id, CorrectiveSubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(id, ct)));
        breakdowns.MapGet("/{id:guid}/submissions/{submissionId:guid}", async (Guid id, Guid submissionId, CorrectiveSubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, submissionId, ct)));
        breakdowns.MapPost("/{id:guid}/approve", async (Guid id, CorrectiveReviewRequest request, CorrectiveReviewService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ApproveAsync(id, request, ct))).RequireAuthorization(Policies.RequireSupervisor);
        breakdowns.MapPost("/{id:guid}/reject", async (Guid id, CorrectiveReviewRequest request, CorrectiveReviewService service, CancellationToken ct) =>
            TypedResults.Ok(await service.RejectAsync(id, request, ct))).RequireAuthorization(Policies.RequireSupervisor);
    }
}
