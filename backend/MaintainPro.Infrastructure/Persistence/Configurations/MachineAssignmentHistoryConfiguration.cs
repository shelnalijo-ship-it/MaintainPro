using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class MachineAssignmentHistoryConfiguration : IEntityTypeConfiguration<MachineAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<MachineAssignmentHistory> builder)
    {
        builder.ToTable("MachineAssignmentHistories");
        builder.HasKey(history => history.Id);
        builder.HasIndex(history => history.MachineId).IsUnique()
            .HasFilter("\"EffectiveTo\" IS NULL");
        builder.Property(history => history.EffectiveFrom).HasColumnType("timestamp with time zone");
        builder.Property(history => history.EffectiveTo).HasColumnType("timestamp with time zone");

        builder.HasOne(history => history.Machine)
            .WithMany()
            .HasForeignKey(history => history.MachineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(history => history.Technician)
            .WithMany()
            .HasForeignKey(history => history.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(history => history.Supervisor)
            .WithMany()
            .HasForeignKey(history => history.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(history => history.AssignedByUser)
            .WithMany()
            .HasForeignKey(history => history.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
