using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Common;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");

        builder.Property(m => m.Code).IsRequired().HasMaxLength(100);
        builder.Property(m => m.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(m => m.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(m => m.RouteKey).HasMaxLength(200);
        builder.Property(m => m.IconKey).HasMaxLength(100);

        builder.HasIndex(m => m.Code).IsUnique();
        builder.HasIndex(m => new { m.ParentId, m.DisplayOrder });

        builder.HasOne(m => m.Parent)
            .WithMany()
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
