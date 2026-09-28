using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("LoginAttempts");

        builder.Property(a => a.Username).IsRequired().HasMaxLength(100);
        builder.Property(a => a.IpAddress).IsRequired().HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.FailureReason).HasMaxLength(200);

        // No FK: an attempt with an unknown username has no user, and attempts must outlive the user row.
        builder.HasIndex(a => new { a.Username, a.AttemptedAtUtc });
        builder.HasIndex(a => new { a.UserId, a.AttemptedAtUtc });
    }
}
