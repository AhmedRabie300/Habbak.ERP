using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class POSInvoiceConfiguration : IEntityTypeConfiguration<POSInvoice>
{
    public void Configure(EntityTypeBuilder<POSInvoice> builder)
    {
        builder.ToTable("POSInvoices");

        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Subtotal).HasPrecision(18, 4);
        builder.Property(i => i.DiscountAmount).HasPrecision(18, 4);
        builder.Property(i => i.ManualDiscountAmount).HasPrecision(18, 4);
        builder.Property(i => i.LoyaltyPointsRedeemed).HasPrecision(18, 4);
        builder.Property(i => i.LoyaltyDiscountAmount).HasPrecision(18, 4);
        builder.Property(i => i.ServiceChargeAmount).HasPrecision(18, 4);
        builder.Property(i => i.TipAmount).HasPrecision(18, 4);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 4);
        builder.Property(i => i.Total).HasPrecision(18, 4);

        builder.HasOne(i => i.POSTerminal)
            .WithMany()
            .HasForeignKey(i => i.POSTerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Shift)
            .WithMany()
            .HasForeignKey(i => i.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Check)
            .WithMany()
            .HasForeignKey(i => i.CheckId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.CompanyId, i.InvoiceNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class POSInvoiceLineConfiguration : IEntityTypeConfiguration<POSInvoiceLine>
{
    public void Configure(EntityTypeBuilder<POSInvoiceLine> builder)
    {
        builder.ToTable("POSInvoiceLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 4);

        builder.HasOne(l => l.POSInvoice)
            .WithMany(i => i.Lines)
            .HasForeignKey(l => l.POSInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Inventory.Item>()
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.POSInvoiceId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
