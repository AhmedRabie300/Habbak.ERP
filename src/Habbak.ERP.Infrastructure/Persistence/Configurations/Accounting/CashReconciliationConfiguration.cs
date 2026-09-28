using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class CashReconciliationConfiguration : IEntityTypeConfiguration<CashReconciliation>
{
    public void Configure(EntityTypeBuilder<CashReconciliation> builder)
    {
        builder.ToTable("CashReconciliations");

        builder.Property(c => c.ExpectedBalance).HasPrecision(18, 4);
        builder.Property(c => c.ActualBalance).HasPrecision(18, 4);
        builder.Property(c => c.DifferenceAmount).HasPrecision(18, 4);
        builder.Property(c => c.DifferenceReason).HasMaxLength(500);

        builder.HasOne(c => c.TreasuryAccount)
            .WithMany()
            .HasForeignKey(c => c.TreasuryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.BranchId, c.TreasuryAccountId, c.ReconciliationDate })
            .IsUnique();
    }
}

public class CashReconciliationDenominationConfiguration : IEntityTypeConfiguration<CashReconciliationDenomination>
{
    public void Configure(EntityTypeBuilder<CashReconciliationDenomination> builder)
    {
        builder.ToTable("CashReconciliationDenominations");

        builder.Property(d => d.DenominationValue).HasPrecision(18, 4);

        builder.HasOne(d => d.CashReconciliation)
            .WithMany(c => c.Denominations)
            .HasForeignKey(d => d.CashReconciliationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
