using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.MasterData;

public sealed class MasterDataService(
    IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit)
{
    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(
        bool? isActive = null, CancellationToken ct = default)
    {
        RequireManager();
        return await db.Departments.AsNoTracking().Where(x => !isActive.HasValue || x.IsActive == isActive)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new DepartmentDto(x.Id, x.Name, x.Description, x.IsActive)).ToListAsync(ct);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(MasterDataWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.Name, "Name");
        var description = Description(request.Description);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await EnsureDepartmentNameAvailableAsync(name, null, ct);
        var entity = new Department { Name = name, Description = description, IsActive = request.IsActive };
        db.Departments.Add(entity);
        var result = ToDto(entity);
        audit.Record("Department.Created", nameof(Department), entity.Id, newValues: result);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, MasterDataWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.Name, "Name");
        var description = Description(request.Description);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Department not found.");
        await EnsureDepartmentNameAvailableAsync(name, id, ct);
        var previous = ToDto(entity);
        entity.Name = name;
        entity.Description = description;
        entity.IsActive = request.IsActive;
        var result = ToDto(entity);
        audit.Record("Department.Updated", nameof(Department), id, previous, result);
        if (previous.IsActive != entity.IsActive)
            audit.Record("Department.StatusChanged", nameof(Department), id,
                new { previous.IsActive }, new { entity.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<DepartmentDto> SetDepartmentStatusAsync(Guid id, MasterDataStatusRequest request, CancellationToken ct = default)
    {
        RequireManager();
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Department not found.");
        if (entity.IsActive != request.IsActive)
        {
            var previous = entity.IsActive;
            entity.IsActive = request.IsActive;
            audit.Record("Department.StatusChanged", nameof(Department), id,
                new { IsActive = previous }, new { entity.IsActive });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<MachineCategoryDto>> ListMachineCategoriesAsync(
        bool? isActive = null, CancellationToken ct = default)
    {
        RequireManager();
        return await db.MachineCategories.AsNoTracking().Where(x => !isActive.HasValue || x.IsActive == isActive)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new MachineCategoryDto(x.Id, x.Name, x.Description, x.IsActive)).ToListAsync(ct);
    }

    public async Task<MachineCategoryDto> CreateMachineCategoryAsync(MasterDataWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.Name, "Name");
        var description = Description(request.Description);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await EnsureCategoryNameAvailableAsync(name, null, ct);
        var entity = new MachineCategory { Name = name, Description = description, IsActive = request.IsActive };
        db.MachineCategories.Add(entity);
        var result = ToDto(entity);
        audit.Record("MachineCategory.Created", nameof(MachineCategory), entity.Id, newValues: result);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<MachineCategoryDto> UpdateMachineCategoryAsync(Guid id, MasterDataWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.Name, "Name");
        var description = Description(request.Description);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await db.MachineCategories.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Machine category not found.");
        await EnsureCategoryNameAvailableAsync(name, id, ct);
        var previous = ToDto(entity);
        entity.Name = name;
        entity.Description = description;
        entity.IsActive = request.IsActive;
        var result = ToDto(entity);
        audit.Record("MachineCategory.Updated", nameof(MachineCategory), id, previous, result);
        if (previous.IsActive != entity.IsActive)
            audit.Record("MachineCategory.StatusChanged", nameof(MachineCategory), id,
                new { previous.IsActive }, new { entity.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<MachineCategoryDto> SetMachineCategoryStatusAsync(Guid id, MasterDataStatusRequest request, CancellationToken ct = default)
    {
        RequireManager();
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await db.MachineCategories.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Machine category not found.");
        if (entity.IsActive != request.IsActive)
        {
            var previous = entity.IsActive;
            entity.IsActive = request.IsActive;
            audit.Record("MachineCategory.StatusChanged", nameof(MachineCategory), id,
                new { IsActive = previous }, new { entity.IsActive });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<LocationDto>> ListLocationsAsync(
        bool? isActive = null, Guid? departmentId = null, CancellationToken ct = default)
    {
        RequireManager();
        return await db.Locations.AsNoTracking()
            .Where(x => (!isActive.HasValue || x.IsActive == isActive) && (!departmentId.HasValue || x.DepartmentId == departmentId))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new LocationDto(x.Id, x.Name, x.Description, x.DepartmentId, x.IsActive)).ToListAsync(ct);
    }

    public async Task<LocationDto> CreateLocationAsync(LocationWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.Name, "Name");
        var description = Description(request.Description);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await EnsureLocationNameAvailableAsync(name, null, ct);
        await ValidateDepartmentAsync(request.DepartmentId, null, ct);
        var entity = new Location
        {
            Name = name,
            Description = description,
            DepartmentId = request.DepartmentId,
            IsActive = request.IsActive
        };
        db.Locations.Add(entity);
        var result = ToDto(entity);
        audit.Record("Location.Created", nameof(Location), entity.Id, newValues: result);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<LocationDto> UpdateLocationAsync(Guid id, LocationWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.Name, "Name");
        var description = Description(request.Description);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await db.Locations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Location not found.");
        await EnsureLocationNameAvailableAsync(name, id, ct);
        await ValidateDepartmentAsync(request.DepartmentId, entity.DepartmentId, ct);
        if (request.DepartmentId.HasValue && request.DepartmentId != entity.DepartmentId &&
            await db.Machines.AnyAsync(x => x.LocationId == id && x.DepartmentId != request.DepartmentId, ct))
            throw new AppException(409, "The location department cannot change while referenced machines belong to another department.");
        var previous = ToDto(entity);
        entity.Name = name;
        entity.Description = description;
        entity.DepartmentId = request.DepartmentId;
        entity.IsActive = request.IsActive;
        var result = ToDto(entity);
        audit.Record("Location.Updated", nameof(Location), id, previous, result);
        if (previous.IsActive != entity.IsActive)
            audit.Record("Location.StatusChanged", nameof(Location), id,
                new { previous.IsActive }, new { entity.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<LocationDto> SetLocationStatusAsync(Guid id, MasterDataStatusRequest request, CancellationToken ct = default)
    {
        RequireManager();
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await db.Locations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Location not found.");
        if (entity.IsActive != request.IsActive)
        {
            var previous = entity.IsActive;
            entity.IsActive = request.IsActive;
            audit.Record("Location.StatusChanged", nameof(Location), id,
                new { IsActive = previous }, new { entity.IsActive });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(entity);
    }

    private async Task EnsureDepartmentNameAvailableAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        if (await db.Departments.AnyAsync(x => x.Name.ToUpper() == normalized && x.Id != exceptId, ct))
            throw new AppException(409, "Department name is already in use.");
    }

    private async Task EnsureCategoryNameAvailableAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        if (await db.MachineCategories.AnyAsync(x => x.Name.ToUpper() == normalized && x.Id != exceptId, ct))
            throw new AppException(409, "Machine category name is already in use.");
    }

    private async Task EnsureLocationNameAvailableAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        if (await db.Locations.AnyAsync(x => x.Name.ToUpper() == normalized && x.Id != exceptId, ct))
            throw new AppException(409, "Location name is already in use.");
    }

    private async Task ValidateDepartmentAsync(Guid? departmentId, Guid? existingDepartmentId, CancellationToken ct)
    {
        if (departmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == departmentId &&
            (x.IsActive || departmentId == existingDepartmentId), ct))
            throw new AppException(400, "Department must reference an existing active department.");
    }

    private void RequireManager()
    {
        if (currentUser.UserId is null) throw new AppException(401, "Authentication is required.");
        if (!currentUser.Roles.Contains(RoleNames.Manager) && !currentUser.Roles.Contains(RoleNames.Admin))
            throw new AppException(403, "Master data management requires MANAGER or ADMIN access.");
    }

    private static string? Description(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 2000) throw new AppException(400, "Description cannot exceed 2000 characters.");
        return value;
    }

    private static DepartmentDto ToDto(Department entity) => new(entity.Id, entity.Name, entity.Description, entity.IsActive);
    private static MachineCategoryDto ToDto(MachineCategory entity) => new(entity.Id, entity.Name, entity.Description, entity.IsActive);
    private static LocationDto ToDto(Location entity) => new(entity.Id, entity.Name, entity.Description, entity.DepartmentId, entity.IsActive);
}
