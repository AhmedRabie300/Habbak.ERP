using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.Property(w => w.Code).IsRequired().HasMaxLength(50);
        builder.Property(w => w.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(w => w.NameEn).IsRequired().HasMaxLength(200);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(w => new { w.CompanyId, w.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
