using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderSubmissionChecklistResultConfiguration : IEntityTypeConfiguration<WorkOrderSubmissionChecklistResult>
{
    public void Configure(EntityTypeBuilder<WorkOrderSubmissionChecklistResult> b)
    {
        b.ToTable("WorkOrderSubmissionChecklistResults");
        b.HasKey(x => x.Id);
        b.Property(x => x.NumericValue).HasColumnType("numeric");
        b.Property(x => x.PassFailValue).HasConversion<string>();
        b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.WorkOrderSubmissionId, x.WorkOrderChecklistItemId }).IsUnique();
        b.HasOne(x => x.Submission).WithMany(x => x.ChecklistResults).HasForeignKey(x => x.WorkOrderSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.WorkOrderChecklistItem).WithMany().HasForeignKey(x => x.WorkOrderChecklistItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
