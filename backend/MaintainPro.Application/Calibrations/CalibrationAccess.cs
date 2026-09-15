using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;

namespace MaintainPro.Application.Calibrations;

public static class CalibrationAccess
{
    public static IQueryable<Machine> VisibleMachines(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (CanManage(user)) return db.Machines;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.Machines.Where(x => (technician && x.MachineOwnerUserId == id) ||
            (supervisor && x.SupervisorUserId == id));
    }

    public static bool CanManage(ICurrentUser user) =>
        user.Roles.Contains(RoleNames.Manager) || user.Roles.Contains(RoleNames.Admin);

    public static void RequireManager(ICurrentUser user)
    {
        Guard.Authenticated(user);
        if (!CanManage(user)) throw new AppException(403, "Calibration administration requires MANAGER or ADMIN access.");
    }
}
