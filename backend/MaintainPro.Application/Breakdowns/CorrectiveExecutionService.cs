using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public sealed class CorrectiveExecutionService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<CorrectiveExecutionDto> StartAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
        BreakdownAccess.RequireTechnician(currentUser, breakdown);
        if (breakdown.Status != BreakdownStatus.ASSIGNED)
            throw new AppException(409, "Only assigned corrective work can start; use resume after rejection.");
        CorrectiveRules.RequireTransition(breakdown, BreakdownStatus.IN_PROGRESS);
        var technician = await ActiveTechnicianAsync(ct);
        var now = Now;
        var draft = new CorrectiveActionDraft { BreakdownId = id, TechnicianId = technician.Id,
            TechnicianEmployeeId = technician.EmployeeId, TechnicianName = FullName(technician),
            AttemptStartedAt = now, CreatedAt = now, UpdatedAt = now };
        db.CorrectiveActionDrafts.Add(draft);
        breakdown.Status = BreakdownStatus.IN_PROGRESS;
        breakdown.StartedAt = now;
        Record(breakdown, "Corrective.Started", new { draft.TechnicianId, breakdown.StartedAt });
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<CorrectiveExecutionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var visible = BreakdownAccess.Visible(db, currentUser);
        var breakdown = await visible.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Breakdown not found.");
        var draft = await db.CorrectiveActionDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.BreakdownId == id &&
            visible.Any(b => b.Id == x.BreakdownId), ct);
        var parts = await db.CorrectivePartUsages.AsNoTracking().Where(x => x.BreakdownId == id &&
            visible.Any(b => b.Id == x.BreakdownId)).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var attachments = await db.BreakdownAttachments.AsNoTracking().Include(x => x.File)
            .Where(x => x.BreakdownId == id && !x.IsDeleted && visible.Any(b => b.Id == x.BreakdownId))
            .OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var duration = draft?.AccumulatedDurationMinutes ?? 0;
        if (draft?.AttemptStartedAt is DateTime started) duration += CorrectiveRules.ElapsedMinutes(started, Now);
        return new(id, breakdown.Status, draft?.TechnicianId, draft?.TechnicianEmployeeId, draft?.TechnicianName,
            draft?.RootCause, draft?.CorrectiveAction, draft?.Comments, breakdown.StartedAt, breakdown.CompletedAt,
            duration, BreakdownTiming.DowntimeMinutes(breakdown, Now), breakdown.CompletedAt.HasValue && draft?.AttemptStartedAt is null,
            parts.Select(ToDto).ToArray(), attachments.Select(ToDto).ToArray());
    }

    public async Task<CorrectiveExecutionDto> SaveAsync(Guid id, CorrectiveActionRequest request, CancellationToken ct = default)
    {
        var cause = ExecutionRules.Optional(request.RootCause, "RootCause", 10000);
        var action = ExecutionRules.Optional(request.CorrectiveAction, "CorrectiveAction", 10000);
        var comments = ExecutionRules.Optional(request.Comments, "Comments", 10000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (breakdown, draft) = await EditableAsync(id, ct);
        if (draft.RootCause != cause || draft.CorrectiveAction != action || draft.Comments != comments)
        {
            draft.RootCause = cause; draft.CorrectiveAction = action; draft.Comments = comments;
            CorrectiveRules.TouchDraft(breakdown, draft, Now);
            Record(breakdown, "Corrective.ActionUpdated", new { HasRootCause = cause is not null,
                HasCorrectiveAction = action is not null, HasComments = comments is not null });
            await db.SaveChangesAsync(ct);
        }
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<CorrectivePartDto> AddPartAsync(Guid id, CorrectivePartRequest request, CancellationToken ct = default)
    {
        var value = Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (breakdown, draft) = await EditableAsync(id, ct);
        var part = new CorrectivePartUsage { BreakdownId = id, PartName = value.PartName, PartNumber = value.PartNumber,
            Quantity = value.Quantity, Remarks = value.Remarks, CreatedByUserId = currentUser.UserId!.Value, CreatedAt = Now };
        db.CorrectivePartUsages.Add(part);
        CorrectiveRules.TouchDraft(breakdown, draft, Now);
        Record(breakdown, "Corrective.PartAdded", new { PartId = part.Id, part.PartName, part.Quantity });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToDto(part);
    }

    public async Task<CorrectivePartDto> UpdatePartAsync(Guid id, Guid partId, CorrectivePartRequest request, CancellationToken ct = default)
    {
        var value = Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (breakdown, draft) = await EditableAsync(id, ct);
        var part = await db.CorrectivePartUsages.SingleOrDefaultAsync(x => x.Id == partId && x.BreakdownId == id, ct)
            ?? throw new AppException(404, "Corrective part usage not found.");
        part.PartName = value.PartName; part.PartNumber = value.PartNumber; part.Quantity = value.Quantity; part.Remarks = value.Remarks;
        CorrectiveRules.TouchDraft(breakdown, draft, Now);
        Record(breakdown, "Corrective.PartUpdated", new { PartId = part.Id, part.PartName, part.Quantity });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToDto(part);
    }

    public async Task DeletePartAsync(Guid id, Guid partId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (breakdown, draft) = await EditableAsync(id, ct);
        var part = await db.CorrectivePartUsages.SingleOrDefaultAsync(x => x.Id == partId && x.BreakdownId == id, ct)
            ?? throw new AppException(404, "Corrective part usage not found.");
        db.CorrectivePartUsages.Remove(part);
        CorrectiveRules.TouchDraft(breakdown, draft, Now);
        Record(breakdown, "Corrective.PartRemoved", new { PartId = part.Id });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public async Task<CorrectiveExecutionDto> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (breakdown, draft) = await EditableAsync(id, ct);
        if (breakdown.CompletedAt.HasValue || draft.AttemptStartedAt is not DateTime started)
            throw new AppException(409, "Corrective execution is already complete.");
        CorrectiveRules.RequireCompleteFacts(draft);
        var now = Now;
        draft.AccumulatedDurationMinutes += CorrectiveRules.ElapsedMinutes(started, now);
        draft.AttemptStartedAt = null; draft.UpdatedAt = now; breakdown.CompletedAt = now;
        Record(breakdown, "Corrective.Completed", new { breakdown.CompletedAt, DurationMinutes = draft.AccumulatedDurationMinutes });
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<CorrectiveExecutionDto> ResumeAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
        BreakdownAccess.RequireTechnician(currentUser, breakdown);
        if (breakdown.Status != BreakdownStatus.REJECTED)
            throw new AppException(409, "Only rejected corrective work can be resumed.");
        CorrectiveRules.RequireTransition(breakdown, BreakdownStatus.IN_PROGRESS);
        var draft = await FindDraftAsync(id, ct);
        var technician = await ActiveTechnicianAsync(ct);
        draft.TechnicianId = technician.Id; draft.TechnicianEmployeeId = technician.EmployeeId;
        draft.TechnicianName = FullName(technician); draft.AttemptStartedAt = Now; draft.UpdatedAt = Now;
        breakdown.Status = BreakdownStatus.IN_PROGRESS; breakdown.CompletedAt = null;
        Record(breakdown, "Corrective.Resumed", new { draft.TechnicianId, PreviousSubmissionVersion = breakdown.SubmissionVersion });
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    private async Task<(Breakdown, CorrectiveActionDraft)> EditableAsync(Guid id, CancellationToken ct)
    {
        var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
        var currentVersion = await db.Breakdowns.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.Version).SingleAsync(ct);
        if (currentVersion != breakdown.Version)
            throw new DbUpdateConcurrencyException("The corrective breakdown was changed by another request.");
        BreakdownAccess.RequireTechnician(currentUser, breakdown);
        CorrectiveRules.RequireEditable(breakdown);
        var draft = await FindDraftAsync(id, ct);
        if (draft.TechnicianId != currentUser.UserId)
            throw new AppException(409, "Resume the corrective assignment before editing its draft.");
        return (breakdown, draft);
    }

    private async Task<CorrectiveActionDraft> FindDraftAsync(Guid id, CancellationToken ct) =>
        await db.CorrectiveActionDrafts.SingleOrDefaultAsync(x => x.BreakdownId == id, ct)
        ?? throw new AppException(409, "Start corrective execution first.");
    private async Task<User> ActiveTechnicianAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId && x.IsActive &&
            x.UserRoles.Any(role => role.Role.Name == RoleNames.Technician), ct)
        ?? throw new AppException(403, "Corrective execution requires an active technician.");
    private void Record(Breakdown breakdown, string action, object details)
    {
        BreakdownHistory.Record(db, currentUser, breakdown, action, Now, details: details);
        audit.Record(action, nameof(Breakdown), breakdown.Id, newValues: details);
    }
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static string FullName(User user) => $"{user.FirstName} {user.LastName}".Trim();
    private static CorrectivePartRequest Normalize(CorrectivePartRequest value)
    {
        if (value.Quantity <= 0) throw new AppException(400, "Quantity must be greater than zero.");
        return value with { PartName = Guard.Required(value.PartName, "PartName", 200),
            PartNumber = ExecutionRules.Optional(value.PartNumber, "PartNumber", 200),
            Remarks = ExecutionRules.Optional(value.Remarks, "Remarks", 10000) };
    }
    public static CorrectivePartDto ToDto(CorrectivePartUsage x) =>
        new(x.Id, x.PartName, x.PartNumber, x.Quantity, x.Remarks, x.CreatedByUserId, x.CreatedAt);
    public static BreakdownAttachmentDto ToDto(BreakdownAttachment x) =>
        new(x.Id, x.FileId, x.EvidenceType, x.Description, x.File.OriginalFilename, x.File.MimeType,
            x.File.FileSize, x.UploadedByUserId, x.UploadedAt);
}
