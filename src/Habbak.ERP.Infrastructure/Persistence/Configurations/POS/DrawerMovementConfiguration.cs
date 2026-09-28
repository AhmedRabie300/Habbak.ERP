using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class DrawerMovementConfiguration : IEntityTypeConfiguration<DrawerMovement>
{
    public void Configure(EntityTypeBuilder<DrawerMovement> builder)
    {
        builder.ToTable("DrawerMovements");

        builder.Property(m => m.Amount).HasPrecision(18, 4);
        builder.Property(m => m.Reason).HasMaxLength(500);

        builder.HasOne(m => m.Shift)
            .WithMany()
            .HasForeignKey(m => m.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
