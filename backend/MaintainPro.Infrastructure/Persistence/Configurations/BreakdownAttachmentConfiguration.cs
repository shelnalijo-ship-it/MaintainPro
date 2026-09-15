using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class BreakdownAttachmentConfiguration : IEntityTypeConfiguration<BreakdownAttachment>
{
    public void Configure(EntityTypeBuilder<BreakdownAttachment> b)
    {
        b.ToTable("BreakdownAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.EvidenceType).HasConversion<string>();
        b.HasIndex(x => new { x.BreakdownId, x.FileId }).IsUnique();
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

