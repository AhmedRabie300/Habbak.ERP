using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class POSCategoryConfiguration : IEntityTypeConfiguration<POSCategory>
{
    public void Configure(EntityTypeBuilder<POSCategory> builder)
    {
        builder.ToTable("POSCategories");

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
