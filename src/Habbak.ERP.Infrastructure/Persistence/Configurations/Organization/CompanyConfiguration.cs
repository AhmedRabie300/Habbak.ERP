using Habbak.ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Organization;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CommercialRegister).HasMaxLength(100);
        builder.Property(c => c.TaxCard).HasMaxLength(100);

        builder.HasOne(c => c.BaseCurrency)
            .WithMany()
            .HasForeignKey(c => c.BaseCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE. Global — the tenant root has no CompanyId.
        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
