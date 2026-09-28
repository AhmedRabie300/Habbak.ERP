using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class POSPaymentMethodConfigConfiguration : IEntityTypeConfiguration<POSPaymentMethodConfig>
{
    public void Configure(EntityTypeBuilder<POSPaymentMethodConfig> builder)
    {
        builder.ToTable("POSPaymentMethodConfigs");

        builder.HasOne(c => c.POSTerminal)
            .WithMany()
            .HasForeignKey(c => c.POSTerminalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.PaymentMethod)
            .WithMany()
            .HasForeignKey(c => c.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.LinkedTreasuryAccount)
            .WithMany()
            .HasForeignKey(c => c.LinkedTreasuryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.POSTerminalId, c.PaymentMethodId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
