using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CorrectiveApprovalConfiguration : IEntityTypeConfiguration<CorrectiveApproval>
{
    public void Configure(EntityTypeBuilder<CorrectiveApproval> b)
    {
        b.ToTable("CorrectiveApprovals");
        b.HasKey(x => x.Id);
        b.Property(x => x.SupervisorEmployeeId).IsRequired();
        b.Property(x => x.SupervisorName).IsRequired();
        b.Property(x => x.DecisionAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Decision).HasConversion<string>();
        b.ToTable(t => t.HasCheckConstraint("CK_CorrectiveApprovals_RejectionRemarks", "\"Decision\" <> 'REJECTED' OR (\"Remarks\" IS NOT NULL AND length(trim(\"Remarks\")) > 0)"));
        b.HasOne(x => x.Submission).WithOne(x => x.Review).HasForeignKey<CorrectiveApproval>(x => x.CorrectiveSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
    }
}

