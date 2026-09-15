using MaintainPro.Domain.Entities;

namespace MaintainPro.Application.Breakdowns;

public static class BreakdownTiming
{
    // Physical machine downtime includes review waiting while the machine is still stopped.
    // Submission snapshots call this with the submission time; closed read models continue
    // accruing until the separate authorized return-to-service action.
    public static decimal DowntimeMinutes(Breakdown breakdown, DateTime now)
    {
        if (!breakdown.MachineStopped) return 0;
        var end = breakdown.ReturnedToServiceAt ?? now;
        return Math.Max(0, decimal.Round((decimal)(end - breakdown.ReportedAt).TotalMinutes, 2));
    }
}
