using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderHistoryEventConfiguration : IEntityTypeConfiguration<WorkOrderHistoryEvent>
{
    public void Configure(EntityTypeBuilder<WorkOrderHistoryEvent> b)
    {
        b.ToTable("WorkOrderHistoryEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired();
        b.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.WorkOrderId, x.SequenceNumber }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_WorkOrderHistoryEvents_Sequence", "\"SequenceNumber\" > 0"));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Submission).WithMany().HasForeignKey(x => x.WorkOrderSubmissionId).OnDelete(DeleteBehavior.Restrict);
    }
}
