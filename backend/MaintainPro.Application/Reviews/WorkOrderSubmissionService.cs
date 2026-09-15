using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reviews;

public sealed class WorkOrderSubmissionService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<WorkOrderSubmissionDto> SubmitAsync(Guid workOrderId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, workOrderId, ct: ct);
        ExecutionAccess.RequireTechnician(currentUser, order);
        ExecutionRules.RequireTransition(order, WorkOrderLifecycleStatus.AWAITING_APPROVAL);
        var execution = await db.WorkOrderExecutions.SingleOrDefaultAsync(x => x.WorkOrderId == workOrderId, ct)
            ?? throw new AppException(409, "Start and complete execution before submitting this work order.");
        if (execution.TechnicianId != currentUser.UserId)
            throw new AppException(409, "The execution record belongs to a different technician.");
        var results = await db.WorkOrderChecklistResults.AsNoTracking()
            .Where(x => x.WorkOrderId == workOrderId).ToListAsync(ct);
        var attachments = await db.WorkOrderAttachments.AsNoTracking().Include(x => x.File)
            .Where(x => x.WorkOrderId == workOrderId && !x.IsDeleted).ToListAsync(ct);
        var parts = await db.SparePartUsages.AsNoTracking().Where(x => x.WorkOrderId == workOrderId).ToListAsync(ct);
        var defects = await db.WorkOrderDefects.AsNoTracking().Where(x => x.WorkOrderId == workOrderId).ToListAsync(ct);
        var itemIds = order.Definition.Items.Select(x => x.Id).ToHashSet();
        if (results.Any(x => !itemIds.Contains(x.WorkOrderChecklistItemId)) ||
            attachments.Any(x => x.WorkOrderChecklistItemId.HasValue && !itemIds.Contains(x.WorkOrderChecklistItemId.Value)))
            throw new AppException(409, "Execution data must refer to this work order's immutable checklist.");
        ExecutionRules.ValidateForSubmission(order, execution, results, attachments);
        if (order.SubmissionVersion == int.MaxValue)
            throw new AppException(409, "This work order has reached its submission-version limit.");

        var now = clock.GetUtcNow().UtcDateTime;
        // The parent concurrency token and serializable transaction protect this persistent counter.
        // A second request must reload the changed lifecycle rather than create another version.
        order.SubmissionVersion++;
        var submission = new WorkOrderSubmission
        {
            WorkOrderId = order.Id,
            VersionNumber = order.SubmissionVersion,
            SubmittedByUserId = currentUser.UserId!.Value,
            TechnicianEmployeeId = execution.TechnicianEmployeeId,
            TechnicianName = execution.TechnicianName,
            SubmittedAt = now,
            OverallComments = execution.OverallComments,
            Observations = execution.Observations,
            StartedAt = order.StartedAt!.Value,
            CompletedAt = order.CompletedAt!.Value,
            DurationMinutes = execution.AccumulatedDurationMinutes,
            CreatedAt = now
        };
        submission.ChecklistResults = results.Select(x => new WorkOrderSubmissionChecklistResult
        {
            WorkOrderSubmissionId = submission.Id,
            WorkOrderChecklistItemId = x.WorkOrderChecklistItemId,
            BooleanValue = x.BooleanValue,
            NumericValue = x.NumericValue,
            TextValue = x.TextValue,
            PassFailValue = x.PassFailValue,
            ConfirmationValue = x.ConfirmationValue,
            Comment = x.Comment,
            CompletedAt = x.CompletedAt,
            CompletedByUserId = x.CompletedByUserId
        }).ToList();
        submission.Attachments = attachments.Select(x => new WorkOrderSubmissionAttachment
        {
            WorkOrderSubmissionId = submission.Id,
            FileId = x.FileId,
            WorkOrderChecklistItemId = x.WorkOrderChecklistItemId,
            EvidenceType = x.EvidenceType,
            Description = x.Description,
            UploadedByUserId = x.UploadedByUserId,
            UploadedAt = x.UploadedAt
        }).ToList();
        submission.PartUsages = parts.Select(x => new WorkOrderSubmissionPartUsage
        {
            WorkOrderSubmissionId = submission.Id,
            PartName = x.PartName,
            PartNumber = x.PartNumber,
            Quantity = x.Quantity,
            Remarks = x.Remarks,
            CreatedByUserId = x.CreatedByUserId,
            CreatedAt = x.CreatedAt
        }).ToList();
        submission.Defects = defects.Select(x => new WorkOrderSubmissionDefect
        {
            WorkOrderSubmissionId = submission.Id,
            Title = x.Title,
            Description = x.Description,
            Severity = x.Severity,
            RequiresFollowUp = x.RequiresFollowUp,
            CreatedByUserId = x.CreatedByUserId,
            CreatedAt = x.CreatedAt
        }).ToList();
        db.WorkOrderSubmissions.Add(submission);
        order.LifecycleStatus = WorkOrderLifecycleStatus.AWAITING_APPROVAL;
        order.SubmittedAt = now;
        ExecutionHistory.Record(db, order, currentUser, clock, "WorkOrder.Submitted", submission.Id,
            $"Submitted version {submission.VersionNumber}.", execution.TechnicianName);
        audit.Record("WorkOrder.Submitted", nameof(WorkOrder), order.Id, newValues: new
        {
            SubmissionId = submission.Id, submission.VersionNumber, submission.SubmittedAt,
            submission.SubmittedByUserId, submission.DurationMinutes,
            ChecklistResultCount = results.Count, AttachmentCount = attachments.Count,
            PartUsageCount = parts.Count, DefectCount = defects.Count, order.LifecycleStatus
        });
        await MaintainPro.Application.Notifications.WorkflowNotificationHooks.SubmittedAsync(
            db, order, submission, currentUser, audit, clock, ct);
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(workOrderId, submission.Id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<IReadOnlyList<SubmissionSummaryDto>> ListAsync(Guid workOrderId, CancellationToken ct = default)
    {
        await ExecutionAccess.LoadVisibleAsync(db, currentUser, workOrderId,
            tracking: false, includeDefinition: false, ct: ct);
        var submissions = await VisibleSubmissions(workOrderId).Include(x => x.Review)
            .OrderBy(x => x.VersionNumber).ToListAsync(ct);
        return submissions.Select(ToSummary).ToArray();
    }

    public async Task<WorkOrderSubmissionDto> GetAsync(Guid workOrderId, Guid submissionId, CancellationToken ct = default)
    {
        var submissions = VisibleSubmissions(workOrderId);
        var submission = await submissions.Include(x => x.Review).SingleOrDefaultAsync(x => x.Id == submissionId, ct)
            ?? throw new AppException(404, "Submission not found.");
        // Read immutable submission rows. Draft edits after rejection never participate in this response.
        var results = await db.WorkOrderSubmissionChecklistResults.AsNoTracking().Include(x => x.WorkOrderChecklistItem)
            .Where(x => x.WorkOrderSubmissionId == submissionId && submissions.Any(s => s.Id == x.WorkOrderSubmissionId))
            .OrderBy(x => x.WorkOrderChecklistItem.SequenceNumber).ToListAsync(ct);
        var attachments = await db.WorkOrderSubmissionAttachments.AsNoTracking()
            .Where(x => x.WorkOrderSubmissionId == submissionId && submissions.Any(s => s.Id == x.WorkOrderSubmissionId))
            .OrderBy(x => x.UploadedAt).ThenBy(x => x.Id)
            .Select(x => new SubmissionAttachmentDto(x.Id, x.FileId, x.WorkOrderChecklistItemId,
                x.File.OriginalFilename, x.File.MimeType, x.File.FileSize, x.EvidenceType,
                x.Description, x.UploadedByUserId, x.UploadedAt)).ToListAsync(ct);
        var parts = await db.WorkOrderSubmissionPartUsages.AsNoTracking()
            .Where(x => x.WorkOrderSubmissionId == submissionId && submissions.Any(s => s.Id == x.WorkOrderSubmissionId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new SubmissionPartUsageDto(x.Id, x.PartName, x.PartNumber, x.Quantity,
                x.Remarks, x.CreatedByUserId, x.CreatedAt)).ToListAsync(ct);
        var defects = await db.WorkOrderSubmissionDefects.AsNoTracking()
            .Where(x => x.WorkOrderSubmissionId == submissionId && submissions.Any(s => s.Id == x.WorkOrderSubmissionId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new SubmissionDefectDto(x.Id, x.Title, x.Description, x.Severity,
                x.RequiresFollowUp, x.CreatedByUserId, x.CreatedAt)).ToListAsync(ct);
        return new(ToSummary(submission), submission.OverallComments, submission.Observations, submission.StartedAt,
            results.Select(x => new SubmissionChecklistResultDto(x.Id, x.WorkOrderChecklistItemId,
                x.WorkOrderChecklistItem.SequenceNumber, x.WorkOrderChecklistItem.Title,
                x.WorkOrderChecklistItem.ResponseType, x.WorkOrderChecklistItem.IsMandatory,
                x.WorkOrderChecklistItem.Unit, x.WorkOrderChecklistItem.MinimumValue, x.WorkOrderChecklistItem.MaximumValue,
                x.BooleanValue, x.NumericValue, ExecutionRules.ReadingStatus(x.WorkOrderChecklistItem, x.NumericValue),
                x.TextValue, x.PassFailValue, x.ConfirmationValue, x.Comment, x.CompletedAt, x.CompletedByUserId)).ToArray(),
            attachments, parts, defects);
    }

    private IQueryable<WorkOrderSubmission> VisibleSubmissions(Guid workOrderId)
    {
        var visibleOrders = ExecutionAccess.VisibleWorkOrders(db, currentUser);
        return db.WorkOrderSubmissions.AsNoTracking().Where(x => x.WorkOrderId == workOrderId &&
            visibleOrders.Any(order => order.Id == x.WorkOrderId));
    }

    private static SubmissionSummaryDto ToSummary(WorkOrderSubmission submission) => new(
        submission.Id, submission.WorkOrderId, submission.VersionNumber, submission.SubmittedByUserId,
        submission.TechnicianEmployeeId, submission.TechnicianName, submission.SubmittedAt,
        submission.CompletedAt, submission.DurationMinutes,
        submission.Review is null ? null : ReviewMapping.ToDto(submission.Review));
}

internal static class ReviewMapping
{
    public static WorkOrderReviewDto ToDto(WorkOrderApproval review) => new(review.Id, review.WorkOrderId,
        review.WorkOrderSubmissionId, review.SupervisorId, review.SupervisorEmployeeId,
        review.SupervisorName, review.Decision, review.Remarks, review.DecisionAt);
}
