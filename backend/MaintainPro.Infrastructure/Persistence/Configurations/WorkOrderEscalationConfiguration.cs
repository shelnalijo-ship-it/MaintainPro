using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderEscalationConfiguration : IEntityTypeConfiguration<WorkOrderEscalation>
{
    public void Configure(EntityTypeBuilder<WorkOrderEscalation> b)
    {
        b.ToTable("WorkOrderEscalations");
        b.HasKey(x => x.Id);
        b.Property(x => x.TriggerType).HasConversion<string>();
        b.Property(x => x.TriggeredAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ResolvedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(500);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
        b.HasIndex(x => x.NotificationId).IsUnique();
        b.HasIndex(x => new { x.WorkOrderId, x.ResolvedAt, x.Level });
        b.ToTable(t => t.HasCheckConstraint("CK_WorkOrderEscalations_Level", "\"Level\" BETWEEN 1 AND 3"));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RecipientUser).WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
