using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public static class BreakdownAccess
{
    public static bool CanManage(ICurrentUser user) =>
        user.Roles.Contains(RoleNames.Manager) || user.Roles.Contains(RoleNames.Admin);

    public static IQueryable<Breakdown> Visible(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (CanManage(user)) return db.Breakdowns;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.Breakdowns.Where(x => (technician && (x.AssignedTechnicianId == id || x.ReportedByUserId == id))
            || (supervisor && x.SupervisorId == id));
    }

    public static Task<Breakdown> LoadAsync(IApplicationDbContext db, ICurrentUser user, Guid id,
        CancellationToken ct = default) => LoadVisibleAsync(db, user, id, ct);

    private static async Task<Breakdown> LoadVisibleAsync(IApplicationDbContext db, ICurrentUser user,
        Guid id, CancellationToken ct) => await Visible(db, user).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new AppException(404, "Breakdown not found.");

    public static void RequireTechnician(ICurrentUser user, Breakdown breakdown)
    {
        Guard.RequireRole(user, RoleNames.Technician);
        if (breakdown.AssignedTechnicianId != user.UserId)
            throw new AppException(403, "Only the assigned technician can execute corrective work.");
    }

    public static void RequireSupervisor(ICurrentUser user, Breakdown breakdown)
    {
        Guard.RequireRole(user, RoleNames.Supervisor);
        if (breakdown.SupervisorId != user.UserId)
            throw new AppException(403, "Only the assigned supervisor can review corrective work.");
    }

    public static void RequireAssignmentManager(ICurrentUser user, Breakdown breakdown)
    {
        Guard.Authenticated(user);
        if (!CanManage(user) && !(user.Roles.Contains(RoleNames.Supervisor) && breakdown.SupervisorId == user.UserId))
            throw new AppException(403, "Assignment and return to service require the assigned supervisor or manager/admin.");
    }
}
