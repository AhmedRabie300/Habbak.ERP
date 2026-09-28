using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class WarehouseDocumentConfiguration : IEntityTypeConfiguration<WarehouseDocument>
{
    public void Configure(EntityTypeBuilder<WarehouseDocument> builder)
    {
        builder.ToTable("WarehouseDocuments");

        builder.Property(d => d.DocumentNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Notes).HasMaxLength(1000);

        builder.HasOne(d => d.SourceWarehouse)
            .WithMany()
            .HasForeignKey(d => d.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DestinationWarehouse)
            .WithMany()
            .HasForeignKey(d => d.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.RelatedWarehouseDocument)
            .WithMany()
            .HasForeignKey(d => d.RelatedWarehouseDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CustodyOfficer)
            .WithMany()
            .HasForeignKey(d => d.CustodyOfficerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.CompanyId, d.DocumentType, d.DocumentNumber }).IsUnique();
        builder.HasIndex(d => new { d.CompanyId, d.BranchId, d.Status });
        builder.HasIndex(d => new { d.CompanyId, d.DocumentDate });
    }
}

public class WarehouseDocumentLineConfiguration : IEntityTypeConfiguration<WarehouseDocumentLine>
{
    public void Configure(EntityTypeBuilder<WarehouseDocumentLine> builder)
    {
        builder.ToTable("WarehouseDocumentLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitCost).HasPrecision(18, 4);
        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.HasOne(l => l.Unit).WithMany().HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(l => l.ExpectedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.VarianceQuantity).HasPrecision(18, 4);
        builder.Property(l => l.BatchNumber).HasMaxLength(100);

        builder.HasOne(l => l.WarehouseDocument)
            .WithMany(d => d.Lines)
            .HasForeignKey(l => l.WarehouseDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into IsDeleted = true
        // rather than a physical DELETE, so an edit that replaces a line and reuses its LineNumber
        // must not collide with the row it just soft-deleted.
        builder.HasIndex(l => new { l.WarehouseDocumentId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
