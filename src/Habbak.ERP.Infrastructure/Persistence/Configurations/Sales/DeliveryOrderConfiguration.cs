using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class DeliveryOrderConfiguration : IEntityTypeConfiguration<DeliveryOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryOrder> builder)
    {
        builder.ToTable("DeliveryOrders");

        builder.Property(d => d.DeliveryNumber).IsRequired().HasMaxLength(50);

        builder.HasOne(d => d.Customer)
            .WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Warehouse)
            .WithMany()
            .HasForeignKey(d => d.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.SourceOrder)
            .WithMany()
            .HasForeignKey(d => d.SourceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.SourceInvoice)
            .WithMany()
            .HasForeignKey(d => d.SourceInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.CompanyId, d.DeliveryNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DeliveryOrderLineConfiguration : IEntityTypeConfiguration<DeliveryOrderLine>
{
    public void Configure(EntityTypeBuilder<DeliveryOrderLine> builder)
    {
        builder.ToTable("DeliveryOrderLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.BatchNumber).HasMaxLength(100);

        builder.HasOne(l => l.DeliveryOrder)
            .WithMany(d => d.Lines)
            .HasForeignKey(l => l.DeliveryOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.DeliveryOrderId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
