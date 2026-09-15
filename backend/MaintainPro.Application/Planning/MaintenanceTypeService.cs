using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Planning;

public sealed class MaintenanceTypeService(
    IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<MaintenanceTypeDto>> ListAsync(MaintenanceTypeQuery request,
        CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        var query = db.MaintenanceTypes.AsNoTracking();
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search").ToUpperInvariant();
            query = query.Where(x => x.Name.ToUpper().Contains(search));
        }
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new MaintenanceTypeDto(x.Id, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<MaintenanceTypeDto> CreateAsync(MaintenanceTypeWriteRequest request,
        CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        var name = Guard.Required(request.Name, "Name");
        var description = PlanningValidation.Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await EnsureNameAvailableAsync(name, null, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var entity = new MaintenanceType
        {
            Name = name, Description = description, IsActive = request.IsActive,
            CreatedAt = now, UpdatedAt = now
        };
        db.MaintenanceTypes.Add(entity);
        audit.Record("maintenance_type.created", nameof(MaintenanceType), entity.Id, newValues: ToDto(entity));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<MaintenanceTypeDto> UpdateAsync(Guid id, MaintenanceTypeWriteRequest request,
        CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        var name = Guard.Required(request.Name, "Name");
        var description = PlanningValidation.Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await FindAsync(id, ct);
        await EnsureNameAvailableAsync(name, id, ct);
        var previous = ToDto(entity);
        entity.Name = name;
        entity.Description = description;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        audit.Record("maintenance_type.updated", nameof(MaintenanceType), id, previous, ToDto(entity));
        if (previous.IsActive != entity.IsActive)
            audit.Record("maintenance_type.status_changed", nameof(MaintenanceType), id,
                new { previous.IsActive }, new { entity.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(entity);
    }

    public async Task<MaintenanceTypeDto> SetStatusAsync(Guid id, MaintenanceTypeStatusRequest request,
        CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var entity = await FindAsync(id, ct);
        if (entity.IsActive != request.IsActive)
        {
            var previous = entity.IsActive;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            audit.Record("maintenance_type.status_changed", nameof(MaintenanceType), id,
                new { IsActive = previous }, new { entity.IsActive });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(entity);
    }

    private async Task<MaintenanceType> FindAsync(Guid id, CancellationToken ct) =>
        await db.MaintenanceTypes.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new AppException(404, "Maintenance type not found.");

    private async Task EnsureNameAvailableAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        if (await db.MaintenanceTypes.AnyAsync(x => x.Id != exceptId && x.Name.ToUpper() == normalized, ct))
            throw new AppException(409, "Maintenance type name is already in use.");
    }

    private static MaintenanceTypeDto ToDto(MaintenanceType entity) =>
        new(entity.Id, entity.Name, entity.Description, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
}
