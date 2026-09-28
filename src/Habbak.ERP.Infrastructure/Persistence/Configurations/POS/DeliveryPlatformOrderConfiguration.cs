using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class DeliveryPlatformOrderConfiguration : IEntityTypeConfiguration<DeliveryPlatformOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryPlatformOrder> builder)
    {
        builder.ToTable("DeliveryPlatformOrders");

        builder.Property(o => o.PlatformName).IsRequired().HasMaxLength(100);
        builder.Property(o => o.PlatformOrderId).IsRequired().HasMaxLength(100);
        builder.Property(o => o.CustomerName).HasMaxLength(200);
        builder.Property(o => o.CustomerPhone).HasMaxLength(30);
        builder.Property(o => o.DeliveryAddress).HasMaxLength(500);

        builder.HasOne(o => o.Check)
            .WithMany()
            .HasForeignKey(o => o.CheckId)
            .OnDelete(DeleteBehavior.Restrict);

        // قاعدة 17: قيد فريد مركّب لمنع معالجة نفس طلب المنصة الخارجية مرتين.
        builder.HasIndex(o => new { o.CompanyId, o.PlatformName, o.PlatformOrderId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
