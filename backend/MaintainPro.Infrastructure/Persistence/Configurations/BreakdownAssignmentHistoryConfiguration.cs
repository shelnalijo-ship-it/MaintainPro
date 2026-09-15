using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class BreakdownAssignmentHistoryConfiguration : IEntityTypeConfiguration<BreakdownAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<BreakdownAssignmentHistory> b)
    {
        b.ToTable("BreakdownAssignmentHistories");
        b.HasKey(x => x.Id);
        b.Property(x => x.TechnicianEmployeeId).IsRequired();
        b.Property(x => x.TechnicianName).IsRequired();
        b.Property(x => x.SupervisorEmployeeId).IsRequired();
        b.Property(x => x.SupervisorName).IsRequired();
        b.Property(x => x.AssignedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.BreakdownId, x.SequenceNumber }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_BreakdownAssignmentHistories_Sequence", "\"SequenceNumber\" > 0"));
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AssignedByUser).WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

