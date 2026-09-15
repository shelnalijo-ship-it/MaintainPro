using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceTypeConfiguration : IEntityTypeConfiguration<MaintenanceType>
{
    public void Configure(EntityTypeBuilder<MaintenanceType> builder)
    {
        builder.ToTable("MaintenanceTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
    }
}
