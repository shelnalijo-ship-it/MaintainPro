using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public static class NotificationRecipientResolver
{
    public static async Task<NotificationRecipients> ResolveAsync(IApplicationDbContext db, WorkOrder order,
        NotificationType type, CancellationToken ct = default)
    {
        var technicianActive = order.AssignedTechnicianId.HasValue && await db.Users.AnyAsync(x =>
            x.Id == order.AssignedTechnicianId && x.IsActive && x.UserRoles.Any(role => role.Role.Name == RoleNames.Technician), ct);
        var supervisorActive = await db.Users.AnyAsync(x => x.Id == order.SupervisorId && x.IsActive &&
            x.UserRoles.Any(role => role.Role.Name == RoleNames.Supervisor), ct);
        var recipients = new HashSet<Guid>();
        var reasons = new List<string>();
        List<Guid>? managers = null;
        var complete = true;

        async Task ManagersAsync()
        {
            managers ??= await db.Users.AsNoTracking().Where(x => x.IsActive &&
                x.UserRoles.Any(role => role.Role.Name == RoleNames.Manager)).Select(x => x.Id).ToListAsync(ct);
            recipients.UnionWith(managers);
            if (managers.Count == 0) { complete = false; reasons.Add("No active MANAGER recipient is available."); }
        }
        async Task SupervisorAsync()
        {
            if (supervisorActive) recipients.Add(order.SupervisorId);
            else { reasons.Add("No active assigned supervisor; manager attention is required."); await ManagersAsync(); }
        }
        async Task TechnicianAsync()
        {
            if (technicianActive) recipients.Add(order.AssignedTechnicianId!.Value);
            else { reasons.Add("No active assigned technician; supervisor or manager attention is required."); await SupervisorAsync(); }
        }

        switch (type)
        {
            case NotificationType.WORK_ORDER_ASSIGNED:
            case NotificationType.WORK_ORDER_REASSIGNED:
            case NotificationType.WORK_ORDER_DUE_SOON:
            case NotificationType.WORK_ORDER_DUE:
            case NotificationType.WORK_ORDER_APPROVED:
            case NotificationType.WORK_ORDER_REJECTED:
                await TechnicianAsync();
                break;
            case NotificationType.WORK_ORDER_SUBMITTED:
            case NotificationType.ESCALATION_SUPERVISOR:
                await SupervisorAsync();
                break;
            case NotificationType.WORK_ORDER_OVERDUE:
                await TechnicianAsync();
                await SupervisorAsync();
                break;
            case NotificationType.ESCALATION_MANAGER:
                await ManagersAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
        var reason = reasons.Count == 0 ? null : string.Join(" ", reasons.Distinct());
        return new(recipients.OrderBy(x => x).ToArray(), reason, complete && recipients.Count > 0);
    }
}

public static class NotificationDeduplication
{
    public static string ForWorkOrder(Guid workOrderId, NotificationType type, Guid recipientId, Guid? eventId = null) =>
        eventId.HasValue
            ? $"work-order:{workOrderId:N}:{type}:event:{eventId.Value:N}:user:{recipientId:N}"
            : $"work-order:{workOrderId:N}:{type}:user:{recipientId:N}";
}
