using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("PriceLists");

        builder.Property(p => p.Code).IsRequired().HasMaxLength(50);
        builder.Property(p => p.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(p => p.NameEn).IsRequired().HasMaxLength(200);

        builder.HasMany(p => p.Branches)
            .WithOne(b => b.PriceList)
            .HasForeignKey(b => b.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Lines)
            .WithOne(l => l.PriceList)
            .HasForeignKey(l => l.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PriceListBranchConfiguration : IEntityTypeConfiguration<PriceListBranch>
{
    public void Configure(EntityTypeBuilder<PriceListBranch> builder)
    {
        builder.ToTable("PriceListBranches");

        builder.HasOne(b => b.Branch)
            .WithMany()
            .HasForeignKey(b => b.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.PriceListId, b.BranchId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PriceListLineConfiguration : IEntityTypeConfiguration<PriceListLine>
{
    public void Configure(EntityTypeBuilder<PriceListLine> builder)
    {
        builder.ToTable("PriceListLines");

        builder.Property(l => l.DineInPrice).HasPrecision(18, 4);
        builder.Property(l => l.TakeawayPrice).HasPrecision(18, 4);
        builder.Property(l => l.DeliveryPrice).HasPrecision(18, 4);

        builder.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PriceListId, l.ItemId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
