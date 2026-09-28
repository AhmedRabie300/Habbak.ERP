using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.Property(a => a.Code).IsRequired().HasMaxLength(50);
        builder.Property(a => a.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(a => a.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(a => a.CurrencyCode).HasMaxLength(3);

        builder.HasOne(a => a.Parent)
            .WithMany()
            .HasForeignKey(a => a.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique per company for normal accounts...
        builder.HasIndex(a => new { a.CompanyId, a.Code })
            .IsUnique()
            .HasFilter("[CompanyId] IS NOT NULL");

        // ...and unique on its own for accounts shared across every company (rule 12).
        builder.HasIndex(a => a.Code)
            .IsUnique()
            .HasFilter("[CompanyId] IS NULL");

        // The combined visibility filter (company match OR shared-across-companies) is built
        // in AppDbContext.OnModelCreating, where "this" can be captured to resolve the current
        // company per-instance — see the comment there (01-Module-Accounting.md, rule 12).
    }
}
