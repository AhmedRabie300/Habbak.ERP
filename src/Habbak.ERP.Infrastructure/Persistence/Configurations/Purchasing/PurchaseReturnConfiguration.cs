using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns");

        builder.Property(r => r.ReturnNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        builder.HasOne(r => r.Supplier)
            .WithMany()
            .HasForeignKey(r => r.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(r => r.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Warehouse)
            .WithMany()
            .HasForeignKey(r => r.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.ReturnNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PurchaseReturnLineConfiguration : IEntityTypeConfiguration<PurchaseReturnLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReturnLine> builder)
    {
        builder.ToTable("PurchaseReturnLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.Property(l => l.BatchNumber).HasMaxLength(100);

        builder.HasOne(l => l.PurchaseReturn)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.PurchaseReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // The invoice line this return came off (Remarks4, item 8) — Restrict: an invoice line that
        // has been returned against is part of that return's history.
        builder.HasOne(l => l.PurchaseInvoiceLine)
            .WithMany()
            .HasForeignKey(l => l.PurchaseInvoiceLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.Property(l => l.BaseQuantity).HasPrecision(18, 4);
        builder.Property(l => l.BaseUnitCost).HasPrecision(18, 6);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PurchaseReturnId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
