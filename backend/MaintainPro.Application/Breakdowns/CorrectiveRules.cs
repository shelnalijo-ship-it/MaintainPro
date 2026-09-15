using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Breakdowns;

public static class CorrectiveRules
{
    public static void RequireTransition(Breakdown breakdown, BreakdownStatus target)
    {
        var allowed = (breakdown.Status, target) switch
        {
            (BreakdownStatus.ASSIGNED or BreakdownStatus.REJECTED, BreakdownStatus.IN_PROGRESS) => true,
            (BreakdownStatus.IN_PROGRESS, BreakdownStatus.AWAITING_APPROVAL) => true,
            (BreakdownStatus.AWAITING_APPROVAL, BreakdownStatus.CLOSED or BreakdownStatus.REJECTED) => true,
            _ => false
        };
        if (!allowed) throw new AppException(409, $"Cannot transition from {breakdown.Status} to {target}.");
    }

    public static void RequireEditable(Breakdown breakdown)
    {
        if (breakdown.Status != BreakdownStatus.IN_PROGRESS)
            throw new AppException(409, "Corrective work can only be edited while the breakdown is IN_PROGRESS.");
    }

    public static void RequireCompleteFacts(CorrectiveActionDraft draft)
    {
        Guard.Required(draft.RootCause, "RootCause", 10000);
        Guard.Required(draft.CorrectiveAction, "CorrectiveAction", 10000);
    }

    public static void TouchDraft(Breakdown breakdown, CorrectiveActionDraft draft, DateTime now)
    {
        if (breakdown.CompletedAt.HasValue) { breakdown.CompletedAt = null; draft.AttemptStartedAt = now; }
        draft.UpdatedAt = now;
        breakdown.Version = Guid.NewGuid();
    }

    public static decimal ElapsedMinutes(DateTime start, DateTime end) =>
        Math.Max(0, (decimal)(end - start).Ticks / TimeSpan.TicksPerMinute);
}
