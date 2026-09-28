using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>Configurations for the three section-2.7 settings entities (screens #21-23) — grouped
/// in one file since each is a small, self-contained settings shape with no cross-references.</summary>
public class ProductionSalesModeSettingConfiguration : IEntityTypeConfiguration<ProductionSalesModeSetting>
{
    public void Configure(EntityTypeBuilder<ProductionSalesModeSetting> builder)
    {
        builder.ToTable("ProductionSalesModeSettings");

        // Not a DB-enforced unique index: SQL Server treats multiple NULL ScopeId rows (the
        // Company scope) as distinct, so "at most one Company-scope row per company" is checked
        // in the command handler instead — this index only accelerates the resolution lookup
        // (rule 17) and blocks true duplicates for the non-null-ScopeId scopes (Branch/POS/Item).
        builder.HasIndex(s => new { s.CompanyId, s.ScopeType, s.ScopeId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [ScopeId] IS NOT NULL");
    }
}

public class ShortagePolicyConfiguration : IEntityTypeConfiguration<ShortagePolicy>
{
    public void Configure(EntityTypeBuilder<ShortagePolicy> builder)
    {
        builder.ToTable("ShortagePolicies");

        builder.HasIndex(p => p.CompanyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class InventorySettingsConfiguration : IEntityTypeConfiguration<InventorySettings>
{
    public void Configure(EntityTypeBuilder<InventorySettings> builder)
    {
        builder.ToTable("InventorySettingsRows");

        builder.HasIndex(s => s.CompanyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
