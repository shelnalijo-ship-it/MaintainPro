using MaintainPro.Api.Security;
using MaintainPro.Application.Calibrations;
using MaintainPro.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MaintainPro.Api.Endpoints;

public static class CalibrationEndpoints
{
    public static void MapCalibrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/calibrations").WithTags("Calibration").RequireAuthorization(Policies.ReadCalibrations);
        group.MapGet("", async ([AsParameters] CalibrationQuery query, CalibrationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)));
        group.MapGet("/summary", async (CalibrationService service, CancellationToken ct) => TypedResults.Ok(await service.SummaryAsync(ct)));
        group.MapGet("/{id:guid}", async (Guid id, CalibrationService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, ct)));
        group.MapPost("/process-reminders", async (CalibrationReminderProcessor service, CancellationToken ct) =>
            TypedResults.Ok(await service.ProcessAsync(ct))).RequireAuthorization(Policies.ManageCalibrations);
        group.MapPost("", async ([FromForm] Guid machineId, [FromForm] string certificateNumber,
            [FromForm] string calibrationProvider, [FromForm] DateOnly calibrationDate, [FromForm] DateOnly expiryDate,
            [FromForm] CalibrationResult result, [FromForm] string? remarks, [FromForm] bool recordForNonRequiredMachine,
            IFormFile? certificate, CalibrationService service, CancellationToken ct) =>
        {
            CalibrationFileUpload? upload = certificate is null ? null : new(certificate.FileName, certificate.ContentType, certificate.Length);
            await using var content = certificate?.OpenReadStream();
            var created = await service.CreateAsync(new(machineId, certificateNumber, calibrationProvider,
                calibrationDate, expiryDate, result, remarks, recordForNonRequiredMachine), upload, content, ct);
            return TypedResults.Created($"/api/v1/calibrations/{created.Id}", created);
        }).DisableAntiforgery().RequireAuthorization(Policies.ManageCalibrations);

        app.MapGet("/api/v1/machines/{id:guid}/calibrations", async (Guid id, [AsParameters] CalibrationQuery query,
            CalibrationService service, CancellationToken ct) => TypedResults.Ok(await service.MachineHistoryAsync(id, query, ct)))
            .RequireAuthorization(Policies.ReadCalibrations).WithTags("Calibration");
        app.MapGet("/api/v1/machines/{id:guid}/calibration-status", async (Guid id, CalibrationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.MachineStatusAsync(id, ct))).RequireAuthorization(Policies.ReadCalibrations).WithTags("Calibration");
        app.MapPost("/api/v1/machines/{id:guid}/calibration-renewals/start", async (Guid id, CalibrationRenewalStartRequest request,
            CalibrationRenewalService service, CancellationToken ct) => TypedResults.Ok(await service.StartAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageCalibrations).WithTags("Calibration renewals");
        app.MapPost("/api/v1/machines/{id:guid}/calibration-renewals/complete", async (Guid id, CalibrationRenewalCompleteRequest request,
            CalibrationRenewalService service, CancellationToken ct) => TypedResults.Ok(await service.CompleteAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageCalibrations).WithTags("Calibration renewals");
        app.MapPost("/api/v1/machines/{id:guid}/calibration-renewals/cancel", async (Guid id, CalibrationRenewalCancelRequest request,
            CalibrationRenewalService service, CancellationToken ct) => TypedResults.Ok(await service.CancelAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageCalibrations).WithTags("Calibration renewals");
        app.MapGet("/api/v1/machines/{id:guid}/calibration-renewals", async (Guid id, CalibrationRenewalService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(id, ct))).RequireAuthorization(Policies.ReadCalibrations).WithTags("Calibration renewals");
    }
}
