using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitsOfMeasure");

        builder.Property(u => u.Code).IsRequired().HasMaxLength(20);
        builder.Property(u => u.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(u => u.NameEn).IsRequired().HasMaxLength(200);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(u => new { u.CompanyId, u.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
