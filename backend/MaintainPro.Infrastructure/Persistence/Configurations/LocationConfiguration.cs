using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Name).IsRequired();

        builder.HasOne(location => location.Department)
            .WithMany()
            .HasForeignKey(location => location.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
