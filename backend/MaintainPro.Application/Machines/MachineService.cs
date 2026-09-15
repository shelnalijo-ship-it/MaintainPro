using System.Linq.Expressions;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Machines;

public sealed class MachineService(
    IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit)
{
    private static readonly Expression<Func<Machine, MachineDto>> Projection = machine => new(
        machine.Id, machine.MachineCode, machine.AssetNumber, machine.Name, machine.CategoryId,
        machine.Manufacturer, machine.Model, machine.SerialNumber, machine.DepartmentId,
        machine.LocationId, machine.InstallationDate, machine.CommissioningDate,
        machine.WarrantyExpiryDate, machine.Status, machine.Criticality,
        machine.CalibrationRequired, machine.PreventiveMaintenanceRequired,
        machine.MachineOwnerUserId, machine.SupervisorUserId, machine.Notes,
        machine.IsActive, machine.CreatedAt, machine.UpdatedAt);

    public async Task<PagedResult<MachineDto>> ListAsync(MachineQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        if (request.Status.HasValue) ValidateStatus(request.Status.Value);
        if (request.Criticality.HasValue) ValidateCriticality(request.Criticality.Value);

        // Start from the user's permitted records so totals and pages reveal no other machines.
        var query = VisibleMachines().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search", 200).ToUpperInvariant();
            query = query.Where(machine =>
                machine.MachineCode.ToUpper().Contains(search) ||
                machine.Name.ToUpper().Contains(search) ||
                (machine.AssetNumber != null && machine.AssetNumber.ToUpper().Contains(search)) ||
                (machine.Manufacturer != null && machine.Manufacturer.ToUpper().Contains(search)) ||
                (machine.Model != null && machine.Model.ToUpper().Contains(search)) ||
                (machine.SerialNumber != null && machine.SerialNumber.ToUpper().Contains(search)));
        }
        if (request.CategoryId.HasValue) query = query.Where(x => x.CategoryId == request.CategoryId);
        if (request.DepartmentId.HasValue) query = query.Where(x => x.DepartmentId == request.DepartmentId);
        if (request.LocationId.HasValue) query = query.Where(x => x.LocationId == request.LocationId);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status);
        if (request.Criticality.HasValue) query = query.Where(x => x.Criticality == request.Criticality);
        if (request.CalibrationRequired.HasValue) query = query.Where(x => x.CalibrationRequired == request.CalibrationRequired);
        if (request.OwnerUserId.HasValue) query = query.Where(x => x.MachineOwnerUserId == request.OwnerUserId);
        if (request.SupervisorUserId.HasValue) query = query.Where(x => x.SupervisorUserId == request.SupervisorUserId);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.MachineCode).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(Projection).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<MachineDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await VisibleMachines().AsNoTracking().Where(x => x.Id == id).Select(Projection)
            .SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Machine not found.");

    public async Task<IReadOnlyList<MachineAssignmentHistoryDto>> GetAssignmentHistoryAsync(
        Guid id, CancellationToken ct = default)
    {
        var visibleMachines = VisibleMachines().AsNoTracking();
        if (!await visibleMachines.AnyAsync(x => x.Id == id, ct))
            throw new AppException(404, "Machine not found.");

        // Recheck scope in the history SELECT so reassignment between these statements
        // cannot expose the newly assigned machine's history to its former owner/supervisor.
        return await db.MachineAssignmentHistories.AsNoTracking()
            .Where(x => x.MachineId == id && visibleMachines.Any(machine => machine.Id == x.MachineId))
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id)
            .Select(x => new MachineAssignmentHistoryDto(x.Id, x.MachineId, x.TechnicianId,
                x.SupervisorId, x.EffectiveFrom, x.EffectiveTo, x.AssignedByUserId, x.Reason))
            .ToListAsync(ct);
    }

    public async Task<MachineDto> CreateAsync(MachineWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        await using var transaction = await db.BeginTransactionAsync(ct);
        var normalized = await ValidateWriteAsync(request, null, ct);
        var machine = new Machine { MachineCode = normalized.MachineCode, Name = normalized.Name };
        ApplyFields(machine, normalized);
        db.Machines.Add(machine);
        await ApplyAssignmentsAsync(machine, normalized.MachineOwnerUserId, normalized.SupervisorUserId,
            normalized.AssignmentReason, ct);
        audit.Record("Machine.Created", nameof(Machine), machine.Id, newValues: ToDto(machine));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(machine);
    }

    public async Task<MachineDto> UpdateAsync(Guid id, MachineWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await FindAsync(id, ct);
        var normalized = await ValidateWriteAsync(request, machine, ct);
        var previous = ToDto(machine);
        ApplyFields(machine, normalized);
        await ApplyAssignmentsAsync(machine, normalized.MachineOwnerUserId, normalized.SupervisorUserId,
            normalized.AssignmentReason, ct);
        machine.UpdatedAt = DateTime.UtcNow;
        audit.Record("Machine.Updated", nameof(Machine), id, previous, ToDto(machine));
        AuditStatusChange(previous, machine);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(machine);
    }

    public async Task<MachineDto> SetStatusAsync(Guid id, MachineStatusRequest request, CancellationToken ct = default)
    {
        RequireManager();
        ValidateStatus(request.Status);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await FindAsync(id, ct);
        var previous = ToDto(machine);
        machine.Status = request.Status;
        machine.IsActive = request.Status == MachineStatus.Decommissioned ? false : request.IsActive ?? machine.IsActive;
        if (previous.Status != machine.Status || previous.IsActive != machine.IsActive)
        {
            machine.UpdatedAt = DateTime.UtcNow;
            AuditStatusChange(previous, machine);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(machine);
    }

    public async Task<MachineDto> AssignOwnerAsync(Guid id, MachineAssignmentRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var reason = Optional(request.Reason, "Reason", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await FindAsync(id, ct);
        await ValidateAssignmentsAsync(request.MachineOwnerUserId, request.SupervisorUserId, machine, ct);
        if (await ApplyAssignmentsAsync(machine, request.MachineOwnerUserId, request.SupervisorUserId, reason, ct))
        {
            machine.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(machine);
    }

    private IQueryable<Machine> VisibleMachines()
    {
        if (currentUser.UserId is not Guid id) throw new AppException(401, "Authentication is required.");
        if (CanManage()) return db.Machines;
        var technician = currentUser.Roles.Contains(RoleNames.Technician);
        var supervisor = currentUser.Roles.Contains(RoleNames.Supervisor);
        return db.Machines.Where(machine =>
            (technician && machine.MachineOwnerUserId == id) ||
            (supervisor && machine.SupervisorUserId == id));
    }

    private bool CanManage() => currentUser.Roles.Contains(RoleNames.Manager) || currentUser.Roles.Contains(RoleNames.Admin);

    private void RequireManager()
    {
        if (currentUser.UserId is null) throw new AppException(401, "Authentication is required.");
        if (!CanManage()) throw new AppException(403, "Machine management requires MANAGER or ADMIN access.");
    }

    private async Task<Machine> FindAsync(Guid id, CancellationToken ct) =>
        await db.Machines.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "Machine not found.");

    private async Task<MachineWriteRequest> ValidateWriteAsync(MachineWriteRequest request, Machine? existing, CancellationToken ct)
    {
        var machineCode = Guard.Required(request.MachineCode, "MachineCode", 100).ToUpperInvariant();
        var name = Guard.Required(request.Name, "Name", 200);
        ValidateStatus(request.Status);
        ValidateCriticality(request.Criticality);
        if (request.InstallationDate.HasValue && request.CommissioningDate < request.InstallationDate)
            throw new AppException(400, "CommissioningDate cannot precede InstallationDate.");
        if (request.WarrantyExpiryDate.HasValue &&
            (request.WarrantyExpiryDate < request.InstallationDate || request.WarrantyExpiryDate < request.CommissioningDate))
            throw new AppException(400, "WarrantyExpiryDate cannot precede installation or commissioning.");

        if (await db.Machines.AnyAsync(x => x.MachineCode.ToUpper() == machineCode && (existing == null || x.Id != existing.Id), ct))
            throw new AppException(409, "MachineCode is already in use.");

        if (request.CategoryId is Guid categoryId && !await db.MachineCategories.AnyAsync(
            x => x.Id == categoryId && (x.IsActive || (existing != null && existing.CategoryId == categoryId)), ct))
            throw new AppException(400, "Category must reference an existing active machine category.");
        if (request.DepartmentId is Guid departmentId && !await db.Departments.AnyAsync(
            x => x.Id == departmentId && (x.IsActive || (existing != null && existing.DepartmentId == departmentId)), ct))
            throw new AppException(400, "Department must reference an existing active department.");
        if (request.LocationId is Guid locationId)
        {
            var location = await db.Locations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == locationId, ct);
            if (location is null || (!location.IsActive && existing?.LocationId != locationId))
                throw new AppException(400, "Location must reference an existing active location.");
            if (location.DepartmentId.HasValue && location.DepartmentId != request.DepartmentId)
                throw new AppException(400, "The selected location does not belong to the selected department.");
        }
        await ValidateAssignmentsAsync(request.MachineOwnerUserId, request.SupervisorUserId, existing, ct);
        return request with
        {
            MachineCode = machineCode,
            Name = name,
            AssetNumber = Optional(request.AssetNumber, "AssetNumber", 200),
            Manufacturer = Optional(request.Manufacturer, "Manufacturer", 200),
            Model = Optional(request.Model, "Model", 200),
            SerialNumber = Optional(request.SerialNumber, "SerialNumber", 200),
            Notes = Optional(request.Notes, "Notes", 10000),
            AssignmentReason = Optional(request.AssignmentReason, "AssignmentReason", 2000)
        };
    }

    private async Task ValidateAssignmentsAsync(Guid? ownerId, Guid? supervisorId, Machine? existing, CancellationToken ct)
    {
        // Existing assignments remain historical references if a user is later deactivated or changes role.
        if (ownerId.HasValue && ownerId != existing?.MachineOwnerUserId &&
            !await db.Users.AnyAsync(x => x.Id == ownerId && x.IsActive &&
                x.UserRoles.Any(role => role.Role.Name == RoleNames.Technician), ct))
            throw new AppException(400, "Machine owner must be an active user with the TECHNICIAN role.");
        if (supervisorId.HasValue && supervisorId != existing?.SupervisorUserId &&
            !await db.Users.AnyAsync(x => x.Id == supervisorId && x.IsActive &&
                x.UserRoles.Any(role => role.Role.Name == RoleNames.Supervisor), ct))
            throw new AppException(400, "Supervisor must be an active user with the SUPERVISOR role.");
    }

    private async Task<bool> ApplyAssignmentsAsync(Machine machine, Guid? ownerId, Guid? supervisorId,
        string? reason, CancellationToken ct)
    {
        var oldOwnerId = machine.MachineOwnerUserId;
        var oldSupervisorId = machine.SupervisorUserId;
        if (oldOwnerId == ownerId && oldSupervisorId == supervisorId) return false;
        var now = DateTime.UtcNow;
        var openHistory = await db.MachineAssignmentHistories
            .Where(x => x.MachineId == machine.Id && x.EffectiveTo == null).ToListAsync(ct);
        foreach (var history in openHistory) history.EffectiveTo = now;
        // Close the old row before inserting its replacement to respect the unique active-history index.
        // Both saves are protected by the enclosing transaction.
        if (openHistory.Count > 0) await db.SaveChangesAsync(ct);
        machine.MachineOwnerUserId = ownerId;
        machine.SupervisorUserId = supervisorId;
        if (ownerId.HasValue)
            db.MachineAssignmentHistories.Add(new MachineAssignmentHistory
            {
                MachineId = machine.Id,
                TechnicianId = ownerId.Value,
                SupervisorId = supervisorId,
                AssignedByUserId = currentUser.UserId!.Value,
                EffectiveFrom = now,
                Reason = reason
            });

        if (oldOwnerId != ownerId)
            audit.Record("Machine.OwnerChanged", nameof(Machine), machine.Id,
                new { MachineOwnerUserId = oldOwnerId }, new { MachineOwnerUserId = ownerId, Reason = reason });
        if (oldSupervisorId != supervisorId)
            audit.Record("Machine.SupervisorChanged", nameof(Machine), machine.Id,
                new { SupervisorUserId = oldSupervisorId }, new { SupervisorUserId = supervisorId, Reason = reason });
        return true;
    }

    private void AuditStatusChange(MachineDto previous, Machine machine)
    {
        if (previous.Status != machine.Status || previous.IsActive != machine.IsActive)
            audit.Record("Machine.StatusChanged", nameof(Machine), machine.Id,
                new { previous.Status, previous.IsActive }, new { machine.Status, machine.IsActive });
    }

    private static void ApplyFields(Machine machine, MachineWriteRequest request)
    {
        machine.MachineCode = request.MachineCode;
        machine.AssetNumber = request.AssetNumber;
        machine.Name = request.Name;
        machine.CategoryId = request.CategoryId;
        machine.Manufacturer = request.Manufacturer;
        machine.Model = request.Model;
        machine.SerialNumber = request.SerialNumber;
        machine.DepartmentId = request.DepartmentId;
        machine.LocationId = request.LocationId;
        machine.InstallationDate = request.InstallationDate;
        machine.CommissioningDate = request.CommissioningDate;
        machine.WarrantyExpiryDate = request.WarrantyExpiryDate;
        machine.Status = request.Status;
        machine.Criticality = request.Criticality;
        machine.CalibrationRequired = request.CalibrationRequired;
        machine.PreventiveMaintenanceRequired = request.PreventiveMaintenanceRequired;
        machine.Notes = request.Notes;
        machine.IsActive = request.Status != MachineStatus.Decommissioned && request.IsActive;
    }

    private static MachineDto ToDto(Machine machine) => new(
        machine.Id, machine.MachineCode, machine.AssetNumber, machine.Name, machine.CategoryId,
        machine.Manufacturer, machine.Model, machine.SerialNumber, machine.DepartmentId,
        machine.LocationId, machine.InstallationDate, machine.CommissioningDate, machine.WarrantyExpiryDate,
        machine.Status, machine.Criticality, machine.CalibrationRequired, machine.PreventiveMaintenanceRequired,
        machine.MachineOwnerUserId, machine.SupervisorUserId, machine.Notes, machine.IsActive,
        machine.CreatedAt, machine.UpdatedAt);

    private static void ValidateStatus(MachineStatus value)
    {
        if (!Enum.IsDefined(value)) throw new AppException(400, "Status is not a valid machine status.");
    }

    private static void ValidateCriticality(MachineCriticality value)
    {
        if (!Enum.IsDefined(value)) throw new AppException(400, "Criticality is not a valid machine criticality.");
    }

    private static string? Optional(string? value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > max) throw new AppException(400, $"{field} cannot exceed {max} characters.");
        return value;
    }
}
