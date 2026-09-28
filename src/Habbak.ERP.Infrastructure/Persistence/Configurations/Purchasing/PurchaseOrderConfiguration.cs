using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
        builder.Property(o => o.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(o => o.ExchangeRate).HasPrecision(18, 6);
        builder.Property(o => o.DeliveryAddress).HasMaxLength(500);
        builder.Property(o => o.Subtotal).HasPrecision(18, 4);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 4);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 4);
        builder.Property(o => o.DiscountAmount).HasPrecision(18, 4);
        builder.Property(o => o.DiscountReason).HasMaxLength(500);
        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.HasOne(o => o.Supplier)
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // The awarded RFQ the order came from (Remarks4, item 6 — PurchaseCycleSettings.RequiresQuotation).
        builder.HasOne(o => o.RFQ)
            .WithMany()
            .HasForeignKey(o => o.RFQId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.PurchaseRequest)
            .WithMany()
            .HasForeignKey(o => o.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.CompanyId, o.OrderNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.ReceivedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.InvoicedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.TotalPrice).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 4);
        builder.Property(l => l.Weight).HasPrecision(18, 4);

        builder.HasOne(l => l.PurchaseOrder)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Remarks7 — which request line this order line came from, if any.
        builder.HasOne(l => l.PurchaseRequestLine)
            .WithMany()
            .HasForeignKey(l => l.PurchaseRequestLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.Property(l => l.BaseQuantity).HasPrecision(18, 4);
        builder.Property(l => l.BaseUnitCost).HasPrecision(18, 6);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PurchaseOrderId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
