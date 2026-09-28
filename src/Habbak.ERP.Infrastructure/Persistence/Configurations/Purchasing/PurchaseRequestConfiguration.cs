using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("PurchaseRequests");

        builder.Property(r => r.RequestNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        builder.HasIndex(r => new { r.CompanyId, r.RequestNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PurchaseRequestLineConfiguration : IEntityTypeConfiguration<PurchaseRequestLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> builder)
    {
        builder.ToTable("PurchaseRequestLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.OrderedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.PurchaseRequest)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.Property(l => l.BaseQuantity).HasPrecision(18, 4);

        builder.HasOne(l => l.Unit)
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
