using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class BranchPOSSettingsConfiguration : IEntityTypeConfiguration<BranchPOSSettings>
{
    public void Configure(EntityTypeBuilder<BranchPOSSettings> builder)
    {
        builder.ToTable("BranchPOSSettings");

        builder.Property(s => s.ServiceChargeRate).HasPrecision(9, 4);
        builder.Property(s => s.VatRate).HasPrecision(9, 4);
        builder.Property(s => s.CashRoundingIncrement).HasPrecision(18, 4);

        builder.HasIndex(s => s.BranchId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
