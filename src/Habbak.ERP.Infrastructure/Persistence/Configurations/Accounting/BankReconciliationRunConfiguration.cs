using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class BankReconciliationRunConfiguration : IEntityTypeConfiguration<BankReconciliationRun>
{
    public void Configure(EntityTypeBuilder<BankReconciliationRun> builder)
    {
        builder.ToTable("BankReconciliationRuns");

        builder.HasOne(r => r.BankAccount)
            .WithMany()
            .HasForeignKey(r => r.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.AdjustmentJournalEntry)
            .WithMany()
            .HasForeignKey(r => r.AdjustmentJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.BankAccountId, r.PeriodFrom, r.PeriodTo });
    }
}

public class BankReconciliationLineConfiguration : IEntityTypeConfiguration<BankReconciliationLine>
{
    public void Configure(EntityTypeBuilder<BankReconciliationLine> builder)
    {
        builder.ToTable("BankReconciliationLines");

        builder.Property(l => l.MatchedAmount).HasPrecision(18, 4);

        builder.HasOne(l => l.BankReconciliationRun)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.BankReconciliationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // SystemTransactionId is a polymorphic reference (Voucher/TreasuryTransfer/JournalEntry
        // per SystemTransactionType) — deliberately not an FK. Rule 23 (both-null / type-required
        // when set) is enforced by a CHECK constraint since it is expressible in SQL.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_BankReconciliationLines_SourceRequired",
            "([SystemTransactionId] IS NOT NULL OR [BankStatementLineId] IS NOT NULL) " +
            "AND ([SystemTransactionId] IS NULL OR [SystemTransactionType] IS NOT NULL)"));
    }
}
