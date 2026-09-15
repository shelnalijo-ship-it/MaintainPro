using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public sealed class CorrectiveSubmissionService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<CorrectiveSubmissionDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
        BreakdownAccess.RequireTechnician(currentUser, breakdown);
        CorrectiveRules.RequireTransition(breakdown, BreakdownStatus.AWAITING_APPROVAL);
        var draft = await db.CorrectiveActionDrafts.SingleOrDefaultAsync(x => x.BreakdownId == id, ct)
            ?? throw new AppException(409, "Start and complete corrective execution before submitting.");
        if (draft.TechnicianId != currentUser.UserId)
            throw new AppException(409, "The corrective draft belongs to a different technician.");
        if (!breakdown.StartedAt.HasValue || !breakdown.CompletedAt.HasValue || draft.AttemptStartedAt.HasValue)
            throw new AppException(409, "Complete corrective execution before submitting.");
        CorrectiveRules.RequireCompleteFacts(draft);
        var parts = await db.CorrectivePartUsages.AsNoTracking().Where(x => x.BreakdownId == id).ToListAsync(ct);
        var attachments = await db.BreakdownAttachments.AsNoTracking().Include(x => x.File)
            .Where(x => x.BreakdownId == id && !x.IsDeleted).ToListAsync(ct);
        if (attachments.Any(x => x.File.FileSize <= 0 || string.IsNullOrWhiteSpace(x.File.StorageKey)))
            throw new AppException(409, "All evidence uploads must be finalized before submitting.");
        if (parts.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.PartName)))
            throw new AppException(409, "All corrective parts require a name and a positive quantity.");
        if (breakdown.SubmissionVersion == int.MaxValue)
            throw new AppException(409, "This breakdown has reached its submission-version limit.");

        var now = clock.GetUtcNow().UtcDateTime;
        breakdown.SubmissionVersion++;
        var submission = new CorrectiveSubmission
        {
            BreakdownId = id, VersionNumber = breakdown.SubmissionVersion, TechnicianId = draft.TechnicianId,
            TechnicianEmployeeId = draft.TechnicianEmployeeId, TechnicianName = draft.TechnicianName,
            RootCause = draft.RootCause!, CorrectiveAction = draft.CorrectiveAction!, Comments = draft.Comments,
            StartedAt = breakdown.StartedAt.Value, CompletedAt = breakdown.CompletedAt.Value,
            DurationMinutes = draft.AccumulatedDurationMinutes,
            DowntimeMinutes = BreakdownTiming.DowntimeMinutes(breakdown, now), SubmittedAt = now, CreatedAt = now
        };
        submission.PartUsages = parts.Select(x => new CorrectiveSubmissionPartUsage
        {
            CorrectiveSubmissionId = submission.Id, PartName = x.PartName, PartNumber = x.PartNumber,
            Quantity = x.Quantity, Remarks = x.Remarks, CreatedByUserId = x.CreatedByUserId, CreatedAt = x.CreatedAt
        }).ToList();
        submission.Attachments = attachments.Select(x => new CorrectiveSubmissionAttachment
        {
            CorrectiveSubmissionId = submission.Id, FileId = x.FileId, EvidenceType = x.EvidenceType,
            Description = x.Description, UploadedByUserId = x.UploadedByUserId, UploadedAt = x.UploadedAt
        }).ToList();
        db.CorrectiveSubmissions.Add(submission);
        breakdown.Status = BreakdownStatus.AWAITING_APPROVAL;
        breakdown.SubmittedAt = now;
        var details = new { SubmissionId = submission.Id, submission.VersionNumber, submission.TechnicianId,
            submission.SubmittedAt, submission.DurationMinutes, submission.DowntimeMinutes,
            PartCount = parts.Count, AttachmentCount = attachments.Count };
        BreakdownHistory.Record(db, currentUser, breakdown, "Corrective.Submitted", now,
            submission.Id, submission.VersionNumber, details);
        audit.Record("Corrective.Submitted", nameof(Breakdown), id, newValues: details);
        await new BreakdownNotificationService(db, currentUser, audit, clock).EnsureAsync(
            breakdown, NotificationType.CORRECTIVE_SUBMITTED, submission.Id, ct);
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, submission.Id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<IReadOnlyList<CorrectiveSubmissionSummaryDto>> ListAsync(Guid id, CancellationToken ct = default)
    {
        if (!await BreakdownAccess.Visible(db, currentUser).AnyAsync(x => x.Id == id, ct))
            throw new AppException(404, "Breakdown not found.");
        var submissions = await VisibleSubmissions(id).Include(x => x.Review)
            .OrderBy(x => x.VersionNumber).ToListAsync(ct);
        return submissions.Select(ToSummary).ToArray();
    }

    public async Task<CorrectiveSubmissionDto> GetAsync(Guid id, Guid submissionId, CancellationToken ct = default)
    {
        var visible = VisibleSubmissions(id);
        var submission = await visible.Include(x => x.Review).SingleOrDefaultAsync(x => x.Id == submissionId, ct)
            ?? throw new AppException(404, "Corrective submission not found.");
        var parts = await db.CorrectiveSubmissionPartUsages.AsNoTracking()
            .Where(x => x.CorrectiveSubmissionId == submissionId && visible.Any(s => s.Id == x.CorrectiveSubmissionId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new CorrectivePartDto(x.Id, x.PartName, x.PartNumber, x.Quantity,
                x.Remarks, x.CreatedByUserId, x.CreatedAt)).ToListAsync(ct);
        var attachments = await db.CorrectiveSubmissionAttachments.AsNoTracking()
            .Where(x => x.CorrectiveSubmissionId == submissionId && visible.Any(s => s.Id == x.CorrectiveSubmissionId))
            .OrderBy(x => x.UploadedAt).ThenBy(x => x.Id)
            .Select(x => new BreakdownAttachmentDto(x.Id, x.FileId, x.EvidenceType, x.Description,
                x.File.OriginalFilename, x.File.MimeType, x.File.FileSize, x.UploadedByUserId, x.UploadedAt)).ToListAsync(ct);
        return new(ToSummary(submission), submission.RootCause, submission.CorrectiveAction, submission.Comments, parts, attachments);
    }

    private IQueryable<CorrectiveSubmission> VisibleSubmissions(Guid id)
    {
        var visible = BreakdownAccess.Visible(db, currentUser);
        return db.CorrectiveSubmissions.AsNoTracking().Where(x => x.BreakdownId == id && visible.Any(b => b.Id == x.BreakdownId));
    }

    private static CorrectiveSubmissionSummaryDto ToSummary(CorrectiveSubmission x) =>
        new(x.Id, x.BreakdownId, x.VersionNumber, x.TechnicianId, x.TechnicianEmployeeId, x.TechnicianName,
            x.StartedAt, x.CompletedAt, x.SubmittedAt, x.DurationMinutes, x.DowntimeMinutes,
            x.Review is null ? null : CorrectiveReviewService.ToDto(x.Review));
}
