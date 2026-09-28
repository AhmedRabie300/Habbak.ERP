using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class DrawerExpenseConfiguration : IEntityTypeConfiguration<DrawerExpense>
{
    public void Configure(EntityTypeBuilder<DrawerExpense> builder)
    {
        builder.ToTable("DrawerExpenses");

        builder.Property(e => e.Amount).HasPrecision(18, 4);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);

        builder.HasOne(e => e.Shift)
            .WithMany()
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ExpenseAccount)
            .WithMany()
            .HasForeignKey(e => e.ExpenseAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
