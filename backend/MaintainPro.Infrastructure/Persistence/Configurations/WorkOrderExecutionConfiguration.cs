using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderExecutionConfiguration : IEntityTypeConfiguration<WorkOrderExecution>
{
    public void Configure(EntityTypeBuilder<WorkOrderExecution> b)
    {
        b.ToTable("WorkOrderExecutions");
        b.HasKey(x => x.Id);
        b.Property(x => x.TechnicianEmployeeId).IsRequired();
        b.Property(x => x.TechnicianName).IsRequired();
        b.Property(x => x.AttemptStartedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.AccumulatedDurationMinutes).HasColumnType("numeric");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.WorkOrderId).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_WorkOrderExecutions_Duration", "\"AccumulatedDurationMinutes\" >= 0"));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
