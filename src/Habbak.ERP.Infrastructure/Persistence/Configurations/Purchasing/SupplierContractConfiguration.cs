using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class SupplierContractConfiguration : IEntityTypeConfiguration<SupplierContract>
{
    public void Configure(EntityTypeBuilder<SupplierContract> builder)
    {
        builder.ToTable("SupplierContracts");

        builder.Property(c => c.ContractNumber).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(1000);

        builder.HasOne(c => c.Supplier)
            .WithMany()
            .HasForeignKey(c => c.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.ContractNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ContractItemConfiguration : IEntityTypeConfiguration<ContractItem>
{
    public void Configure(EntityTypeBuilder<ContractItem> builder)
    {
        builder.ToTable("ContractItems");

        builder.Property(i => i.UnitPrice).HasPrecision(18, 4);
        builder.Property(i => i.MinQuantity).HasPrecision(18, 4);
        builder.Property(i => i.MaxQuantity).HasPrecision(18, 4);
        builder.Property(i => i.DiscountPercentage).HasPrecision(9, 4);

        builder.HasOne(i => i.SupplierContract)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.SupplierContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.SupplierContractId, i.ItemId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
