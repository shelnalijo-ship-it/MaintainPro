using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CalibrationNotificationEventConfiguration : IEntityTypeConfiguration<CalibrationNotificationEvent>
{
    public void Configure(EntityTypeBuilder<CalibrationNotificationEvent> b)
    {
        b.ToTable("CalibrationNotificationEvents"); b.HasKey(x => x.Id);
        b.Property(x => x.NotificationType).HasConversion<string>(); b.Property(x => x.Priority).HasConversion<string>();
        b.Property(x => x.DeduplicationKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Message).HasMaxLength(10000).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone"); b.Property(x => x.ProcessedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.DeduplicationKey).IsUnique(); b.HasIndex(x => new { x.ProcessedAt, x.MachineId });
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Certificate).WithMany().HasForeignKey(x => x.CalibrationCertificateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Renewal).WithMany().HasForeignKey(x => x.CalibrationRenewalId).OnDelete(DeleteBehavior.Restrict);
    }
}
