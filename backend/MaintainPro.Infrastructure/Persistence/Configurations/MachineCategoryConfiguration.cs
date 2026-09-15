using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class MachineCategoryConfiguration : IEntityTypeConfiguration<MachineCategory>
{
    public void Configure(EntityTypeBuilder<MachineCategory> builder)
    {
        builder.ToTable("MachineCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).IsRequired();
        builder.HasIndex(category => category.Name).IsUnique();
    }
}
