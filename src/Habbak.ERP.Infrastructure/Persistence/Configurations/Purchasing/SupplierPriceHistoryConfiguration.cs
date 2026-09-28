using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class SupplierPriceHistoryConfiguration : IEntityTypeConfiguration<SupplierPriceHistory>
{
    public void Configure(EntityTypeBuilder<SupplierPriceHistory> builder)
    {
        builder.ToTable("SupplierPriceHistories");

        builder.Property(h => h.UnitPrice).HasPrecision(18, 4);
        builder.Property(h => h.Notes).HasMaxLength(500);

        builder.HasOne(h => h.Supplier)
            .WithMany()
            .HasForeignKey(h => h.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Item)
            .WithMany()
            .HasForeignKey(h => h.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Unit)
            .WithMany()
            .HasForeignKey(h => h.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(h => h.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.SupplierId, h.ItemId, h.EffectiveDate });
    }
}
