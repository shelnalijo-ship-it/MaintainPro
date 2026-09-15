using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderAttachmentConfiguration : IEntityTypeConfiguration<WorkOrderAttachment>
{
    public void Configure(EntityTypeBuilder<WorkOrderAttachment> b)
    {
        b.ToTable("WorkOrderAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.EvidenceType).HasConversion<string>();
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.WorkOrderId, x.IsDeleted });
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.WorkOrderChecklistItem).WithMany().HasForeignKey(x => x.WorkOrderChecklistItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
