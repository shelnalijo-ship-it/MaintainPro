using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class MaintenancePlanConfiguration : IEntityTypeConfiguration<MaintenancePlan>
{
    public void Configure(EntityTypeBuilder<MaintenancePlan> builder)
    {
        builder.ToTable("MaintenancePlans");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => { t.HasCheckConstraint("CK_MaintenancePlans_Frequency", "\"FrequencyValue\" > 0"); t.HasCheckConstraint("CK_MaintenancePlans_Evidence", "(\"PhotoRequired\" AND \"MinimumPhotoCount\" >= 1) OR (NOT \"PhotoRequired\" AND \"MinimumPhotoCount\" = 0)"); t.HasCheckConstraint("CK_MaintenancePlans_Duration", "\"EstimatedDurationMinutes\" IS NULL OR \"EstimatedDurationMinutes\" >= 0"); });
        builder.Property(x => x.PlanName).IsRequired();
        builder.Property(x => x.Priority).HasConversion<string>();
        builder.Property(x => x.FrequencyType).HasConversion<string>();
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.NextDueDate).HasColumnType("date");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.IsActive, x.NextDueDate });
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaintenanceType).WithMany().HasForeignKey(x => x.MaintenanceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DefaultTechnician).WithMany().HasForeignKey(x => x.DefaultTechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
