using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.Property(t => t.Token).IsRequired().HasMaxLength(128);
        builder.Property(t => t.CreatedByIp).IsRequired().HasMaxLength(64);
        builder.Property(t => t.UserAgent).HasMaxLength(500);
        builder.Property(t => t.RevokedReason).HasMaxLength(200);
        builder.Property(t => t.ReplacedByToken).HasMaxLength(128);
        builder.Ignore(t => t.IsActive);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.ExpiresAtUtc });
    }
}
