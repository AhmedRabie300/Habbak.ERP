using Habbak.ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Organization;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");

        builder.Property(c => c.Code).IsRequired().HasMaxLength(3);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE. System-wide — no CompanyId in the key.
        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");

        // Exactly one default currency (at most one here; the commands keep it at least one).
        builder.HasIndex(c => c.IsDefault).IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
    }
}
