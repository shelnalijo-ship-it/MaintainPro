using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> b)
    {
        b.ToTable("NotificationEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.NotificationType).HasConversion<string>();
        b.Property(x => x.Priority).HasConversion<string>();
        b.Property(x => x.Title).IsRequired();
        b.Property(x => x.Message).IsRequired();
        b.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ProcessedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
        b.HasIndex(x => new { x.ProcessedAt, x.WorkOrderId });
        b.ToTable(t => t.HasCheckConstraint("CK_NotificationEvents_Level", "\"EscalationLevel\" BETWEEN 0 AND 3"));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
