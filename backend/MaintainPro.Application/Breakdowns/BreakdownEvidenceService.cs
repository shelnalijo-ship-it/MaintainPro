using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public sealed class BreakdownEvidenceService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IFileStorageService storage)
{
    public async Task<BreakdownAttachmentDto> UploadAsync(Guid id, BreakdownEvidenceUploadRequest request,
        Stream content, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.EvidenceType)) throw new AppException(400, "EvidenceType is invalid.");
        var description = ExecutionRules.Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (breakdown, draft) = await EditableAsync(id, ct);
        StoredFile? stored = null;
        var commitAttempted = false;
        try
        {
            stored = await storage.StoreAsync(content, request.OriginalFilename, request.MimeType, request.FileSize, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var file = new FileRecord { StorageKey = stored.StorageKey, OriginalFilename = stored.OriginalFilename,
                MimeType = stored.MimeType, FileSize = stored.FileSize, UploadedByUserId = currentUser.UserId!.Value, UploadedAt = now };
            var attachment = new BreakdownAttachment { BreakdownId = id, FileId = file.Id, File = file,
                EvidenceType = request.EvidenceType, Description = description,
                UploadedByUserId = currentUser.UserId.Value, UploadedAt = now };
            db.FileRecords.Add(file); db.BreakdownAttachments.Add(attachment);
            Touch(breakdown, draft, now);
            var details = new { AttachmentId = attachment.Id, FileId = file.Id, file.MimeType, file.FileSize, attachment.EvidenceType };
            BreakdownHistory.Record(db, currentUser, breakdown, "Breakdown.EvidenceAdded", now, details: details);
            audit.Record("Breakdown.EvidenceAdded", nameof(Breakdown), id, newValues: details);
            await db.SaveChangesAsync(ct);
            // A failed COMMIT can have an uncertain outcome. Preserve bytes whenever the database
            // may already have committed a reference; cleanup is safe for pre-commit failures only.
            commitAttempted = true;
            await transaction.CommitAsync(ct);
            return CorrectiveExecutionService.ToDto(attachment);
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
        var (breakdown, draft) = await EditableAsync(id, ct);
        var attachment = await db.BreakdownAttachments.SingleOrDefaultAsync(
            x => x.Id == attachmentId && x.BreakdownId == id && !x.IsDeleted, ct)
            ?? throw new AppException(404, "Breakdown attachment not found.");
        if (breakdown.Status is BreakdownStatus.REPORTED or BreakdownStatus.ASSIGNED &&
            attachment.UploadedByUserId != currentUser.UserId)
            throw new AppException(403, "Only the reporting author can remove their initial evidence.");
        attachment.IsDeleted = true;
        var now = clock.GetUtcNow().UtcDateTime;
        Touch(breakdown, draft, now);
        var details = new { AttachmentId = attachment.Id, attachment.FileId };
        BreakdownHistory.Record(db, currentUser, breakdown, "Breakdown.EvidenceRemoved", now, details: details);
        audit.Record("Breakdown.EvidenceRemoved", nameof(Breakdown), id, newValues: details);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // File metadata and bytes remain available to authorized immutable submission reads.
    }

    private async Task<(Breakdown Breakdown, CorrectiveActionDraft? Draft)> EditableAsync(Guid id, CancellationToken ct)
    {
        var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
        if (breakdown.Status is BreakdownStatus.REPORTED or BreakdownStatus.ASSIGNED)
        {
            if (breakdown.ReportedByUserId != currentUser.UserId)
                throw new AppException(403, "Only the reporting author can change initial breakdown evidence.");
            return (breakdown, null);
        }
        CorrectiveRules.RequireEditable(breakdown);
        BreakdownAccess.RequireTechnician(currentUser, breakdown);
        var draft = await db.CorrectiveActionDrafts.SingleOrDefaultAsync(x => x.BreakdownId == id, ct)
            ?? throw new AppException(409, "Start corrective execution before changing evidence.");
        if (draft.TechnicianId != currentUser.UserId)
            throw new AppException(409, "Resume the corrective assignment before changing evidence.");
        return (breakdown, draft);
    }

    private static void Touch(Breakdown breakdown, CorrectiveActionDraft? draft, DateTime now)
    {
        if (draft is not null) CorrectiveRules.TouchDraft(breakdown, draft, now);
        else breakdown.Version = Guid.NewGuid();
    }
}
