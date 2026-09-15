using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> b)
    {
        b.ToTable("FileRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.StorageKey).IsRequired();
        b.Property(x => x.OriginalFilename).IsRequired();
        b.Property(x => x.MimeType).IsRequired();
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_FileRecords_Size", "\"FileSize\" > 0"));
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
