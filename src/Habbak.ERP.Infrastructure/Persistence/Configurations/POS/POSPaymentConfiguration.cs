using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class POSPaymentConfiguration : IEntityTypeConfiguration<POSPayment>
{
    public void Configure(EntityTypeBuilder<POSPayment> builder)
    {
        builder.ToTable("POSPayments");

        builder.Property(p => p.Amount).HasPrecision(18, 4);
        builder.Property(p => p.MachineCommission).HasPrecision(18, 4);
        builder.Property(p => p.AmountTendered).HasPrecision(18, 4);
        builder.Property(p => p.ChangeGiven).HasPrecision(18, 4);
        builder.Property(p => p.CashRoundingAdjustment).HasPrecision(18, 4);
        builder.Property(p => p.CardTransactionReference).HasMaxLength(100);

        builder.HasOne(p => p.POSInvoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.POSInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.PaymentMethod)
            .WithMany()
            .HasForeignKey(p => p.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
