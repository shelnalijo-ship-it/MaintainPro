using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.ExternalServices;

public static class ExternalServiceAccess
{
    public static bool CanManageAll(ICurrentUser user) =>
        user.Roles.Contains(RoleNames.Manager) || user.Roles.Contains(RoleNames.Admin);

    public static IQueryable<Machine> VisibleMachines(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (CanManageAll(user)) return db.Machines;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.Machines.Where(x => (technician && x.MachineOwnerUserId == id) ||
            (supervisor && x.SupervisorUserId == id));
    }

    public static IQueryable<ExternalService> VisibleServices(IApplicationDbContext db, ICurrentUser user)
    {
        var machines = VisibleMachines(db, user);
        return db.ExternalServices.Where(x => machines.Any(machine => machine.Id == x.MachineId));
    }

    public static async Task<ExternalService> LoadVisibleServiceAsync(IApplicationDbContext db,
        ICurrentUser user, Guid id, CancellationToken ct = default) =>
        await VisibleServices(db, user).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new AppException(404, "External service not found.");

    public static void RequireServiceEditor(ICurrentUser user, ExternalService service)
    {
        Guard.Authenticated(user);
        if (CanManageAll(user)) return;
        if (!user.Roles.Contains(RoleNames.Supervisor) || service.Machine.SupervisorUserId != user.UserId)
            throw new AppException(403, "External-service corrections require the supervised machine or manager/admin access.");
    }

    public static void RequireAttachmentEditor(ICurrentUser user, ExternalService service)
    {
        Guard.Authenticated(user);
        if (CanManageAll(user)) return;
        var allowed = (user.Roles.Contains(RoleNames.Supervisor) && service.Machine.SupervisorUserId == user.UserId) ||
            (user.Roles.Contains(RoleNames.Technician) && service.Machine.MachineOwnerUserId == user.UserId);
        if (!allowed) throw new AppException(403, "Attachments require access to the assigned machine.");
    }

    public static void RequireDocumentManager(ICurrentUser user, Machine machine)
    {
        Guard.Authenticated(user);
        if (CanManageAll(user)) return;
        if (!user.Roles.Contains(RoleNames.Supervisor) || machine.SupervisorUserId != user.UserId)
            throw new AppException(403, "Machine documents require the supervised machine or manager/admin access.");
    }
}
