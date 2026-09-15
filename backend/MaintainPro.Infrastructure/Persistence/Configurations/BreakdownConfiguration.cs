using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class BreakdownConfiguration : IEntityTypeConfiguration<Breakdown>
{
    public void Configure(EntityTypeBuilder<Breakdown> b)
    {
        b.ToTable("Breakdowns");
        b.HasKey(x => x.Id);
        b.Property(x => x.BreakdownNumber).IsRequired();
        b.Property(x => x.MachineCode).IsRequired();
        b.Property(x => x.MachineName).IsRequired();
        b.Property(x => x.ReporterEmployeeId).IsRequired();
        b.Property(x => x.ReporterName).IsRequired();
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.SupervisorEmployeeId).IsRequired();
        b.Property(x => x.SupervisorName).IsRequired();
        b.Property(x => x.ReportedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.SubmittedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ClosedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ReturnedToServiceAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Severity).HasConversion<string>();
        b.Property(x => x.Status).HasConversion<string>();
        b.Property(x => x.PreviousMachineStatus).HasConversion<string>();
        b.Property(x => x.RestoreMachineStatus).HasConversion<string>();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.BreakdownNumber).IsUnique();
        b.HasIndex(x => new { x.MachineId, x.MachineStopped, x.ReturnedToServiceAt });
        b.HasIndex(x => new { x.Status, x.ReportedAt });
        b.HasIndex(x => x.ReportedAt);
        b.HasIndex(x => new { x.AssignedTechnicianId, x.Status });
        b.HasIndex(x => new { x.SupervisorId, x.Status });
        b.ToTable(t => t.HasCheckConstraint("CK_Breakdowns_Versions", "\"AssignmentVersion\" >= 0 AND \"SubmissionVersion\" >= 0 AND \"HistoryVersion\" >= 0"));
        b.ToTable(t => t.HasCheckConstraint("CK_Breakdowns_ReturnedToService", "\"ReturnedToServiceAt\" IS NULL OR (\"MachineStopped\" AND \"ClosedAt\" IS NOT NULL AND \"Status\" = 'CLOSED' AND \"ReturnedToServiceAt\" >= \"ReportedAt\")"));
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReportedByUser).WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AssignedTechnician).WithMany().HasForeignKey(x => x.AssignedTechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
    }
}
