using Habbak.ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Organization;

// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1. All four are system-wide reference catalogs
// (like CurrencyConfiguration) — no CompanyId, unique on Code alone, filtered on soft-delete.

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");
        builder.Property(c => c.Code).IsRequired().HasMaxLength(20);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(c => c.IsoCode).HasMaxLength(10);

        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");
        builder.Property(c => c.Code).IsRequired().HasMaxLength(20);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(c => c.Country).WithMany().HasForeignKey(c => c.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class NationalityConfiguration : IEntityTypeConfiguration<Nationality>
{
    public void Configure(EntityTypeBuilder<Nationality> builder)
    {
        builder.ToTable("Nationalities");
        builder.Property(n => n.Code).IsRequired().HasMaxLength(20);
        builder.Property(n => n.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(n => n.NameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(n => n.Country).WithMany().HasForeignKey(n => n.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => n.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("Banks");
        builder.Property(b => b.Code).IsRequired().HasMaxLength(20);
        builder.Property(b => b.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(b => b.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(b => b.SwiftCode).HasMaxLength(20);
        builder.Property(b => b.Address).HasMaxLength(500);

        builder.HasOne(b => b.Country).WithMany().HasForeignKey(b => b.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
