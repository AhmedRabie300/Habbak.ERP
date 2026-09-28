using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class RequestForQuotationConfiguration : IEntityTypeConfiguration<RequestForQuotation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotation> builder)
    {
        builder.ToTable("RequestsForQuotation");

        builder.Property(r => r.RFQNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        builder.HasOne(r => r.PurchaseRequest)
            .WithMany()
            .HasForeignKey(r => r.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.RFQNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RFQLineConfiguration : IEntityTypeConfiguration<RFQLine>
{
    public void Configure(EntityTypeBuilder<RFQLine> builder)
    {
        builder.ToTable("RFQLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);

        builder.HasOne(l => l.RFQ)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.RFQId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.RFQId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RFQSupplierConfiguration : IEntityTypeConfiguration<RFQSupplier>
{
    public void Configure(EntityTypeBuilder<RFQSupplier> builder)
    {
        builder.ToTable("RFQSuppliers");

        builder.HasOne(s => s.RFQ)
            .WithMany(r => r.Suppliers)
            .HasForeignKey(s => s.RFQId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Supplier)
            .WithMany()
            .HasForeignKey(s => s.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.RFQId, s.SupplierId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RFQSupplierQuoteConfiguration : IEntityTypeConfiguration<RFQSupplierQuote>
{
    public void Configure(EntityTypeBuilder<RFQSupplierQuote> builder)
    {
        builder.ToTable("RFQSupplierQuotes");

        builder.Property(q => q.UnitPrice).HasPrecision(18, 4);
        builder.Property(q => q.DiscountPercentage).HasPrecision(9, 4);
        builder.Property(q => q.Notes).HasMaxLength(500);

        builder.HasOne(q => q.RFQSupplier)
            .WithMany(s => s.Quotes)
            .HasForeignKey(q => q.RFQSupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.RFQLine)
            .WithMany(l => l.Quotes)
            .HasForeignKey(q => q.RFQLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.RFQSupplierId, q.RFQLineId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
