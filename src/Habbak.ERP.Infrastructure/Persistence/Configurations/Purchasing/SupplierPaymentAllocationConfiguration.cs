using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class SupplierPaymentAllocationConfiguration : IEntityTypeConfiguration<SupplierPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentAllocation> builder)
    {
        builder.ToTable("SupplierPaymentAllocations");

        builder.Property(a => a.Amount).HasPrecision(18, 4);

        // Cascade from the voucher: an allocation has no life of its own once the payment is gone.
        builder.HasOne(a => a.Voucher)
            .WithMany()
            .HasForeignKey(a => a.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(a => a.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (payment, invoice): the same invoice is never split twice on one payment.
        builder.HasIndex(a => new { a.VoucherId, a.PurchaseInvoiceId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => a.PurchaseInvoiceId);
    }
}
