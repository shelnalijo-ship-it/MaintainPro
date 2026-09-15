using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => { t.HasCheckConstraint("CK_WorkOrders_Dates", "\"DueDate\" >= \"PlannedDate\""); t.HasCheckConstraint("CK_WorkOrders_Escalation", "\"EscalationLevel\" >= 0"); });
        builder.Property(x => x.WorkOrderNumber).IsRequired();
        builder.HasIndex(x => x.WorkOrderNumber).IsUnique();
        builder.HasIndex(x => new { x.MaintenancePlanId, x.PlannedDate }).IsUnique().HasFilter("\"MaintenancePlanId\" IS NOT NULL");
        builder.HasIndex(x => new { x.LifecycleStatus, x.DueDate });
        builder.HasIndex(x => new { x.SupervisorId, x.LifecycleStatus, x.SubmittedAt });
        builder.HasIndex(x => x.SupervisorId);
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkOrders_ExecutionCounters", "\"SubmissionVersion\" >= 0 AND \"HistoryVersion\" >= 0"));
        builder.HasIndex(x => new { x.AssignedTechnicianId, x.PlannedDate });
        builder.Property(x => x.Priority).HasConversion<string>();
        builder.Property(x => x.LifecycleStatus).HasConversion<string>();
        builder.Property(x => x.PlannedDate).HasColumnType("date");
        builder.Property(x => x.DueDate).HasColumnType("date");
        builder.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SubmittedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ApprovedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CancelledAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaintenancePlan).WithMany().HasForeignKey(x => x.MaintenancePlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedTechnician).WithMany().HasForeignKey(x => x.AssignedTechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
    }
}
