using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Planning;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.WorkOrders;

public sealed class WorkOrderGenerationService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, RecurrenceService recurrence, TimeProvider clock,
    WorkOrderNumberAllocator numbers, IGenerationConcurrency concurrency)
{
    public async Task<GenerationSummary> GenerateDueAsync(GenerationRequest? request = null, CancellationToken ct = default)
    {
        // Only the in-process scheduler uses a null actor; HTTP entry points require manager authorization.
        if (currentUser.UserId.HasValue) Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        request ??= new();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var through = request.ThroughDate ?? today;
        if (through > today || through == default)
            throw new AppException(400, "ThroughDate must be a valid date on or before today in UTC.");
        if (request.MaxOccurrences is < 1 or > 1000)
            throw new AppException(400, "MaxOccurrences must be between 1 and 1000.");

        var planIds = await db.MaintenancePlans.AsNoTracking()
            .Where(x => x.IsActive && x.NextDueDate <= through)
            .OrderBy(x => x.NextDueDate).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var queue = new Queue<Guid>(planIds);
        var evaluated = new HashSet<Guid>();
        var issues = new List<GenerationIssue>();
        var skipped = 0;
        var created = 0;
        var advanced = 0;
        // Round-robin catch-up lets each eligible plan progress before an older plan consumes the batch.
        while (advanced < request.MaxOccurrences && queue.TryDequeue(out var planId))
        {
            ct.ThrowIfCancellationRequested();
            evaluated.Add(planId);
            var result = await GenerateOccurrenceWithRetryAsync(planId, through, ct);
            if (result.Error is not null)
            {
                issues.Add(new(planId, result.Error));
                continue;
            }
            if (!result.Advanced)
            {
                skipped++;
                continue;
            }
            advanced++;
            if (result.Created) created++;
            else skipped++;
            if (result.StillDue) queue.Enqueue(planId);
        }

        // Failed/disabled-machine plans retain their cursor and can be retried after configuration is corrected.
        var hasMore = await db.MaintenancePlans.AsNoTracking().AnyAsync(x => x.IsActive && x.NextDueDate <= through, ct);
        return new(evaluated.Count, created, skipped, issues.Count, hasMore, issues);
    }

    private async Task<OccurrenceResult> GenerateOccurrenceWithRetryAsync(Guid planId, DateOnly through, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            concurrency.ResetTracking();
            try
            {
                return await GenerateOccurrenceAsync(planId, through, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) when (concurrency.IsRetryable(exception))
            {
                if (attempt == 4)
                    return new(false, false, false, "Generation encountered concurrent changes; retry this plan.");
                // Every retry starts a fresh serializable transaction and reloads all operational facts.
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
            }
            catch (AppException exception)
            {
                return new(false, false, false, exception.Message);
            }
            catch (Exception)
            {
                // Do not expose connection details or provider exception text in administrative summaries.
                return new(false, false, false, "Generation failed for this plan; its schedule was not advanced.");
            }
            finally
            {
                concurrency.ResetTracking();
            }
        }
        throw new InvalidOperationException("The bounded retry loop exited unexpectedly.");
    }

    private async Task<OccurrenceResult> GenerateOccurrenceAsync(Guid planId, DateOnly through, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var plan = await db.MaintenancePlans
            .Include(x => x.Machine).Include(x => x.MaintenanceType)
            .Include(x => x.Supervisor).ThenInclude(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.DefaultTechnician).ThenInclude(x => x!.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == planId, ct);
        if (plan is null || !plan.IsActive || plan.NextDueDate > through ||
            !plan.Machine.IsActive || plan.Machine.Status == MachineStatus.Decommissioned)
            return new(false, false, false);
        if (!plan.MaintenanceType.IsActive)
            throw new AppException(409, "The maintenance type is inactive; reactivate it or update the plan.");
        if (!HasRole(plan.Supervisor, RoleNames.Supervisor))
            throw new AppException(409, "The plan requires an active supervisor with the SUPERVISOR role.");

        var occurrence = plan.NextDueDate;
        if (occurrence < plan.StartDate)
            throw new AppException(409, "The plan's next occurrence precedes its start date.");
        var next = recurrence.Next(plan.StartDate, occurrence, plan.FrequencyType, plan.FrequencyValue);
        if (await db.WorkOrders.AnyAsync(x => x.MaintenancePlanId == plan.Id && x.PlannedDate == occurrence, ct))
        {
            // Recover a stale cursor without allocating a second number or altering the existing snapshot.
            plan.NextDueDate = next;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(false, true, next <= through);
        }

        var template = await db.ChecklistTemplates.AsNoTracking().Include(x => x.Items)
            .Where(x => x.MaintenancePlanId == plan.Id && x.IsActive)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (template is null || template.Items.Count == 0)
            throw new AppException(409, "The plan requires an active checklist version containing at least one item.");
        var technician = HasRole(plan.DefaultTechnician, RoleNames.Technician) ? plan.DefaultTechnician : null;
        var now = clock.GetUtcNow().UtcDateTime;
        var workOrder = new WorkOrder
        {
            WorkOrderNumber = await numbers.AllocateAsync(occurrence, ct),
            MachineId = plan.MachineId,
            MaintenancePlanId = plan.Id,
            AssignedTechnicianId = technician?.Id,
            SupervisorId = plan.SupervisorId,
            PlannedDate = occurrence,
            DueDate = occurrence,
            Priority = plan.Priority,
            LifecycleStatus = technician is null ? WorkOrderLifecycleStatus.PLANNED : WorkOrderLifecycleStatus.ASSIGNED,
            CreatedAt = now,
            UpdatedAt = now
        };
        workOrder.Definition = Snapshot(plan, template, technician, workOrder.Id);
        db.WorkOrders.Add(workOrder);
        plan.NextDueDate = next;
        audit.Record("WorkOrder.Generated", nameof(WorkOrder), workOrder.Id, newValues: new
        {
            workOrder.WorkOrderNumber, workOrder.MachineId, workOrder.MaintenancePlanId,
            workOrder.PlannedDate, workOrder.DueDate, workOrder.AssignedTechnicianId,
            workOrder.SupervisorId, workOrder.Priority, workOrder.LifecycleStatus,
            ChecklistTemplateId = template.Id, ChecklistVersion = template.Version,
            NextDueDate = next
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, true, next <= through);
    }

    private static WorkOrderDefinition Snapshot(MaintenancePlan plan, ChecklistTemplate template, User? technician, Guid workOrderId)
    {
        var definition = new WorkOrderDefinition
        {
            WorkOrderId = workOrderId,
            MaintenanceTypeId = plan.MaintenanceTypeId,
            ChecklistTemplateId = template.Id,
            ChecklistVersion = template.Version,
            ChecklistName = template.Name,
            PlanName = plan.PlanName,
            MaintenanceTypeName = plan.MaintenanceType.Name,
            Instructions = plan.Instructions,
            EstimatedDurationMinutes = plan.EstimatedDurationMinutes,
            PhotoRequired = plan.PhotoRequired,
            MinimumPhotoCount = plan.MinimumPhotoCount,
            CommentRequired = plan.CommentRequired,
            Priority = plan.Priority,
            MachineCode = plan.Machine.MachineCode,
            MachineName = plan.Machine.Name,
            AssignedTechnicianEmployeeId = technician?.EmployeeId,
            AssignedTechnicianName = technician is null ? null : FullName(technician),
            SupervisorEmployeeId = plan.Supervisor.EmployeeId,
            SupervisorName = FullName(plan.Supervisor)
        };
        definition.Items = template.Items.OrderBy(x => x.SequenceNumber).Select(x => new WorkOrderChecklistItem
        {
            WorkOrderDefinitionId = definition.Id,
            SequenceNumber = x.SequenceNumber,
            Title = x.Title,
            Description = x.Description,
            ResponseType = x.ResponseType,
            IsMandatory = x.IsMandatory,
            Unit = x.Unit,
            MinimumValue = x.MinimumValue,
            MaximumValue = x.MaximumValue,
            PhotoRequired = x.PhotoRequired
        }).ToList();
        return definition;
    }

    private static bool HasRole(User? user, string role) => user is { IsActive: true } &&
        user.UserRoles.Any(x => x.Role.Name == role);
    private static string FullName(User user) => $"{user.FirstName} {user.LastName}".Trim();
    private sealed record OccurrenceResult(bool Created, bool Advanced, bool StillDue, string? Error = null);
}
