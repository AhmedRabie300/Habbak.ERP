using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("StockBalances");

        builder.Property(b => b.QuantityOnHand).HasPrecision(18, 4);

        // Same 18,4 as StockTransaction.UnitCost: per-gram costs are fractions of a piastre, and
        // rounding the average to 2 places would drift every time stock is received.
        builder.Property(b => b.AverageCost).HasPrecision(18, 4);

        builder.HasOne(b => b.Item)
            .WithMany()
            .HasForeignKey(b => b.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Warehouse)
            .WithMany()
            .HasForeignKey(b => b.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(b => new { b.ItemId, b.WarehouseId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
