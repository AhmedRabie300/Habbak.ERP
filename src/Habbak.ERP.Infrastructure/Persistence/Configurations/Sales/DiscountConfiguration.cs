using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("Discounts");

        builder.Property(d => d.Code).IsRequired().HasMaxLength(50);
        builder.Property(d => d.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(d => d.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Value).HasPrecision(18, 4);
        builder.Property(d => d.MinInvoiceAmount).HasPrecision(18, 4);
        builder.Property(d => d.MinQuantity).HasPrecision(18, 4);

        builder.HasIndex(d => new { d.CompanyId, d.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
