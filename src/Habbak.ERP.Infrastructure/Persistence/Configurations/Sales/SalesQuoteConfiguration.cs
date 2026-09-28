using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class SalesQuoteConfiguration : IEntityTypeConfiguration<SalesQuote>
{
    public void Configure(EntityTypeBuilder<SalesQuote> builder)
    {
        builder.ToTable("SalesQuotes");

        builder.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(50);
        builder.Property(q => q.Subtotal).HasPrecision(18, 4);

        builder.HasOne(q => q.Customer)
            .WithMany()
            .HasForeignKey(q => q.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.CompanyId, q.QuoteNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SalesQuoteLineConfiguration : IEntityTypeConfiguration<SalesQuoteLine>
{
    public void Configure(EntityTypeBuilder<SalesQuoteLine> builder)
    {
        builder.ToTable("SalesQuoteLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 4);
        builder.Property(l => l.LineTotal).HasPrecision(18, 4);

        builder.HasOne(l => l.SalesQuote)
            .WithMany(q => q.Lines)
            .HasForeignKey(l => l.SalesQuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.SalesQuoteId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
