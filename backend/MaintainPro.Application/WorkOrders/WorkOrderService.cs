using System.Linq.Expressions;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.WorkOrders;

public sealed class WorkOrderService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<PagedResult<WorkOrderSummaryDto>> ListAsync(WorkOrderQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        ValidateLifecycle(request.LifecycleStatus);
        if (request.Priority.HasValue && !Enum.IsDefined(request.Priority.Value))
            throw new AppException(400, "Priority is invalid.");
        ValidateRange(request.PlannedFrom, request.PlannedTo, "Planned");
        ValidateRange(request.DueFrom, request.DueTo, "Due");
        var today = Today;
        var query = VisibleWorkOrders().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search", 200).ToUpperInvariant();
            // Historical searches use the generated definition, even after machine or plan renaming.
            query = query.Where(x => x.WorkOrderNumber.ToUpper().Contains(search) ||
                x.Definition.MachineCode.ToUpper().Contains(search) ||
                x.Definition.MachineName.ToUpper().Contains(search) ||
                x.Definition.PlanName.ToUpper().Contains(search));
        }
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (request.MaintenancePlanId.HasValue) query = query.Where(x => x.MaintenancePlanId == request.MaintenancePlanId);
        if (request.TechnicianId.HasValue) query = query.Where(x => x.AssignedTechnicianId == request.TechnicianId);
        if (request.SupervisorId.HasValue) query = query.Where(x => x.SupervisorId == request.SupervisorId);
        if (request.LifecycleStatus.HasValue) query = query.Where(x => x.LifecycleStatus == request.LifecycleStatus);
        if (request.Priority.HasValue) query = query.Where(x => x.Priority == request.Priority);
        if (request.PlannedFrom.HasValue) query = query.Where(x => x.PlannedDate >= request.PlannedFrom);
        if (request.PlannedTo.HasValue) query = query.Where(x => x.PlannedDate <= request.PlannedTo);
        if (request.DueFrom.HasValue) query = query.Where(x => x.DueDate >= request.DueFrom);
        if (request.DueTo.HasValue) query = query.Where(x => x.DueDate <= request.DueTo);
        if (request.Overdue.HasValue)
            query = query.Where(x => (x.DueDate < today &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL) == request.Overdue.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.PlannedDate).ThenBy(x => x.WorkOrderNumber).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(SummaryProjection(today)).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<WorkOrderDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var workOrder = await VisibleWorkOrders().AsNoTracking()
            .Include(x => x.Definition).ThenInclude(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Work order not found.");
        var definition = workOrder.Definition;
        return new(ToSummary(workOrder, Today), workOrder.StartedAt, workOrder.CompletedAt,
            workOrder.SubmittedAt, workOrder.ApprovedAt, workOrder.CancelledAt,
            new(definition.Id, definition.MaintenanceTypeId, definition.MaintenanceTypeName,
                definition.ChecklistTemplateId, definition.ChecklistVersion, definition.ChecklistName,
                definition.PlanName, definition.Instructions, definition.EstimatedDurationMinutes,
                definition.PhotoRequired, definition.MinimumPhotoCount, definition.CommentRequired,
                definition.Priority, definition.MachineCode, definition.MachineName,
                definition.AssignedTechnicianEmployeeId, definition.AssignedTechnicianName,
                definition.SupervisorEmployeeId, definition.SupervisorName,
                definition.Items.OrderBy(x => x.SequenceNumber).Select(x => new WorkOrderChecklistItemDto(
                    x.Id, x.SequenceNumber, x.Title, x.Description, x.ResponseType, x.IsMandatory,
                    x.Unit, x.MinimumValue, x.MaximumValue, x.PhotoRequired)).ToArray()));
    }

    public async Task<IReadOnlyList<WorkOrderCalendarEventDto>> CalendarAsync(
        WorkOrderCalendarQuery request, CancellationToken ct = default)
    {
        if (request.From == default || request.To == default)
            throw new AppException(400, "Calendar from and to dates are required.");
        ValidateRange(request.From, request.To, "Calendar");
        if (request.To.DayNumber - request.From.DayNumber > 366)
            throw new AppException(400, "Calendar ranges cannot exceed 366 days.");
        ValidateLifecycle(request.LifecycleStatus);
        var today = Today;
        var query = VisibleWorkOrders().AsNoTracking()
            .Where(x => x.PlannedDate >= request.From && x.PlannedDate <= request.To);
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (request.TechnicianId.HasValue) query = query.Where(x => x.AssignedTechnicianId == request.TechnicianId);
        if (request.SupervisorId.HasValue) query = query.Where(x => x.SupervisorId == request.SupervisorId);
        if (request.LifecycleStatus.HasValue) query = query.Where(x => x.LifecycleStatus == request.LifecycleStatus);
        var events = await query.OrderBy(x => x.PlannedDate).ThenBy(x => x.WorkOrderNumber).ThenBy(x => x.Id)
            .Take(5001).Select(x => new WorkOrderCalendarEventDto(x.Id, x.WorkOrderNumber,
                x.Definition.PlanName, x.MachineId, x.Definition.MachineCode, x.Definition.MachineName,
                x.AssignedTechnicianId, x.Definition.AssignedTechnicianName, x.SupervisorId,
                x.Definition.SupervisorName, x.PlannedDate, x.DueDate, x.Priority, x.LifecycleStatus,
                x.DueDate < today && x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL)).ToListAsync(ct);
        if (events.Count > 5000)
            throw new AppException(400, "The calendar contains more than 5000 events; narrow its dates or filters.");
        return events;
    }

    private IQueryable<WorkOrder> VisibleWorkOrders()
    {
        var userId = Guard.Authenticated(currentUser);
        if (currentUser.Roles.Contains(RoleNames.Manager) || currentUser.Roles.Contains(RoleNames.Admin))
            return db.WorkOrders;
        var technician = currentUser.Roles.Contains(RoleNames.Technician);
        var supervisor = currentUser.Roles.Contains(RoleNames.Supervisor);
        return db.WorkOrders.Where(x => (technician && x.AssignedTechnicianId == userId) ||
            (supervisor && x.SupervisorId == userId));
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static void ValidateLifecycle(WorkOrderLifecycleStatus? value)
    {
        if (value.HasValue && !Enum.IsDefined(value.Value))
            throw new AppException(400, "LifecycleStatus is invalid.");
    }

    private static void ValidateRange(DateOnly? from, DateOnly? to, string name)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new AppException(400, $"{name} from date cannot follow its to date.");
    }

    private static Expression<Func<WorkOrder, WorkOrderSummaryDto>> SummaryProjection(DateOnly today) => x =>
        new(x.Id, x.WorkOrderNumber, x.MachineId, x.Definition.MachineCode, x.Definition.MachineName,
            x.MaintenancePlanId, x.Definition.PlanName, x.AssignedTechnicianId,
            x.Definition.AssignedTechnicianName, x.SupervisorId, x.Definition.SupervisorName,
            x.PlannedDate, x.DueDate, x.Priority, x.LifecycleStatus,
            x.DueDate < today && x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
            x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
            x.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL,
            x.EscalationLevel, x.CreatedAt, x.UpdatedAt);

    private static WorkOrderSummaryDto ToSummary(WorkOrder x, DateOnly today) =>
        new(x.Id, x.WorkOrderNumber, x.MachineId, x.Definition.MachineCode, x.Definition.MachineName,
            x.MaintenancePlanId, x.Definition.PlanName, x.AssignedTechnicianId,
            x.Definition.AssignedTechnicianName, x.SupervisorId, x.Definition.SupervisorName,
            x.PlannedDate, x.DueDate, x.Priority, x.LifecycleStatus,
            x.DueDate < today && x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
            x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
            x.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL,
            x.EscalationLevel, x.CreatedAt, x.UpdatedAt);
}
