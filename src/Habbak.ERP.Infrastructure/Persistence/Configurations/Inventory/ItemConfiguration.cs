using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");

        builder.Property(i => i.Code).IsRequired().HasMaxLength(50);
        builder.Property(i => i.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(i => i.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Barcode).HasMaxLength(100);
        builder.Property(i => i.TaxCode).HasMaxLength(50);
        builder.Property(i => i.StandardCost).HasPrecision(18, 4);
        builder.Property(i => i.DefaultPrice).HasPrecision(18, 4);

        builder.HasOne(i => i.ItemGroup)
            .WithMany()
            .HasForeignKey(i => i.ItemGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.POSCategory)
            .WithMany()
            .HasForeignKey(i => i.POSCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.BaseUnitOfMeasure)
            .WithMany()
            .HasForeignKey(i => i.BaseUnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.PurchaseUnitOfMeasure)
            .WithMany()
            .HasForeignKey(i => i.PurchaseUnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.SellUnitOfMeasure)
            .WithMany()
            .HasForeignKey(i => i.SellUnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.Barcode);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(i => new { i.CompanyId, i.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ItemUnitConversionConfiguration : IEntityTypeConfiguration<ItemUnitConversion>
{
    public void Configure(EntityTypeBuilder<ItemUnitConversion> builder)
    {
        builder.ToTable("ItemUnitConversions");

        builder.Property(c => c.ConversionFactor).HasPrecision(18, 6);

        builder.HasOne(c => c.Item)
            .WithMany(i => i.UnitConversions)
            .HasForeignKey(c => c.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.AlternateUnitOfMeasure)
            .WithMany()
            .HasForeignKey(c => c.AlternateUnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(c => new { c.ItemId, c.AlternateUnitOfMeasureId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ItemWarehouseSettingsConfiguration : IEntityTypeConfiguration<ItemWarehouseSettings>
{
    public void Configure(EntityTypeBuilder<ItemWarehouseSettings> builder)
    {
        builder.ToTable("ItemWarehouseSettings");

        builder.Property(s => s.MinStockLevel).HasPrecision(18, 4);
        builder.Property(s => s.MaxStockLevel).HasPrecision(18, 4);
        builder.Property(s => s.ReorderPoint).HasPrecision(18, 4);

        builder.HasOne(s => s.Item)
            .WithMany(i => i.WarehouseSettings)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(s => new { s.ItemId, s.WarehouseId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class BranchItemLimitConfiguration : IEntityTypeConfiguration<BranchItemLimit>
{
    public void Configure(EntityTypeBuilder<BranchItemLimit> builder)
    {
        builder.ToTable("BranchItemLimits");

        builder.Property(l => l.MinRequestQuantity).HasPrecision(18, 4);
        builder.Property(l => l.MaxRequestQuantity).HasPrecision(18, 4);

        builder.HasOne(l => l.Item)
            .WithMany(i => i.BranchItemLimits)
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(l => new { l.ItemId, l.BranchId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
