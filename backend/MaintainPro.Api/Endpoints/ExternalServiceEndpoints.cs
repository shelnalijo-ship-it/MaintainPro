using MaintainPro.Api.Security;
using MaintainPro.Application.ExternalServices;
using MaintainPro.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MaintainPro.Api.Endpoints;

public static class ExternalServiceEndpoints
{
    public static void MapExternalServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var services = app.MapGroup("/api/v1/external-services")
            .WithTags("External services").RequireAuthorization(Policies.ReadExternalServices);
        services.MapGet("", async ([AsParameters] ExternalServiceQuery query,
            ExternalServiceService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(query, ct)));
        services.MapGet("/follow-ups", async ([AsParameters] ExternalServiceFollowUpQuery query,
            ExternalServiceService service, CancellationToken ct) => TypedResults.Ok(await service.FollowUpsAsync(query, ct)))
            .RequireAuthorization(Policies.ManageExternalServiceFollowUps);
        services.MapGet("/{id:guid}", async (Guid id, ExternalServiceService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)));
        services.MapPost("", async (ExternalServiceWriteRequest request,
            ExternalServiceService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/v1/external-services/{created.Id}", created);
        });
        services.MapPut("/{id:guid}", async (Guid id, ExternalServiceWriteRequest request,
            ExternalServiceService service, CancellationToken ct) => TypedResults.Ok(await service.UpdateAsync(id, request, ct)));
        services.MapGet("/{id:guid}/attachments", async (Guid id, ExternalServiceAttachmentService service,
            CancellationToken ct) => TypedResults.Ok(await service.ListAsync(id, ct)));
        services.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile file,
            [FromForm] ExternalServiceDocumentType documentType, [FromForm] string? description,
            ExternalServiceAttachmentService service, CancellationToken ct) =>
        {
            await using var content = file.OpenReadStream();
            return TypedResults.Ok(await service.UploadAsync(id,
                new(file.FileName, file.ContentType, file.Length, documentType, description), content, ct));
        }).DisableAntiforgery().WithSummary("Upload a private external-service document");
        services.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId,
            ExternalServiceAttachmentService service, CancellationToken ct) =>
        {
            await service.DeactivateAsync(id, attachmentId, ct);
            return TypedResults.NoContent();
        });

        app.MapGet("/api/v1/machines/{id:guid}/external-services", async (Guid id,
            [AsParameters] ExternalServiceQuery query, ExternalServiceService service, CancellationToken ct) =>
                TypedResults.Ok(await service.MachineHistoryAsync(id, query, ct)))
            .RequireAuthorization(Policies.ReadExternalServices).WithTags("External-service history");

        var documents = app.MapGroup("/api/v1/machines/{id:guid}/documents")
            .WithTags("Machine documents").RequireAuthorization(Policies.ReadExternalServices);
        documents.MapGet("", async (Guid id, [AsParameters] MachineDocumentQuery query,
            MachineDocumentService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(id, query, ct)));
        documents.MapGet("/{documentId:guid}", async (Guid id, Guid documentId,
            MachineDocumentService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(id, documentId, ct)));
        documents.MapPost("", async (Guid id, IFormFile file, [FromForm] MachineDocumentType documentType,
            [FromForm] string title, [FromForm] string? description, [FromForm] DateOnly? documentDate,
            [FromForm] DateOnly? expiryDate, MachineDocumentService service, CancellationToken ct) =>
        {
            await using var content = file.OpenReadStream();
            return TypedResults.Ok(await service.CreateAsync(id,
                new(file.FileName, file.ContentType, file.Length, documentType, title,
                    description, documentDate, expiryDate), content, ct));
        }).DisableAntiforgery().RequireAuthorization(Policies.ManageMachineDocuments)
            .WithSummary("Upload a private general machine document");
        documents.MapPut("/{documentId:guid}", async (Guid id, Guid documentId,
            MachineDocumentUpdateRequest request, MachineDocumentService service, CancellationToken ct) =>
                TypedResults.Ok(await service.UpdateAsync(id, documentId, request, ct)))
            .RequireAuthorization(Policies.ManageMachineDocuments);
        documents.MapPatch("/{documentId:guid}/status", async (Guid id, Guid documentId,
            MachineDocumentStatusRequest request, MachineDocumentService service, CancellationToken ct) =>
                TypedResults.Ok(await service.SetStatusAsync(id, documentId, request, ct)))
            .RequireAuthorization(Policies.ManageMachineDocuments);
    }
}
