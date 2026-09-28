using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ToTable("ProductionOrders");

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
        builder.Property(o => o.PlannedQuantity).HasPrecision(18, 4);
        builder.Property(o => o.ActualQuantity).HasPrecision(18, 4);
        builder.Property(o => o.StandardCost).HasPrecision(18, 4);
        builder.Property(o => o.ActualCost).HasPrecision(18, 4);

        builder.HasOne(o => o.Warehouse)
            .WithMany()
            .HasForeignKey(o => o.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Recipe)
            .WithMany()
            .HasForeignKey(o => o.RecipeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.ProductionIssueDocument)
            .WithMany()
            .HasForeignKey(o => o.ProductionIssueDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.ProductionReceiptDocument)
            .WithMany()
            .HasForeignKey(o => o.ProductionReceiptDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.CompanyId, o.OrderNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
