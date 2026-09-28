using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class CheckConfiguration : IEntityTypeConfiguration<Check>
{
    public void Configure(EntityTypeBuilder<Check> builder)
    {
        builder.ToTable("Checks");

        builder.Property(c => c.CheckCode).IsRequired().HasMaxLength(50);
        builder.Property(c => c.ManualDiscountValue).HasPrecision(18, 4);
        builder.Property(c => c.ManualDiscountReason).HasMaxLength(500);
        builder.Property(c => c.LoyaltyPointsToRedeem).HasPrecision(18, 4);

        builder.HasOne(c => c.POSTerminal)
            .WithMany()
            .HasForeignKey(c => c.POSTerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Shift)
            .WithMany()
            .HasForeignKey(c => c.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Table)
            .WithMany()
            .HasForeignKey(c => c.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.MergedIntoCheck)
            .WithMany()
            .HasForeignKey(c => c.MergedIntoCheckId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.CheckCode }).IsUnique().HasFilter("[IsDeleted] = 0");

        // قاعدة 2.2/30: طرابيزة واحدة أقصى ما يكون عليها شيك واحد غير منتهٍ (Open/Held) في نفس
        // اللحظة — دفاع على مستوى الـDB يكمّل التحقق على مستوى التطبيق في OpenCheckForTableCommand.
        builder.HasIndex(c => c.TableId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [TableId] IS NOT NULL AND [Status] IN (1, 2)");
    }
}

public class CheckLineConfiguration : IEntityTypeConfiguration<CheckLine>
{
    public void Configure(EntityTypeBuilder<CheckLine> builder)
    {
        builder.ToTable("CheckLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);
        builder.Property(l => l.DiscountAmount).HasPrecision(18, 4);
        builder.Property(l => l.Note).HasMaxLength(500);

        builder.HasOne(l => l.Check)
            .WithMany(c => c.Lines)
            .HasForeignKey(l => l.CheckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Inventory.Item>()
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.CheckId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class CheckLineVoidConfiguration : IEntityTypeConfiguration<CheckLineVoid>
{
    public void Configure(EntityTypeBuilder<CheckLineVoid> builder)
    {
        builder.ToTable("CheckLineVoids");

        builder.Property(v => v.Quantity).HasPrecision(18, 4);
        builder.Property(v => v.UnitPrice).HasPrecision(18, 4);
        builder.Property(v => v.Reason).IsRequired().HasMaxLength(500);

        builder.HasOne(v => v.Check)
            .WithMany()
            .HasForeignKey(v => v.CheckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
