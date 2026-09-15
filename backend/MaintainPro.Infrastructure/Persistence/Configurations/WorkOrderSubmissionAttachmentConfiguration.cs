using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderSubmissionAttachmentConfiguration : IEntityTypeConfiguration<WorkOrderSubmissionAttachment>
{
    public void Configure(EntityTypeBuilder<WorkOrderSubmissionAttachment> b)
    {
        b.ToTable("WorkOrderSubmissionAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.EvidenceType).HasConversion<string>();
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.WorkOrderSubmissionId, x.FileId }).IsUnique();
        b.HasOne(x => x.Submission).WithMany(x => x.Attachments).HasForeignKey(x => x.WorkOrderSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.WorkOrderChecklistItem).WithMany().HasForeignKey(x => x.WorkOrderChecklistItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
