using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CalibrationCertificateConfiguration : IEntityTypeConfiguration<CalibrationCertificate>
{
    public void Configure(EntityTypeBuilder<CalibrationCertificate> b)
    {
        b.ToTable("CalibrationCertificates"); b.HasKey(x => x.Id);
        b.Property(x => x.CertificateNumber).HasMaxLength(200).IsRequired();
        b.Property(x => x.CalibrationProvider).HasMaxLength(300).IsRequired();
        b.Property(x => x.Remarks).HasMaxLength(10000);
        b.Property(x => x.CalibrationDate).HasColumnType("date"); b.Property(x => x.ExpiryDate).HasColumnType("date");
        b.Property(x => x.Result).HasConversion<string>();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.CalibrationProvider, x.CertificateNumber }).IsUnique();
        b.HasIndex(x => new { x.MachineId, x.CalibrationDate, x.CreatedAt });
        b.HasIndex(x => x.ExpiryDate);
        b.ToTable(t => t.HasCheckConstraint("CK_CalibrationCertificates_Dates", "\"ExpiryDate\" > \"CalibrationDate\""));
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CertificateFile).WithMany().HasForeignKey(x => x.CertificateFileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
