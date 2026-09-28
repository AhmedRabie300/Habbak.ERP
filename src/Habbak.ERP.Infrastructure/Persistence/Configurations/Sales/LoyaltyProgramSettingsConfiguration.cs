using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class LoyaltyProgramSettingsConfiguration : IEntityTypeConfiguration<LoyaltyProgramSettings>
{
    public void Configure(EntityTypeBuilder<LoyaltyProgramSettings> builder)
    {
        builder.ToTable("LoyaltyProgramSettings");

        builder.Property(s => s.PointsEarnRate).HasPrecision(18, 4);
        builder.Property(s => s.PointsRedemptionValue).HasPrecision(18, 4);

        builder.HasIndex(s => s.CompanyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
