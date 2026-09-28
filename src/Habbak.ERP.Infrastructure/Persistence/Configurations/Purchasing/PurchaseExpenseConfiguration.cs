using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class PurchaseExpenseConfiguration : IEntityTypeConfiguration<PurchaseExpense>
{
    public void Configure(EntityTypeBuilder<PurchaseExpense> builder)
    {
        builder.ToTable("PurchaseExpenses");

        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasOne(e => e.PurchaseInvoice)
            .WithMany()
            .HasForeignKey(e => e.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
