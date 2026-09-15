using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Execution;

public sealed class WorkOrderExecutionService(
    IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock)
{
    public async Task<WorkOrderExecutionDto> StartAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, id, ct: ct);
        ExecutionAccess.RequireTechnician(currentUser, order);
        if (order.LifecycleStatus is not (WorkOrderLifecycleStatus.PLANNED or WorkOrderLifecycleStatus.ASSIGNED))
            throw new AppException(409, "Only a planned or assigned work order can be started; use resume after rejection.");
        ExecutionRules.RequireTransition(order, WorkOrderLifecycleStatus.IN_PROGRESS);
        var technician = await db.Users.SingleAsync(x => x.Id == currentUser.UserId, ct);
        if (!technician.IsActive) throw new AppException(403, "An inactive technician cannot start work.");
        var now = Now;
        var execution = new WorkOrderExecution
        {
            WorkOrderId = id, TechnicianId = technician.Id, TechnicianEmployeeId = technician.EmployeeId,
            TechnicianName = $"{technician.FirstName} {technician.LastName}".Trim(),
            AttemptStartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        db.WorkOrderExecutions.Add(execution);
        order.StartedAt = now;
        order.LifecycleStatus = WorkOrderLifecycleStatus.IN_PROGRESS;
        RecordWorkflow(order, execution, "WorkOrder.Started", new { order.StartedAt, execution.TechnicianId });
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<WorkOrderExecutionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, id, tracking: false, ct: ct);
        var execution = await db.WorkOrderExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.WorkOrderId == id, ct);
        var results = await db.WorkOrderChecklistResults.AsNoTracking().Where(x => x.WorkOrderId == id).ToListAsync(ct);
        var parts = await db.SparePartUsages.AsNoTracking().Where(x => x.WorkOrderId == id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var defects = await db.WorkOrderDefects.AsNoTracking().Where(x => x.WorkOrderId == id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var attachments = await db.WorkOrderAttachments.AsNoTracking().Include(x => x.File)
            .Where(x => x.WorkOrderId == id && !x.IsDeleted).OrderBy(x => x.UploadedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var definitions = order.Definition.Items.ToDictionary(x => x.Id);
        var duration = execution?.AccumulatedDurationMinutes ?? 0;
        if (execution?.AttemptStartedAt is DateTime attempt) duration += ElapsedMinutes(attempt, Now);
        return new(id, order.LifecycleStatus, execution?.OverallComments, execution?.Observations,
            order.StartedAt, order.CompletedAt, order.SubmittedAt, order.ApprovedAt, duration,
            order.CompletedAt.HasValue && execution?.AttemptStartedAt is null,
            execution?.TechnicianId, execution?.TechnicianEmployeeId, execution?.TechnicianName,
            results.OrderBy(x => definitions[x.WorkOrderChecklistItemId].SequenceNumber)
                .Select(x => ToDto(x, definitions[x.WorkOrderChecklistItemId])).ToArray(),
            parts.Select(ToDto).ToArray(), defects.Select(ToDto).ToArray(), attachments.Select(ToDto).ToArray());
    }

    public async Task<WorkOrderExecutionDto> SaveChecklistResultsAsync(Guid id, ChecklistResultsRequest request,
        CancellationToken ct = default)
    {
        if (request.Results is null || request.Results.Count is < 1 or > 500)
            throw new AppException(400, "Supply between 1 and 500 checklist results.");
        if (request.Results.Any(x => x is null) || request.Results.Select(x => x.WorkOrderChecklistItemId).Distinct().Count() != request.Results.Count)
            throw new AppException(400, "Each checklist item must appear exactly once in a request.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var items = order.Definition.Items.ToDictionary(x => x.Id);
        var normalized = new List<WorkOrderChecklistResult>();
        foreach (var value in request.Results)
        {
            if (!items.TryGetValue(value.WorkOrderChecklistItemId, out var item))
                throw new AppException(400, "Checklist result must reference an item belonging to this work order.");
            var result = new WorkOrderChecklistResult
            {
                WorkOrderId = id, WorkOrderChecklistItemId = item.Id, BooleanValue = value.BooleanValue,
                NumericValue = value.NumericValue, TextValue = ExecutionRules.Optional(value.TextValue, "TextValue", 10000),
                PassFailValue = value.PassFailValue, ConfirmationValue = value.ConfirmationValue,
                Comment = ExecutionRules.Optional(value.Comment, "Comment", 10000),
                CompletedByUserId = currentUser.UserId!.Value, UpdatedAt = Now
            };
            ExecutionRules.ValidateAnswer(item, result);
            result.CompletedAt = ExecutionRules.IsSatisfied(item, result, Array.Empty<WorkOrderAttachment>()) ? Now : null;
            normalized.Add(result);
        }
        var existing = await db.WorkOrderChecklistResults.Where(x => x.WorkOrderId == id).ToDictionaryAsync(x => x.WorkOrderChecklistItemId, ct);
        var changed = 0;
        foreach (var value in normalized)
        {
            if (!existing.TryGetValue(value.WorkOrderChecklistItemId, out var result))
            {
                db.WorkOrderChecklistResults.Add(value);
                changed++;
            }
            else if (!SameAnswer(result, value))
            {
                result.BooleanValue = value.BooleanValue; result.NumericValue = value.NumericValue;
                result.TextValue = value.TextValue; result.PassFailValue = value.PassFailValue;
                result.ConfirmationValue = value.ConfirmationValue; result.Comment = value.Comment;
                result.CompletedAt = value.CompletedAt; result.CompletedByUserId = value.CompletedByUserId;
                result.UpdatedAt = value.UpdatedAt; changed++;
            }
        }
        if (changed > 0)
        {
            TouchDraft(order, execution, Now);
            audit.Record("WorkOrder.ChecklistChanged", nameof(WorkOrder), id,
                newValues: new { ChangedItems = changed, ChecklistItemIds = normalized.Select(x => x.WorkOrderChecklistItemId).ToArray() });
            await db.SaveChangesAsync(ct);
        }
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<WorkOrderExecutionDto> SaveCommentsAsync(Guid id, ExecutionCommentsRequest request, CancellationToken ct = default)
    {
        var comments = ExecutionRules.Optional(request.OverallComments, "OverallComments", 10000);
        var observations = ExecutionRules.Optional(request.Observations, "Observations", 10000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        if (execution.OverallComments != comments || execution.Observations != observations)
        {
            execution.OverallComments = comments; execution.Observations = observations;
            TouchDraft(order, execution, Now);
            audit.Record("WorkOrder.CommentsChanged", nameof(WorkOrder), id,
                newValues: new { HasOverallComments = comments is not null, HasObservations = observations is not null });
            await db.SaveChangesAsync(ct);
        }
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<PartUsageDto> AddPartAsync(Guid id, PartUsageRequest request, CancellationToken ct = default)
    {
        var value = Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var part = new SparePartUsage { WorkOrderId = id, PartName = value.PartName, PartNumber = value.PartNumber,
            Quantity = value.Quantity, Remarks = value.Remarks, CreatedByUserId = currentUser.UserId!.Value, CreatedAt = Now };
        db.SparePartUsages.Add(part);
        TouchDraft(order, execution, Now);
        audit.Record("WorkOrder.PartAdded", nameof(WorkOrder), id, newValues: new { PartId = part.Id, part.PartName, part.Quantity });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToDto(part);
    }

    public async Task<PartUsageDto> UpdatePartAsync(Guid id, Guid partId, PartUsageRequest request, CancellationToken ct = default)
    {
        var value = Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var part = await db.SparePartUsages.SingleOrDefaultAsync(x => x.WorkOrderId == id && x.Id == partId, ct)
            ?? throw new AppException(404, "Part usage not found.");
        var previous = new { part.PartName, part.Quantity };
        part.PartName = value.PartName; part.PartNumber = value.PartNumber; part.Quantity = value.Quantity; part.Remarks = value.Remarks;
        TouchDraft(order, execution, Now);
        audit.Record("WorkOrder.PartUpdated", nameof(WorkOrder), id, previous, new { PartId = part.Id, part.PartName, part.Quantity });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToDto(part);
    }

    public async Task DeletePartAsync(Guid id, Guid partId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var part = await db.SparePartUsages.SingleOrDefaultAsync(x => x.WorkOrderId == id && x.Id == partId, ct)
            ?? throw new AppException(404, "Part usage not found.");
        db.SparePartUsages.Remove(part); TouchDraft(order, execution, Now);
        audit.Record("WorkOrder.PartRemoved", nameof(WorkOrder), id, newValues: new { PartId = part.Id });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public async Task<DefectDto> AddDefectAsync(Guid id, DefectRequest request, CancellationToken ct = default)
    {
        var value = Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var defect = new WorkOrderDefect { WorkOrderId = id, Title = value.Title, Description = value.Description,
            Severity = value.Severity, RequiresFollowUp = value.RequiresFollowUp, CreatedByUserId = currentUser.UserId!.Value,
            CreatedAt = Now, UpdatedAt = Now };
        db.WorkOrderDefects.Add(defect); TouchDraft(order, execution, Now);
        audit.Record("WorkOrder.DefectAdded", nameof(WorkOrder), id, newValues: new { DefectId = defect.Id, defect.Title, defect.RequiresFollowUp });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToDto(defect);
    }

    public async Task<DefectDto> UpdateDefectAsync(Guid id, Guid defectId, DefectRequest request, CancellationToken ct = default)
    {
        var value = Normalize(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var defect = await db.WorkOrderDefects.SingleOrDefaultAsync(x => x.WorkOrderId == id && x.Id == defectId, ct)
            ?? throw new AppException(404, "Defect not found.");
        defect.Title = value.Title; defect.Description = value.Description; defect.Severity = value.Severity;
        defect.RequiresFollowUp = value.RequiresFollowUp; defect.UpdatedAt = Now;
        TouchDraft(order, execution, Now);
        audit.Record("WorkOrder.DefectUpdated", nameof(WorkOrder), id, newValues: new { DefectId = defect.Id, defect.Title, defect.RequiresFollowUp });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ToDto(defect);
    }

    public async Task DeleteDefectAsync(Guid id, Guid defectId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        var defect = await db.WorkOrderDefects.SingleOrDefaultAsync(x => x.WorkOrderId == id && x.Id == defectId, ct)
            ?? throw new AppException(404, "Defect not found.");
        db.WorkOrderDefects.Remove(defect); TouchDraft(order, execution, Now);
        audit.Record("WorkOrder.DefectRemoved", nameof(WorkOrder), id, newValues: new { DefectId = defect.Id });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public async Task<WorkOrderExecutionDto> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var (order, execution) = await EditableAsync(id, ct);
        if (order.CompletedAt.HasValue || execution.AttemptStartedAt is not DateTime started)
            throw new AppException(409, "Execution is already complete.");
        var now = Now;
        execution.AccumulatedDurationMinutes += ElapsedMinutes(started, now);
        execution.AttemptStartedAt = null; execution.UpdatedAt = now; order.CompletedAt = now;
        RecordWorkflow(order, execution, "WorkOrder.Completed", new { order.CompletedAt, DurationMinutes = execution.AccumulatedDurationMinutes });
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<WorkOrderExecutionDto> ResumeAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, id, ct: ct);
        ExecutionAccess.RequireTechnician(currentUser, order);
        if (order.LifecycleStatus != WorkOrderLifecycleStatus.REJECTED)
            throw new AppException(409, "Only a rejected work order can be resumed.");
        ExecutionRules.RequireTransition(order, WorkOrderLifecycleStatus.IN_PROGRESS);
        var execution = await FindExecutionAsync(id, ct);
        order.LifecycleStatus = WorkOrderLifecycleStatus.IN_PROGRESS;
        order.CompletedAt = null;
        execution.AttemptStartedAt = Now; execution.UpdatedAt = Now;
        RecordWorkflow(order, execution, "WorkOrder.Resumed", new { PreviousSubmissionVersion = order.SubmissionVersion });
        await db.SaveChangesAsync(ct);
        var response = await GetAsync(id, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    private async Task<(WorkOrder Order, WorkOrderExecution Execution)> EditableAsync(Guid id, CancellationToken ct)
    {
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, id, ct: ct);
        ExecutionAccess.RequireTechnician(currentUser, order); ExecutionRules.RequireEditable(order);
        return (order, await FindExecutionAsync(id, ct));
    }

    private async Task<WorkOrderExecution> FindExecutionAsync(Guid id, CancellationToken ct) =>
        await db.WorkOrderExecutions.SingleOrDefaultAsync(x => x.WorkOrderId == id, ct)
        ?? throw new AppException(409, "Start execution before editing it.");

    public static void TouchDraft(WorkOrder order, WorkOrderExecution execution, DateTime now)
    {
        if (order.CompletedAt.HasValue) { order.CompletedAt = null; execution.AttemptStartedAt = now; }
        execution.UpdatedAt = now;
        // Rotate even with a fixed clock: every draft write contends on the work-order token.
        order.Version = Guid.NewGuid();
    }

    private void RecordWorkflow(WorkOrder order, WorkOrderExecution execution, string action, object details)
    {
        ExecutionHistory.Record(db, order, currentUser, clock, action, actorName: execution.TechnicianName);
        audit.Record(action, nameof(WorkOrder), order.Id, newValues: details);
    }

    private static decimal ElapsedMinutes(DateTime start, DateTime end) =>
        Math.Max(0, (decimal)(end - start).Ticks / TimeSpan.TicksPerMinute);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static bool SameAnswer(WorkOrderChecklistResult left, WorkOrderChecklistResult right) =>
        left.BooleanValue == right.BooleanValue && left.NumericValue == right.NumericValue &&
        left.TextValue == right.TextValue && left.PassFailValue == right.PassFailValue &&
        left.ConfirmationValue == right.ConfirmationValue && left.Comment == right.Comment;
    private static PartUsageRequest Normalize(PartUsageRequest value)
    {
        if (value.Quantity <= 0) throw new AppException(400, "Quantity must be greater than zero.");
        return value with { PartName = Guard.Required(value.PartName, "PartName", 200),
            PartNumber = ExecutionRules.Optional(value.PartNumber, "PartNumber", 200),
            Remarks = ExecutionRules.Optional(value.Remarks, "Remarks", 10000) };
    }
    private static DefectRequest Normalize(DefectRequest value) => value with
    {
        Title = Guard.Required(value.Title, "Title", 200), Description = Guard.Required(value.Description, "Description", 10000),
        Severity = ExecutionRules.Optional(value.Severity, "Severity", 100)
    };
    private static ChecklistResultDto ToDto(WorkOrderChecklistResult value, WorkOrderChecklistItem item) =>
        new(value.Id, value.WorkOrderChecklistItemId, value.BooleanValue, value.NumericValue, value.TextValue,
            value.PassFailValue, value.ConfirmationValue, value.Comment, value.CompletedAt,
            value.CompletedByUserId, value.UpdatedAt, ExecutionRules.ReadingStatus(item, value.NumericValue));
    private static PartUsageDto ToDto(SparePartUsage value) =>
        new(value.Id, value.PartName, value.PartNumber, value.Quantity, value.Remarks, value.CreatedByUserId, value.CreatedAt);
    private static DefectDto ToDto(WorkOrderDefect value) =>
        new(value.Id, value.Title, value.Description, value.Severity, value.RequiresFollowUp,
            value.CreatedByUserId, value.CreatedAt, value.UpdatedAt);
    public static AttachmentDto ToDto(WorkOrderAttachment value) =>
        new(value.Id, value.FileId, value.WorkOrderChecklistItemId, value.EvidenceType, value.Description,
            value.File.OriginalFilename, value.File.MimeType, value.File.FileSize, value.UploadedByUserId, value.UploadedAt);
}
