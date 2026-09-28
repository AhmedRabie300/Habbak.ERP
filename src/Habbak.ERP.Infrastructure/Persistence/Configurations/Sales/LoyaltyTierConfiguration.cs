using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class LoyaltyTierConfiguration : IEntityTypeConfiguration<LoyaltyTier>
{
    public void Configure(EntityTypeBuilder<LoyaltyTier> builder)
    {
        builder.ToTable("LoyaltyTiers");

        builder.Property(t => t.Code).IsRequired().HasMaxLength(50);
        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(t => t.MinPointsThreshold).HasPrecision(18, 4);
        builder.Property(t => t.EarnRateMultiplier).HasPrecision(9, 4);

        builder.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
