using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

// System-wide, not company-scoped: AppDbContext gives User only the soft-delete filter.
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(200);
        builder.Property(u => u.TwoFactorSecret).HasMaxLength(1000);
        builder.Property(u => u.TwoFactorPendingSecret).HasMaxLength(1000);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PhoneNumber).HasMaxLength(50);
        builder.Property(u => u.PreferredLanguage).HasDefaultValue(PreferredLanguage.Arabic).HasSentinel(0);
        builder.Property(u => u.Status).HasDefaultValue(UserStatus.PendingActivation).HasSentinel(0);

        builder.HasIndex(u => u.Username).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
