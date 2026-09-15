using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CalibrationRenewalConfiguration : IEntityTypeConfiguration<CalibrationRenewal>
{
    public void Configure(EntityTypeBuilder<CalibrationRenewal> b)
    {
        b.ToTable("CalibrationRenewals"); b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>();
        b.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.MachineId).HasFilter("\"Status\" = 'IN_PROGRESS'").IsUnique();
        b.HasIndex(x => new { x.MachineId, x.StartedAt });
        b.ToTable(t => t.HasCheckConstraint("CK_CalibrationRenewals_Completion", "(\"Status\" = 'IN_PROGRESS' AND \"CompletedAt\" IS NULL AND \"CompletedCertificateId\" IS NULL) OR (\"Status\" = 'COMPLETED' AND \"CompletedAt\" IS NOT NULL AND \"CompletedCertificateId\" IS NOT NULL) OR (\"Status\" = 'CANCELLED' AND \"CompletedAt\" IS NOT NULL AND \"CompletedCertificateId\" IS NULL)"));
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PreviousCertificate).WithMany().HasForeignKey(x => x.PreviousCertificateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompletedCertificate).WithMany().HasForeignKey(x => x.CompletedCertificateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
