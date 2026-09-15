using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderApprovalConfiguration : IEntityTypeConfiguration<WorkOrderApproval>
{
    public void Configure(EntityTypeBuilder<WorkOrderApproval> b)
    {
        b.ToTable("WorkOrderApprovals");
        b.HasKey(x => x.Id);
        b.Property(x => x.SupervisorEmployeeId).IsRequired();
        b.Property(x => x.SupervisorName).IsRequired();
        b.Property(x => x.Decision).HasConversion<string>();
        b.Property(x => x.DecisionAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.WorkOrderSubmissionId).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_WorkOrderApprovals_RejectionReason", "\"Decision\" <> 'REJECTED' OR (\"Remarks\" IS NOT NULL AND length(trim(\"Remarks\")) > 0)"));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Submission).WithOne(x => x.Review).HasForeignKey<WorkOrderApproval>(x => x.WorkOrderSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
    }
}
