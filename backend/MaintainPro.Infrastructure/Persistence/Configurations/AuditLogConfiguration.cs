using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Action).IsRequired();
        builder.Property(log => log.EntityType).IsRequired();
        builder.Property(log => log.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(log => log.CreatedAt);
        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt });
        builder.HasOne(log => log.User).WithMany().HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
