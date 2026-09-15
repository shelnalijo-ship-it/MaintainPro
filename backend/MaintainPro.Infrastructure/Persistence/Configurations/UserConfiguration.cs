using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Version).IsConcurrencyToken();
        builder.Property(user => user.EmployeeId).IsRequired();
        builder.Property(user => user.FirstName).IsRequired();
        builder.Property(user => user.LastName).IsRequired();
        builder.Property(user => user.Email).IsRequired();
        builder.Property(user => user.PasswordHash).IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(user => user.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(user => user.LastLoginAt).HasColumnType("timestamp with time zone");

        builder.HasIndex(user => user.EmployeeId).IsUnique();
        builder.HasIndex(user => user.Email).IsUnique();

        builder.HasOne(user => user.Department)
            .WithMany()
            .HasForeignKey(user => user.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
