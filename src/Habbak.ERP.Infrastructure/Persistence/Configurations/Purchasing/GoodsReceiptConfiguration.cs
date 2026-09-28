using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("GoodsReceipts");

        builder.Property(r => r.ReceiptNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        builder.HasOne(r => r.Warehouse)
            .WithMany()
            .HasForeignKey(r => r.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Supplier)
            .WithMany()
            .HasForeignKey(r => r.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Both sources are optional at the schema level; "exactly one of them" is enforced by
        // CreateGoodsReceiptCommand's validator.
        builder.HasOne(r => r.PurchaseOrder)
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(r => r.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.ReceiptNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("GoodsReceiptLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.AcceptedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.RejectedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.RejectedReason).HasMaxLength(500);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.Property(l => l.ExpectedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.VarianceQuantity).HasPrecision(18, 4);
        builder.Property(l => l.VarianceReason).HasMaxLength(500);
        builder.Property(l => l.BatchNumber).HasMaxLength(100);
        builder.Property(l => l.QualityCheckNotes).HasMaxLength(500);

        builder.HasOne(l => l.GoodsReceipt)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.Property(l => l.BaseQuantity).HasPrecision(18, 4);
        builder.Property(l => l.BaseUnitCost).HasPrecision(18, 6);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.RejectedWarehouse)
            .WithMany()
            .HasForeignKey(l => l.RejectedWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.GoodsReceiptId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
