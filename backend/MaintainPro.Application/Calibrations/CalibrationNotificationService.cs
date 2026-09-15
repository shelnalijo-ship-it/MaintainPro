using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Calibrations;

public sealed class CalibrationNotificationService(IApplicationDbContext db, IAuditWriter audit, TimeProvider clock)
{
    public async Task<NotificationDispatchResult> EnsureAsync(Machine machine, NotificationType type,
        CalibrationCertificate? certificate = null, CalibrationRenewal? renewal = null, CancellationToken ct = default)
    {
        if (!db.HasActiveTransaction) throw new InvalidOperationException("Calibration notifications require a business transaction.");
        if (type is not (NotificationType.CALIBRATION_60_DAY or NotificationType.CALIBRATION_30_DAY or
            NotificationType.CALIBRATION_7_DAY or NotificationType.CALIBRATION_EXPIRED or
            NotificationType.CALIBRATION_RENEWAL_STARTED or NotificationType.CALIBRATION_RENEWED))
            throw new AppException(400, "Notification type is not a calibration event.");
        var source = certificate?.Id ?? renewal?.Id ?? machine.Id;
        var key = $"calibration:{machine.Id:N}:{source:N}:{type}";
        var evt = db.CalibrationNotificationEvents.Local.SingleOrDefault(x => x.DeduplicationKey == key)
            ?? await db.CalibrationNotificationEvents.SingleOrDefaultAsync(x => x.DeduplicationKey == key, ct);
        if (evt is null)
        {
            var text = Describe(machine, type);
            evt = new CalibrationNotificationEvent { MachineId = machine.Id,
                CalibrationCertificateId = certificate?.Id, CalibrationRenewalId = renewal?.Id,
                NotificationType = type, Priority = type == NotificationType.CALIBRATION_EXPIRED
                    ? NotificationPriority.CRITICAL : NotificationPriority.HIGH,
                Title = text.Title, Message = text.Message, DeduplicationKey = key,
                CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.CalibrationNotificationEvents.Add(evt);
        }
        return await DispatchAsync(machine, evt, ct);
    }

    public async Task<NotificationDispatchResult> ReplayAsync(Machine machine, CancellationToken ct)
    {
        var events = await db.CalibrationNotificationEvents.Where(x => x.MachineId == machine.Id && x.ProcessedAt == null)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var result = NotificationDispatchResult.Empty;
        foreach (var evt in events) result = result.Add(await DispatchAsync(machine, evt, ct));
        return result;
    }

    private async Task<NotificationDispatchResult> DispatchAsync(Machine machine, CalibrationNotificationEvent evt, CancellationToken ct)
    {
        if (evt.ProcessedAt.HasValue) return new(0, 0, 1, 0, Array.Empty<string>());
        var users = new HashSet<Guid>();
        var supervisor = machine.SupervisorUserId.HasValue && await db.Users.AnyAsync(x => x.Id == machine.SupervisorUserId && x.IsActive &&
            x.UserRoles.Any(r => r.Role.Name == RoleNames.Supervisor), ct);
        if (supervisor) users.Add(machine.SupervisorUserId!.Value);
        var managers = await db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.Manager))
            .Select(x => x.Id).ToListAsync(ct);
        users.UnionWith(managers);
        var created = 0; var duplicates = 0;
        var writer = new NotificationWriter(db, clock);
        foreach (var userId in users)
        {
            var result = await writer.EmitAsync(new(userId, evt.NotificationType, evt.Title, evt.Message,
                evt.Priority, $"{evt.DeduplicationKey}:user:{userId:N}", nameof(Machine), machine.Id), ct);
            if (result.Created) created++; else duplicates++;
        }
        if (managers.Count > 0)
        {
            evt.ProcessedAt = clock.GetUtcNow().UtcDateTime; evt.LastError = null;
            audit.Record("CalibrationNotification.Dispatched", nameof(Machine), machine.Id,
                newValues: new { evt.NotificationType, created, duplicates });
            return new(created, 0, duplicates, 0, Array.Empty<string>());
        }
        var reason = managers.Count == 0 ? "No active MANAGER recipient is available." : "The assigned supervisor is inactive or missing; managers were notified.";
        evt.LastError = reason;
        audit.Record("CalibrationNotification.RoutingIncomplete", nameof(Machine), machine.Id,
            newValues: new { evt.NotificationType, Reason = reason });
        return new(created, 0, duplicates, 1, new[] { reason });
    }

    private static (string Title, string Message) Describe(Machine machine, NotificationType type) => type switch
    {
        NotificationType.CALIBRATION_60_DAY => ("Calibration expires within 60 days", $"Calibration for {machine.MachineCode} expires within 60 days."),
        NotificationType.CALIBRATION_30_DAY => ("Calibration expires within 30 days", $"Calibration for {machine.MachineCode} expires within 30 days."),
        NotificationType.CALIBRATION_7_DAY => ("Calibration expires within 7 days", $"Calibration for {machine.MachineCode} expires within 7 days."),
        NotificationType.CALIBRATION_EXPIRED => ("Calibration expired", $"Calibration for {machine.MachineCode} is expired."),
        NotificationType.CALIBRATION_RENEWAL_STARTED => ("Calibration renewal started", $"Calibration renewal for {machine.MachineCode} has started."),
        NotificationType.CALIBRATION_RENEWED => ("Calibration renewed", $"Calibration for {machine.MachineCode} has been renewed."),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
