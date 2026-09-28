using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class POSTerminalConfiguration : IEntityTypeConfiguration<POSTerminal>
{
    public void Configure(EntityTypeBuilder<POSTerminal> builder)
    {
        builder.ToTable("POSTerminals");

        builder.Property(t => t.Code).IsRequired().HasMaxLength(50);
        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(t => t.DefaultWarehouse)
            .WithMany()
            .HasForeignKey(t => t.DefaultWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
