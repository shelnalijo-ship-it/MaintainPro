using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.ExternalServices;

public sealed class ExternalServiceAttachmentService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IFileStorageService storage)
{
    public async Task<ExternalServiceAttachmentDto> UploadAsync(Guid serviceId,
        ExternalServiceAttachmentUploadRequest request, Stream content, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.DocumentType)) throw new AppException(400, "DocumentType is invalid.");
        var description = Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var service = await ExternalServiceAccess.VisibleServices(db, currentUser).Include(x => x.Machine)
            .SingleOrDefaultAsync(x => x.Id == serviceId, ct)
            ?? throw new AppException(404, "External service not found.");
        ExternalServiceAccess.RequireAttachmentEditor(currentUser, service);
        StoredFile? stored = null;
        var commitAttempted = false;
        try
        {
            stored = await storage.StoreAsync(content, request.OriginalFilename, request.MimeType, request.FileSize, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var file = new FileRecord
            {
                StorageKey = stored.StorageKey, OriginalFilename = stored.OriginalFilename,
                MimeType = stored.MimeType, FileSize = stored.FileSize,
                UploadedByUserId = currentUser.UserId!.Value, UploadedAt = now
            };
            var attachment = new ExternalServiceAttachment
            {
                ExternalServiceId = service.Id, FileId = file.Id, File = file,
                DocumentType = request.DocumentType, Description = description,
                UploadedByUserId = currentUser.UserId.Value, UploadedAt = now
            };
            db.FileRecords.Add(file);
            db.ExternalServiceAttachments.Add(attachment);
            audit.Record("ExternalService.AttachmentAdded", nameof(ExternalService), service.Id,
                newValues: new { AttachmentId = attachment.Id, FileId = file.Id, attachment.DocumentType,
                    file.OriginalFilename, file.MimeType, file.FileSize, attachment.Description });
            await db.SaveChangesAsync(ct);
            commitAttempted = true;
            await transaction.CommitAsync(ct);
            return ToDto(attachment, file);
        }
        catch
        {
            if (stored is not null && !commitAttempted)
                await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<ExternalServiceAttachmentDto>> ListAsync(Guid serviceId,
        CancellationToken ct = default)
    {
        var visible = ExternalServiceAccess.VisibleServices(db, currentUser);
        if (!await visible.AnyAsync(x => x.Id == serviceId, ct))
            throw new AppException(404, "External service not found.");
        return await db.ExternalServiceAttachments.AsNoTracking()
            .Where(x => x.ExternalServiceId == serviceId && visible.Any(s => s.Id == x.ExternalServiceId))
            .OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id)
            .Select(x => new ExternalServiceAttachmentDto(x.Id, x.ExternalServiceId, x.FileId,
                x.DocumentType, x.Description, x.File.OriginalFilename, x.File.MimeType, x.File.FileSize,
                x.UploadedByUserId, x.UploadedAt, x.IsActive)).ToListAsync(ct);
    }

    public async Task DeactivateAsync(Guid serviceId, Guid attachmentId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var service = await ExternalServiceAccess.VisibleServices(db, currentUser).Include(x => x.Machine)
            .SingleOrDefaultAsync(x => x.Id == serviceId, ct)
            ?? throw new AppException(404, "External service not found.");
        ExternalServiceAccess.RequireAttachmentEditor(currentUser, service);
        var attachment = await db.ExternalServiceAttachments.SingleOrDefaultAsync(x =>
            x.Id == attachmentId && x.ExternalServiceId == serviceId && x.IsActive, ct)
            ?? throw new AppException(404, "External-service attachment not found.");
        attachment.IsActive = false;
        audit.Record("ExternalService.AttachmentDeactivated", nameof(ExternalService), service.Id,
            newValues: new { AttachmentId = attachment.Id, attachment.FileId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static ExternalServiceAttachmentDto ToDto(ExternalServiceAttachment x, FileRecord file) =>
        new(x.Id, x.ExternalServiceId, x.FileId, x.DocumentType, x.Description,
            file.OriginalFilename, file.MimeType, file.FileSize, x.UploadedByUserId, x.UploadedAt, x.IsActive);

    private static string? Optional(string? value, string name, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Guard.Required(value, name, max);
}
