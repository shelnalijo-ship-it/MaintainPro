using MaintainPro.Application.Notifications;
using Microsoft.Extensions.Options;

namespace MaintainPro.Api.Scheduling;

public sealed class NotificationProcessingOptions
{
    public const string SectionName = "NotificationProcessing";
    public bool? Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 60;
}

public sealed class NotificationProcessingService(IServiceScopeFactory scopes,
    IHostEnvironment environment, IOptions<NotificationProcessingOptions> options,
    TimeProvider clock, ILogger<NotificationProcessingService> logger) : BackgroundService
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
                var processor = scope.ServiceProvider.GetRequiredService<ReminderProcessingService>();
                var result = await processor.ProcessDueAsync(stoppingToken);
                logger.LogInformation("Reminders evaluated {Orders} orders, created {Notifications} notifications and {Escalations} escalations; {Errors} errors",
                    result.WorkOrdersEvaluated, result.NotificationsCreated, result.EscalationsCreated, result.Errors);
                delay = NextDelay(result, options.Value.IntervalMinutes);
                var breakdownProcessor = scope.ServiceProvider.GetRequiredService<BreakdownNotificationProcessor>();
                var breakdownResult = await breakdownProcessor.ProcessPendingAsync(stoppingToken);
                logger.LogInformation("Breakdown notifications evaluated {Breakdowns} breakdowns and created {Notifications} notifications; {Errors} errors",
                    breakdownResult.BreakdownsEvaluated, breakdownResult.NotificationsCreated, breakdownResult.Errors);
                if (breakdownResult.Errors > 0 && delay > TimeSpan.FromMinutes(15)) delay = TimeSpan.FromMinutes(15);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogWarning("Reminder processing failed ({ExceptionType}); retrying in 15 minutes", exception.GetType().Name);
            }
            try { await Task.Delay(delay, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    public static TimeSpan NextDelay(ReminderProcessingSummary result, int intervalMinutes = 60) =>
        result.Errors > 0 ? TimeSpan.FromMinutes(15)
        : result.HasMoreGeneration && result.GeneratedWorkOrders > 0 ? TimeSpan.FromMinutes(1)
        : TimeSpan.FromMinutes(intervalMinutes);
}
