using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class BlendTypeConfiguration : IEntityTypeConfiguration<BlendType>
{
    public void Configure(EntityTypeBuilder<BlendType> builder)
    {
        builder.ToTable("BlendTypes");

        builder.Property(b => b.Code).IsRequired().HasMaxLength(50);
        builder.Property(b => b.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(b => b.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(b => b.PricePerGram).HasPrecision(18, 4);

        builder.HasOne(b => b.Item)
            .WithMany()
            .HasForeignKey(b => b.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.CompanyId, b.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
