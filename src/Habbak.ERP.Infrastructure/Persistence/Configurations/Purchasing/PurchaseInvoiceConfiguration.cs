using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");

        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
        builder.Property(i => i.SupplierInvoiceNumber).HasMaxLength(100);
        builder.Property(i => i.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(i => i.ExchangeRate).HasPrecision(18, 6);
        builder.Property(i => i.Subtotal).HasPrecision(18, 4);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 4);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 4);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 4);
        builder.Property(i => i.DiscountReason).HasMaxLength(500);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 4);
        builder.Property(i => i.AdditionalCosts).HasPrecision(18, 4);
        builder.Property(i => i.CommissionRate).HasPrecision(9, 4);
        builder.Property(i => i.CommissionAmount).HasPrecision(18, 4);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasOne(i => i.Supplier)
            .WithMany()
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.PurchaseOrder)
            .WithMany()
            .HasForeignKey(i => i.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Warehouse)
            .WithMany()
            .HasForeignKey(i => i.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.GoodsReceipt)
            .WithMany()
            .HasForeignKey(i => i.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.JournalEntry)
            .WithMany()
            .HasForeignKey(i => i.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PurchaseInvoiceLineConfiguration : IEntityTypeConfiguration<PurchaseInvoiceLine>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceLine> builder)
    {
        builder.ToTable("PurchaseInvoiceLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.TotalPrice).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 4);
        builder.Property(l => l.AllocatedAdditionalCost).HasPrecision(18, 4);
        builder.Property(l => l.AllocationPercentage).HasPrecision(9, 4);
        builder.Property(l => l.Weight).HasPrecision(18, 4);

        builder.HasOne(l => l.PurchaseInvoice)
            .WithMany(i => i.Lines)
            .HasForeignKey(l => l.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // The order line this bills (Remarks6) — Restrict: an order line that has been invoiced is
        // part of that invoice’s history, and its InvoicedQuantity is kept in step with it.
        builder.HasOne(l => l.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(l => l.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.PurchaseOrderLineId);

        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.Property(l => l.BaseQuantity).HasPrecision(18, 4);
        builder.Property(l => l.BaseUnitCost).HasPrecision(18, 6);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PurchaseInvoiceId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
