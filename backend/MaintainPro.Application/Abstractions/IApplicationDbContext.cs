using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Abstractions;

public interface IApplicationDbContext
{
    bool HasActiveTransaction { get; }
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Department> Departments { get; }
    DbSet<Location> Locations { get; }
    DbSet<MachineCategory> MachineCategories { get; }
    DbSet<Machine> Machines { get; }
    DbSet<MachineAssignmentHistory> MachineAssignmentHistories { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<MaintenanceType> MaintenanceTypes { get; }
    DbSet<MaintenancePlan> MaintenancePlans { get; }
    DbSet<ChecklistTemplate> ChecklistTemplates { get; }
    DbSet<ChecklistItem> ChecklistItems { get; }
    DbSet<WorkOrder> WorkOrders { get; }
    DbSet<WorkOrderDefinition> WorkOrderDefinitions { get; }
    DbSet<WorkOrderChecklistItem> WorkOrderChecklistItems { get; }
    DbSet<WorkOrderNumberSequence> WorkOrderNumberSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
