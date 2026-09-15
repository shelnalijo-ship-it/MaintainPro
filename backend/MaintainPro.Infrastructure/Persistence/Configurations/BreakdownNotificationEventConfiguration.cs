using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class BreakdownNotificationEventConfiguration : IEntityTypeConfiguration<BreakdownNotificationEvent>
{
    public void Configure(EntityTypeBuilder<BreakdownNotificationEvent> b)
    {
        b.ToTable("BreakdownNotificationEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired();
        b.Property(x => x.Message).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ProcessedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.NotificationType).HasConversion<string>();
        b.Property(x => x.Priority).HasConversion<string>();
        b.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
        b.HasIndex(x => new { x.ProcessedAt, x.BreakdownId });
        b.ToTable(t => t.HasCheckConstraint("CK_BreakdownNotificationEvents_AssignmentVersion", "\"AssignmentVersion\" >= 0"));
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
    }
}

