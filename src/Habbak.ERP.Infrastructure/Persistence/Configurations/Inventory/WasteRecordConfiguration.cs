using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class WasteRecordConfiguration : IEntityTypeConfiguration<WasteRecord>
{
    public void Configure(EntityTypeBuilder<WasteRecord> builder)
    {
        builder.ToTable("WasteRecords");

        builder.Property(w => w.Quantity).HasPrecision(18, 4);
        builder.Property(w => w.Reason).IsRequired().HasMaxLength(500);
        builder.Property(w => w.SourceDocumentType).HasMaxLength(50);

        builder.HasOne(w => w.Warehouse)
            .WithMany()
            .HasForeignKey(w => w.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.Item)
            .WithMany()
            .HasForeignKey(w => w.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
