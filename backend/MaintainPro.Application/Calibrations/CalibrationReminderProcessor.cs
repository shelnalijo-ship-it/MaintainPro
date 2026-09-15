using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Notifications;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Calibrations;

public sealed class CalibrationReminderProcessor(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IGenerationConcurrency concurrency)
{
    private readonly CalibrationTimingService timing = new();

    public async Task<CalibrationReminderSummary> ProcessAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId.HasValue) Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        var ids = await db.Machines.AsNoTracking().Where(x => x.CalibrationRequired).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var created = 0; var duplicates = 0; var expired = 0; var errors = 0; var issues = new List<string>();
        foreach (var id in ids)
        {
            var handled = false;
            for (var attempt = 0; attempt < 5 && !handled; attempt++)
            {
                concurrency.ResetTracking();
                try
                {
                    await using var transaction = await db.BeginTransactionAsync(ct);
                    var machine = await db.Machines.SingleAsync(x => x.Id == id, ct);
                    var certs = await db.CalibrationCertificates.Where(x => x.MachineId == id).ToListAsync(ct);
                    var current = timing.Current(certs, Today);
                    var state = timing.Evaluate(machine, current, await db.CalibrationRenewals.AnyAsync(x =>
                        x.MachineId == id && x.Status == CalibrationRenewalStatus.IN_PROGRESS, ct), Today);
                    var notifier = new CalibrationNotificationService(db, audit, clock);
                    var result = NotificationDispatchResult.Empty;
                    if (current is not null)
                    {
                        var days = current.ExpiryDate.DayNumber - Today.DayNumber;
                        if (days <= 60) result = result.Add(await notifier.EnsureAsync(machine, NotificationType.CALIBRATION_60_DAY, current, ct: ct));
                        if (days <= 30) result = result.Add(await notifier.EnsureAsync(machine, NotificationType.CALIBRATION_30_DAY, current, ct: ct));
                        if (days <= 7) result = result.Add(await notifier.EnsureAsync(machine, NotificationType.CALIBRATION_7_DAY, current, ct: ct));
                        if (days <= 0) result = result.Add(await notifier.EnsureAsync(machine, NotificationType.CALIBRATION_EXPIRED, current, ct: ct));
                    }
                    else result = result.Add(await notifier.EnsureAsync(machine, NotificationType.CALIBRATION_EXPIRED, ct: ct));
                    result = result.Add(await notifier.ReplayAsync(machine, ct));
                    if (state.ValidityStatus == CalibrationValidityStatus.EXPIRED) expired++;
                    created += result.NotificationsCreated; duplicates += result.DuplicatesSkipped;
                    errors += result.Errors; issues.AddRange(result.Issues);
                    if (result.NotificationsCreated > 0)
                        audit.Record("Calibration.RemindersProcessed", nameof(Domain.Entities.Machine), id,
                            newValues: new { result.NotificationsCreated, state.ValidityStatus, state.DaysRemaining });
                    await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); handled = true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (attempt < 4 && concurrency.IsRetryable(ex))
                { await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct); }
                catch (Exception)
                { errors++; issues.Add($"Calibration reminder processing failed for machine {id}; retry later."); handled = true; }
                finally { concurrency.ResetTracking(); }
            }
        }
        return new(ids.Count, created, duplicates, expired, errors, issues.Distinct().ToArray());
    }
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
