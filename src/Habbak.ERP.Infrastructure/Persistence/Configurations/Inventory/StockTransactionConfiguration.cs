using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions");

        builder.Property(t => t.Quantity).HasPrecision(18, 4);
        builder.Property(t => t.UnitCost).HasPrecision(18, 4);
        builder.Property(t => t.SourceDocumentType).HasMaxLength(50);
        builder.Property(t => t.BatchNumber).HasMaxLength(100);

        builder.HasOne(t => t.Item)
            .WithMany()
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Warehouse)
            .WithMany()
            .HasForeignKey(t => t.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reporting/history lookups (card-per-item, per-warehouse ledgers) always filter by these.
        builder.HasIndex(t => new { t.CompanyId, t.WarehouseId, t.ItemId, t.TransactionDate });
    }
}
