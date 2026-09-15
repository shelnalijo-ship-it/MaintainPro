using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("Machines");
        builder.HasKey(machine => machine.Id);
        builder.Property(machine => machine.Version).IsConcurrencyToken();
        builder.Property(machine => machine.StatusVersion).IsConcurrencyToken();
        builder.Property(machine => machine.MachineCode).IsRequired();
        builder.Property(machine => machine.Name).IsRequired();
        builder.Property(machine => machine.Status).HasConversion<string>().IsRequired();
        builder.Property(machine => machine.Criticality).HasConversion<string>().IsRequired();
        builder.Property(machine => machine.InstallationDate).HasColumnType("date");
        builder.Property(machine => machine.CommissioningDate).HasColumnType("date");
        builder.Property(machine => machine.WarrantyExpiryDate).HasColumnType("date");
        builder.Property(machine => machine.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(machine => machine.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(machine => machine.MachineCode).IsUnique();

        builder.HasOne(machine => machine.MachineOwner)
            .WithMany()
            .HasForeignKey(machine => machine.MachineOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(machine => machine.Supervisor)
            .WithMany()
            .HasForeignKey(machine => machine.SupervisorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(machine => machine.Category)
            .WithMany()
            .HasForeignKey(machine => machine.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(machine => machine.Department)
            .WithMany()
            .HasForeignKey(machine => machine.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(machine => machine.Location)
            .WithMany()
            .HasForeignKey(machine => machine.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
