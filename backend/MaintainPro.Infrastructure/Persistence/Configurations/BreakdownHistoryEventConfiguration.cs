using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class BreakdownHistoryEventConfiguration : IEntityTypeConfiguration<BreakdownHistoryEvent>
{
    public void Configure(EntityTypeBuilder<BreakdownHistoryEvent> b)
    {
        b.ToTable("BreakdownHistoryEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired();
        b.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.BreakdownId, x.SequenceNumber }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_BreakdownHistoryEvents_Sequence", "\"SequenceNumber\" > 0"));
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Submission).WithMany().HasForeignKey(x => x.CorrectiveSubmissionId).OnDelete(DeleteBehavior.Restrict);
    }
}

