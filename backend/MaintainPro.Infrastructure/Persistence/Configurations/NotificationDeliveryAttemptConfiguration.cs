using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> b)
    {
        b.ToTable("NotificationDeliveryAttempts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Channel).HasConversion<string>();
        b.Property(x => x.Status).HasConversion<string>();
        b.Property(x => x.AttemptedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.NotificationId, x.Channel, x.AttemptNumber }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_NotificationDeliveryAttempts_AttemptNumber", "\"AttemptNumber\" > 0"));
        b.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
