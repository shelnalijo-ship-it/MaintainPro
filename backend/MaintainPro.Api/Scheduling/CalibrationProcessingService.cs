using MaintainPro.Application.Calibrations;
using Microsoft.Extensions.Options;

namespace MaintainPro.Api.Scheduling;

public sealed class CalibrationProcessingOptions
{
    public const string SectionName = "CalibrationProcessing";
    public bool? Enabled { get; set; }
    public int IntervalHours { get; set; } = 24;
}

public sealed class CalibrationProcessingService(IServiceScopeFactory scopes, IHostEnvironment environment,
    IOptions<CalibrationProcessingOptions> options, TimeProvider clock,
    ILogger<CalibrationProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing") || !(options.Value.Enabled ?? environment.IsDevelopment())) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromHours(options.Value.IntervalHours);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<CalibrationReminderProcessor>().ProcessAsync(stoppingToken);
                logger.LogInformation("Calibration reminders evaluated {Machines} machines, created {Notifications} notifications; {Errors} errors",
                    result.MachinesEvaluated, result.NotificationsCreated, result.Errors);
                if (result.Errors > 0) delay = TimeSpan.FromMinutes(15);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            { logger.LogWarning("Calibration reminder processing failed ({ExceptionType}); retrying in 15 minutes", exception.GetType().Name); delay = TimeSpan.FromMinutes(15); }
            try { await Task.Delay(delay, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
