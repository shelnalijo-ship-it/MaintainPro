using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.NotificationType).HasConversion<string>();
        b.Property(x => x.Title).IsRequired();
        b.Property(x => x.Message).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ReadAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Priority).HasConversion<string>();
        b.Property(x => x.DeduplicationKey).IsRequired().HasMaxLength(500);
        b.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        b.ToTable(t => t.HasCheckConstraint("CK_Notifications_ReadState", "(\"IsRead\" AND \"ReadAt\" IS NOT NULL) OR (NOT \"IsRead\" AND \"ReadAt\" IS NULL)"));
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
