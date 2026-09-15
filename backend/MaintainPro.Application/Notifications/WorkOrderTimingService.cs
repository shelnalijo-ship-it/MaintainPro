using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Notifications;

public sealed record WorkOrderTiming(bool IsDueSoon, bool IsDueToday, bool IsOverdue,
    int DaysOverdue, int EscalationLevel);

public interface IWorkOrderTimingService
{
    WorkOrderTiming Evaluate(WorkOrder order, EscalationSettings settings, DateTime utcNow);
}

/// <summary>Evaluates execution deadlines using UTC calendar dates without changing lifecycle state.</summary>
public sealed class WorkOrderTimingService : IWorkOrderTimingService
{
    public WorkOrderTiming Evaluate(WorkOrder order, EscalationSettings settings, DateTime utcNow)
    {
        // SubmittedAt remains present during rejection/correction. A correction deadline is a
        // separate future policy; it must not silently restart the original execution reminders.
        if (order.SubmittedAt.HasValue || order.LifecycleStatus is WorkOrderLifecycleStatus.APPROVED
            or WorkOrderLifecycleStatus.CANCELLED or WorkOrderLifecycleStatus.AWAITING_APPROVAL)
            return new(false, false, false, 0, 0);

        var difference = DateOnly.FromDateTime(utcNow).DayNumber - order.DueDate.DayNumber;
        if (difference < 0)
            return new(-difference <= settings.DueSoonDays, false, false, 0, 0);
        if (difference == 0) return new(false, true, false, 0, 0);

        var level = difference >= settings.ManagerEscalationDays ? 3
            : difference >= settings.SupervisorEscalationDays ? 2
            : difference >= settings.TechnicianOverdueDays ? 1 : 0;
        return new(false, false, true, difference, level);
    }
}
