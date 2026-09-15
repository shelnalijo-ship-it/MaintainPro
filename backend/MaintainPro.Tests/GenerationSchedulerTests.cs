using MaintainPro.Api.Scheduling;
using MaintainPro.Application.WorkOrders;

namespace MaintainPro.Tests;

public sealed class GenerationSchedulerTests
{
    [Fact]
    public void Scheduler_retries_errors_and_catch_up_but_does_not_busy_loop_blocked_plans()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMinutes(15), MaintenanceGenerationService.NextDelay(now,
            new GenerationSummary(1, 0, 0, 1, true, Array.Empty<GenerationIssue>())));
        Assert.Equal(TimeSpan.FromMinutes(1), MaintenanceGenerationService.NextDelay(now,
            new GenerationSummary(1, 500, 0, 0, true, Array.Empty<GenerationIssue>())));
        Assert.Equal(TimeSpan.FromHours(12), MaintenanceGenerationService.NextDelay(now,
            new GenerationSummary(1, 0, 1, 0, true, Array.Empty<GenerationIssue>())));
        Assert.Equal(TimeSpan.FromHours(12), MaintenanceGenerationService.NextDelay(now,
            new GenerationSummary(1, 1, 0, 0, false, Array.Empty<GenerationIssue>())));
    }
}
