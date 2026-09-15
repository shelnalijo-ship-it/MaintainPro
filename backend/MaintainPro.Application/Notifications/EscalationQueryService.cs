using System.Linq.Expressions;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public sealed class EscalationQueryService(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<EscalationDto>> ListAsync(EscalationQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        NotificationService.ValidateDates(request.From, request.To);
        if (request.Level.HasValue && request.Level is < 1 or > 3)
            throw new AppException(400, "Level must be between 1 and 3.");
        var query = VisibleEscalations().AsNoTracking();
        if (request.WorkOrderId.HasValue) query = query.Where(x => x.WorkOrderId == request.WorkOrderId);
        if (request.TechnicianId.HasValue) query = query.Where(x => x.WorkOrder.AssignedTechnicianId == request.TechnicianId);
        if (request.SupervisorId.HasValue) query = query.Where(x => x.WorkOrder.SupervisorId == request.SupervisorId);
        if (request.Level.HasValue) query = query.Where(x => x.Level == request.Level);
        if (request.UnresolvedOnly == true) query = query.Where(x => x.ResolvedAt == null);
        if (request.From.HasValue)
        {
            var from = request.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.TriggeredAt >= from);
        }
        if (request.To.HasValue)
        {
            var to = request.To.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.TriggeredAt <= to);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Level).ThenByDescending(x => x.TriggeredAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Projection).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<EscalationDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await VisibleEscalations().AsNoTracking().Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new AppException(404, "Escalation not found.");

    private IQueryable<WorkOrderEscalation> VisibleEscalations()
    {
        Guard.RequireRole(currentUser, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);
        if (currentUser.Roles.Contains(RoleNames.Manager) || currentUser.Roles.Contains(RoleNames.Admin))
            return db.WorkOrderEscalations;
        var actorId = currentUser.UserId!.Value;
        return db.WorkOrderEscalations.Where(x => x.WorkOrder.SupervisorId == actorId);
    }

    private static readonly Expression<Func<WorkOrderEscalation, EscalationDto>> Projection = x => new(x.Id,
        x.WorkOrderId, x.WorkOrder.WorkOrderNumber, x.WorkOrder.MachineId,
        x.WorkOrder.Definition.MachineCode, x.WorkOrder.Definition.MachineName,
        x.WorkOrder.AssignedTechnicianId, x.WorkOrder.SupervisorId, x.Level, x.TriggerType,
        x.TriggeredAt, x.RecipientUserId, x.NotificationId, x.ResolvedAt, x.RoutingReason);
}
