using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class BranchRequestConfiguration : IEntityTypeConfiguration<BranchRequest>
{
    public void Configure(EntityTypeBuilder<BranchRequest> builder)
    {
        builder.ToTable("BranchRequests");

        builder.Property(r => r.RequestNumber).IsRequired().HasMaxLength(50);

        builder.HasIndex(r => new { r.CompanyId, r.RequestNumber }).IsUnique();
        builder.HasIndex(r => new { r.CompanyId, r.BranchId, r.Status });
    }
}

public class BranchRequestLineConfiguration : IEntityTypeConfiguration<BranchRequestLine>
{
    public void Configure(EntityTypeBuilder<BranchRequestLine> builder)
    {
        builder.ToTable("BranchRequestLines");

        builder.Property(l => l.RequestedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.ApprovedQuantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.HasOne(l => l.Unit).WithMany().HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.BranchRequest)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.BranchRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
