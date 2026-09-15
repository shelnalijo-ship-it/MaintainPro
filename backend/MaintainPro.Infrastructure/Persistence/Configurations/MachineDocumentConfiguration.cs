using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class MachineDocumentConfiguration : IEntityTypeConfiguration<MachineDocument>
{
    public void Configure(EntityTypeBuilder<MachineDocument> b)
    {
        b.ToTable("MachineDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasConversion<string>();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.DocumentDate).HasColumnType("date");
        b.Property(x => x.ExpiryDate).HasColumnType("date");
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.MachineId, x.FileId }).IsUnique();
        b.HasIndex(x => new { x.MachineId, x.DocumentType, x.IsActive });
        b.HasIndex(x => x.ExpiryDate);
        b.HasIndex(x => x.UploadedAt);
        b.ToTable(t => t.HasCheckConstraint("CK_MachineDocuments_Dates",
            "\"ExpiryDate\" IS NULL OR \"DocumentDate\" IS NULL OR \"ExpiryDate\" >= \"DocumentDate\""));
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
