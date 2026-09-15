using MaintainPro.Application.WorkOrders;
using Microsoft.Extensions.Options;

namespace MaintainPro.Api.Scheduling;

public sealed class MaintenanceGenerationOptions
{
    public const string SectionName = "MaintenanceGeneration";
    // Development runs by default; other deployments explicitly opt in.
    public bool? Enabled { get; set; }
}

public sealed class MaintenanceGenerationService(IServiceScopeFactory scopes,
    IHostEnvironment environment, IOptions<MaintenanceGenerationOptions> options,
    TimeProvider clock, ILogger<MaintenanceGenerationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing") || !(options.Value.Enabled ?? environment.IsDevelopment())) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(15);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var generator = scope.ServiceProvider.GetRequiredService<WorkOrderGenerationService>();
                var summary = await generator.GenerateDueAsync(ct: stoppingToken);
                logger.LogInformation("Maintenance generation evaluated {Plans} plans, created {Created} orders, skipped {Skipped}, errors {Errors}",
                    summary.PlansEvaluated, summary.WorkOrdersCreated, summary.Skipped, summary.Errors);
                delay = NextDelay(clock.GetUtcNow(), summary);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                // Do not log provider exceptions or configuration values.
                logger.LogWarning("Maintenance generation failed ({ExceptionType}); retrying in 15 minutes", exception.GetType().Name);
            }
            try { await Task.Delay(delay, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    public static TimeSpan NextDelay(DateTimeOffset now, GenerationSummary summary)
    {
        if (summary.Errors > 0) return TimeSpan.FromMinutes(15);
        if (summary.HasMore && summary.WorkOrdersCreated > 0) return TimeSpan.FromMinutes(1);
        // Blocked/inactive plans cannot cause a busy loop through HasMore.
        return new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero) - now;
    }
}
