using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class POSReturnConfiguration : IEntityTypeConfiguration<POSReturn>
{
    public void Configure(EntityTypeBuilder<POSReturn> builder)
    {
        builder.ToTable("POSReturns");

        builder.Property(r => r.ReturnNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);

        builder.HasOne(r => r.POSTerminal)
            .WithMany()
            .HasForeignKey(r => r.POSTerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Shift)
            .WithMany()
            .HasForeignKey(r => r.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.SourceInvoice)
            .WithMany()
            .HasForeignKey(r => r.SourceInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.ReturnNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class POSReturnLineConfiguration : IEntityTypeConfiguration<POSReturnLine>
{
    public void Configure(EntityTypeBuilder<POSReturnLine> builder)
    {
        builder.ToTable("POSReturnLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);

        builder.HasOne(l => l.POSReturn)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.POSReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Inventory.Item>()
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.POSReturnId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
