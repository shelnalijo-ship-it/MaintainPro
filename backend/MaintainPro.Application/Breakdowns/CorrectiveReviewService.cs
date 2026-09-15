using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public sealed class CorrectiveReviewService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public Task<CorrectiveApprovalDto> ApproveAsync(Guid id, CorrectiveReviewRequest request, CancellationToken ct = default) =>
        DecideAsync(id, request, CorrectiveReviewDecision.APPROVED, ct);
    public Task<CorrectiveApprovalDto> RejectAsync(Guid id, CorrectiveReviewRequest request, CancellationToken ct = default) =>
        DecideAsync(id, request, CorrectiveReviewDecision.REJECTED, ct);

    private async Task<CorrectiveApprovalDto> DecideAsync(Guid id, CorrectiveReviewRequest request,
        CorrectiveReviewDecision decision, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
        BreakdownAccess.RequireSupervisor(currentUser, breakdown);
        var target = decision == CorrectiveReviewDecision.APPROVED ? BreakdownStatus.CLOSED : BreakdownStatus.REJECTED;
        CorrectiveRules.RequireTransition(breakdown, target);
        var submission = await db.CorrectiveSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.SubmissionId && x.BreakdownId == id, ct)
            ?? throw new AppException(404, "Corrective submission not found for this breakdown.");
        if (submission.TechnicianId == currentUser.UserId)
            throw new AppException(403, "A technician cannot review their own corrective submission.");
        if (submission.VersionNumber != breakdown.SubmissionVersion)
            throw new AppException(409, "Only the latest pending corrective submission can be reviewed.");
        if (await db.CorrectiveApprovals.AnyAsync(x => x.CorrectiveSubmissionId == submission.Id, ct))
            throw new AppException(409, "This corrective submission already has a final decision.");
        var remarks = decision == CorrectiveReviewDecision.REJECTED
            ? Guard.Required(request.Remarks, "Rejection remarks", 10000)
            : ExecutionRules.Optional(request.Remarks, "Remarks", 10000);
        var supervisor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId && x.IsActive &&
            x.UserRoles.Any(role => role.Role.Name == RoleNames.Supervisor), ct)
            ?? throw new AppException(403, "Corrective review requires an active supervisor.");
        var now = clock.GetUtcNow().UtcDateTime;
        var approval = new CorrectiveApproval { BreakdownId = id, CorrectiveSubmissionId = submission.Id,
            SupervisorId = supervisor.Id, SupervisorEmployeeId = supervisor.EmployeeId,
            SupervisorName = $"{supervisor.FirstName} {supervisor.LastName}".Trim(), Decision = decision, Remarks = remarks, DecisionAt = now };
        db.CorrectiveApprovals.Add(approval);
        breakdown.Status = target;
        if (decision == CorrectiveReviewDecision.APPROVED) breakdown.ClosedAt = now;
        var action = decision == CorrectiveReviewDecision.APPROVED ? "Corrective.Approved" : "Corrective.Rejected";
        var details = new { ReviewId = approval.Id, SubmissionId = submission.Id, submission.VersionNumber,
            approval.SupervisorId, approval.Decision, approval.DecisionAt, approval.Remarks };
        BreakdownHistory.Record(db, currentUser, breakdown, action, now, submission.Id, submission.VersionNumber, details);
        audit.Record(action, nameof(Breakdown), id, newValues: details);
        if (decision == CorrectiveReviewDecision.APPROVED)
        {
            BreakdownHistory.Record(db, currentUser, breakdown, "Breakdown.Closed", now,
                submission.Id, submission.VersionNumber, new { breakdown.ClosedAt });
            audit.Record("Breakdown.Closed", nameof(Breakdown), id, newValues: new { breakdown.ClosedAt, submission.Id });
        }
        await new BreakdownNotificationService(db, currentUser, audit, clock).EnsureAsync(breakdown,
            decision == CorrectiveReviewDecision.APPROVED ? NotificationType.CORRECTIVE_APPROVED : NotificationType.CORRECTIVE_REJECTED,
            submission.Id, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(approval);
    }

    public static CorrectiveApprovalDto ToDto(CorrectiveApproval x) => new(x.Id, x.BreakdownId,
        x.CorrectiveSubmissionId, x.SupervisorId, x.SupervisorEmployeeId, x.SupervisorName, x.Decision, x.Remarks, x.DecisionAt);
}
