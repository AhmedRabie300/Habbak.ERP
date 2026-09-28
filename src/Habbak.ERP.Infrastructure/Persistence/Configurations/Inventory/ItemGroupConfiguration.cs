using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class ItemGroupConfiguration : IEntityTypeConfiguration<ItemGroup>
{
    public void Configure(EntityTypeBuilder<ItemGroup> builder)
    {
        builder.ToTable("ItemGroups");

        builder.Property(g => g.Code).IsRequired().HasMaxLength(50);
        builder.Property(g => g.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(g => g.NameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(g => g.Parent)
            .WithMany()
            .HasForeignKey(g => g.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(g => new { g.CompanyId, g.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
