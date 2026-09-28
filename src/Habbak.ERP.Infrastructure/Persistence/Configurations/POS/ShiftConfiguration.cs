using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");

        builder.Property(s => s.OpeningCashAmount).HasPrecision(18, 4);
        builder.Property(s => s.ExpectedClosingCashAmount).HasPrecision(18, 4);
        builder.Property(s => s.ActualClosingCashAmount).HasPrecision(18, 4);
        builder.Property(s => s.DifferenceAmount).HasPrecision(18, 4);

        builder.HasOne(s => s.POSTerminal)
            .WithMany()
            .HasForeignKey(s => s.POSTerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Rule 2: at most one Open shift per terminal at a time.
        builder.HasIndex(s => s.POSTerminalId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] = 1");
    }
}

public class ShiftDenominationCountConfiguration : IEntityTypeConfiguration<ShiftDenominationCount>
{
    public void Configure(EntityTypeBuilder<ShiftDenominationCount> builder)
    {
        builder.ToTable("ShiftDenominationCounts");

        builder.Property(c => c.DenominationValue).HasPrecision(18, 4);

        builder.HasOne(c => c.Shift)
            .WithMany(s => s.DenominationCounts)
            .HasForeignKey(c => c.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
