using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;

namespace MaintainPro.Application.Execution;

public static class ExecutionHistory
{
    public static void Record(IApplicationDbContext db, WorkOrder order, ICurrentUser user,
        TimeProvider clock, string action, Guid? submissionId = null, string? details = null,
        string? actorName = null)
    {
        if (!db.HasActiveTransaction)
            throw new InvalidOperationException("Work-order history requires the workflow transaction.");
        order.HistoryVersion = checked(order.HistoryVersion + 1);
        db.WorkOrderHistoryEvents.Add(new WorkOrderHistoryEvent
        {
            WorkOrderId = order.Id, SequenceNumber = order.HistoryVersion, Action = action,
            ActorUserId = user.UserId, ActorName = actorName,
            OccurredAt = clock.GetUtcNow().UtcDateTime, WorkOrderSubmissionId = submissionId,
            Details = details
        });
    }
}
