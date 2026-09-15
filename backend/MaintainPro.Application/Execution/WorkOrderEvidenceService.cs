using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Execution;

public sealed class WorkOrderEvidenceService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IFileStorageService storage)
{
    public async Task<AttachmentDto> UploadAsync(Guid id, EvidenceUploadRequest request, Stream content,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.EvidenceType)) throw new AppException(400, "EvidenceType is invalid.");
        var description = ExecutionRules.Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        if (request.WorkOrderChecklistItemId is Guid itemId && !order.Definition.Items.Any(x => x.Id == itemId))
            throw new AppException(400, "Evidence must reference a checklist item belonging to this work order.");
        StoredFile? stored = null;
        var commitAttempted = false;
        try
        {
            stored = await storage.StoreAsync(content, request.OriginalFilename, request.MimeType, request.FileSize, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var file = new FileRecord { StorageKey = stored.StorageKey, OriginalFilename = stored.OriginalFilename,
                MimeType = stored.MimeType, FileSize = stored.FileSize, UploadedByUserId = currentUser.UserId!.Value, UploadedAt = now };
            var attachment = new WorkOrderAttachment { WorkOrderId = id, FileId = file.Id, File = file,
                WorkOrderChecklistItemId = request.WorkOrderChecklistItemId, EvidenceType = request.EvidenceType,
                Description = description, UploadedByUserId = currentUser.UserId.Value, UploadedAt = now };
            db.FileRecords.Add(file); db.WorkOrderAttachments.Add(attachment);
            WorkOrderExecutionService.TouchDraft(order, execution, now);
            audit.Record("WorkOrder.EvidenceAdded", nameof(WorkOrder), id,
                newValues: new { AttachmentId = attachment.Id, FileId = file.Id, file.MimeType, file.FileSize, attachment.EvidenceType });
            await db.SaveChangesAsync(ct);
            // If the connection fails during COMMIT the outcome may be uncertain. Keep bytes in that
            // case rather than deleting a potentially committed file reference.
            commitAttempted = true;
            await transaction.CommitAsync(ct);
            return WorkOrderExecutionService.ToDto(attachment);
        }
        catch
        {
            if (stored is not null && !commitAttempted)
                await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(Guid id, Guid attachmentId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var attachment = await db.WorkOrderAttachments.SingleOrDefaultAsync(
            x => x.Id == attachmentId && x.WorkOrderId == id && !x.IsDeleted, ct)
            ?? throw new AppException(404, "Attachment not found.");
        attachment.IsDeleted = true;
        WorkOrderExecutionService.TouchDraft(order, execution, clock.GetUtcNow().UtcDateTime);
        audit.Record("WorkOrder.EvidenceRemoved", nameof(WorkOrder), id,
            newValues: new { AttachmentId = attachment.Id, attachment.FileId });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        // Retain bytes and metadata. Earlier immutable submissions may still reference this file.
    }

    public async Task<FileDownload> DownloadAsync(Guid fileId, CancellationToken ct = default)
    {
        var visible = ExecutionAccess.VisibleWorkOrders(db, currentUser);
        var file = await db.FileRecords.AsNoTracking().Where(file => file.Id == fileId &&
            (db.WorkOrderAttachments.Any(attachment => attachment.FileId == file.Id && !attachment.IsDeleted &&
                visible.Any(order => order.Id == attachment.WorkOrderId)) ||
             db.WorkOrderSubmissionAttachments.Any(attachment => attachment.FileId == file.Id &&
                visible.Any(order => order.Id == attachment.Submission.WorkOrderId))))
            .SingleOrDefaultAsync(ct) ?? throw new AppException(404, "File not found.");
        try
        {
            var stream = await storage.OpenReadAsync(file.StorageKey, ct);
            return new(stream, file.OriginalFilename, file.MimeType, file.FileSize);
        }
        catch (FileNotFoundException) { throw new AppException(404, "File content is unavailable."); }
        catch (DirectoryNotFoundException) { throw new AppException(404, "File content is unavailable."); }
    }

    private async Task<(WorkOrder Order, WorkOrderExecution Execution)> EditableAsync(Guid id, CancellationToken ct)
    {
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, id, ct: ct);
        ExecutionAccess.RequireTechnician(currentUser, order); ExecutionRules.RequireEditable(order);
        var execution = await db.WorkOrderExecutions.SingleOrDefaultAsync(x => x.WorkOrderId == id, ct)
            ?? throw new AppException(409, "Start execution before uploading evidence.");
        return (order, execution);
    }
}
