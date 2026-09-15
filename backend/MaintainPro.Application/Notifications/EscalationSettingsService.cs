using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public sealed class EscalationSettingsService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<EscalationSettingsDto> GetAsync(CancellationToken ct = default)
    {
        RequireManager();
        var settings = await db.EscalationSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        return settings is null ? DefaultDto() : ToDto(settings);
    }

    public async Task<EscalationSettingsDto> UpdateAsync(EscalationSettingsRequest request, CancellationToken ct = default)
    {
        RequireManager();
        Validate(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var settings = await db.EscalationSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        var previous = settings is null ? DefaultDto() : ToDto(settings);
        if (settings is null) { settings = new EscalationSettings(); db.EscalationSettings.Add(settings); }
        settings.DueSoonDays = request.DueSoonDays;
        settings.TechnicianOverdueDays = request.TechnicianOverdueDays;
        settings.SupervisorEscalationDays = request.SupervisorEscalationDays;
        settings.ManagerEscalationDays = request.ManagerEscalationDays;
        settings.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        settings.UpdatedByUserId = currentUser.UserId;
        // Explicitly rotate even if a fixed clock and identical values would otherwise hide the write.
        settings.Version = Guid.NewGuid();
        audit.Record("EscalationSettings.Updated", nameof(EscalationSettings), null, previous, ToDto(settings));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(settings);
    }

    public static async Task<EscalationSettings> ReadEffectiveAsync(IApplicationDbContext db, CancellationToken ct = default) =>
        await db.EscalationSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct) ?? new EscalationSettings();

    public static void Validate(EscalationSettingsRequest request)
    {
        if (request.DueSoonDays is < 0 or > 365)
            throw new AppException(400, "DueSoonDays must be between 0 and 365.");
        if (request.TechnicianOverdueDays is < 0 or > 36500 || request.SupervisorEscalationDays is < 0 or > 36500 ||
            request.ManagerEscalationDays is < 0 or > 36500)
            throw new AppException(400, "Escalation thresholds must be between 0 and 36500 days.");
        if (request.TechnicianOverdueDays > request.SupervisorEscalationDays ||
            request.SupervisorEscalationDays > request.ManagerEscalationDays)
            throw new AppException(400, "Thresholds must satisfy technician <= supervisor <= manager.");
    }

    private void RequireManager() => Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
    private static EscalationSettingsDto DefaultDto()
    {
        var settings = new EscalationSettings();
        return new(settings.DueSoonDays, settings.TechnicianOverdueDays,
            settings.SupervisorEscalationDays, settings.ManagerEscalationDays, null, null);
    }
    private static EscalationSettingsDto ToDto(EscalationSettings settings) => new(settings.DueSoonDays,
        settings.TechnicianOverdueDays, settings.SupervisorEscalationDays, settings.ManagerEscalationDays,
        settings.UpdatedAt, settings.UpdatedByUserId);
}
