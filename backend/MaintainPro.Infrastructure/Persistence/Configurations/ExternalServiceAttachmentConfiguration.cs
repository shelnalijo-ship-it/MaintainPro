using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class ExternalServiceAttachmentConfiguration : IEntityTypeConfiguration<ExternalServiceAttachment>
{
    public void Configure(EntityTypeBuilder<ExternalServiceAttachment> b)
    {
        b.ToTable("ExternalServiceAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasConversion<string>();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.ExternalServiceId, x.FileId }).IsUnique();
        b.HasIndex(x => new { x.ExternalServiceId, x.IsActive });
        b.HasOne(x => x.ExternalService).WithMany(x => x.Attachments).HasForeignKey(x => x.ExternalServiceId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
