using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> builder)
    {
        builder.ToTable("InventoryCounts");

        builder.Property(c => c.CountNumber).IsRequired().HasMaxLength(50);

        builder.HasOne(c => c.Warehouse)
            .WithMany()
            .HasForeignKey(c => c.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.CountNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

        // Rule 12: check for another InProgress/PendingSettlement count on the same warehouse.
        builder.HasIndex(c => new { c.WarehouseId, c.Status });
    }
}

public class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLine>
{
    public void Configure(EntityTypeBuilder<InventoryCountLine> builder)
    {
        builder.ToTable("InventoryCountLines");

        builder.Property(l => l.SystemQuantity).HasPrecision(18, 4);
        builder.Property(l => l.CountedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.VarianceQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.HasOne(l => l.Unit).WithMany().HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(l => l.SettlementReason).HasMaxLength(500);

        builder.HasOne(l => l.InventoryCount)
            .WithMany(c => c.Lines)
            .HasForeignKey(l => l.InventoryCountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.InventoryCountId, l.ItemId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
