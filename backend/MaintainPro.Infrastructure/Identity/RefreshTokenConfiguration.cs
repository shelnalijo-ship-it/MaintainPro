using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Identity;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(token => token.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(token => token.ExpiresAt).HasColumnType("timestamp with time zone");
        builder.Property(token => token.RevokedAt).HasColumnType("timestamp with time zone");
        builder.Property(token => token.CreatedByIp).HasMaxLength(64);
        builder.Property(token => token.RevokedByIp).HasMaxLength(64);
        builder.Property(token => token.Version).IsConcurrencyToken();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => new { token.UserId, token.RevokedAt });
        builder.HasOne(token => token.User).WithMany().HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(token => token.ReplacedByToken).WithMany()
            .HasForeignKey(token => token.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
    }
}
