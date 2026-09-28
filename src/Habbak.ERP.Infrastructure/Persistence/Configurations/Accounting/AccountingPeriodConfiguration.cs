using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("AccountingPeriods");

        builder.HasIndex(p => new { p.CompanyId, p.PeriodStart, p.PeriodEnd }).IsUnique();
        builder.HasIndex(p => new { p.CompanyId, p.Status });
    }
}

public class PeriodCloseChecklistItemConfiguration : IEntityTypeConfiguration<PeriodCloseChecklistItem>
{
    public void Configure(EntityTypeBuilder<PeriodCloseChecklistItem> builder)
    {
        builder.ToTable("PeriodCloseChecklistItems");

        builder.HasOne(i => i.Period)
            .WithMany(p => p.ChecklistItems)
            .HasForeignKey(i => i.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.PeriodId, i.ItemKey }).IsUnique();
    }
}
