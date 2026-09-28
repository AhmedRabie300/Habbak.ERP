using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class CustodyRegisterConfiguration : IEntityTypeConfiguration<CustodyRegister>
{
    public void Configure(EntityTypeBuilder<CustodyRegister> builder)
    {
        builder.ToTable("CustodyRegisters");

        builder.Property(c => c.Amount).HasPrecision(18, 4);

        builder.HasOne(c => c.JournalEntry)
            .WithMany()
            .HasForeignKey(c => c.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // EmployeeId references the future HR module's aggregate — plain column, no FK.
        // EmployeeIdLinked (Option A, Phase 1.2): plain nullable column, no FK — set by manual linking only.

        builder.HasIndex(c => new { c.CompanyId, c.BranchId, c.Status });
    }
}

public class CustodySettlementConfiguration : IEntityTypeConfiguration<CustodySettlement>
{
    public void Configure(EntityTypeBuilder<CustodySettlement> builder)
    {
        builder.ToTable("CustodySettlements");

        builder.Property(s => s.RemainingAmount).HasPrecision(18, 4);

        builder.HasOne(s => s.CustodyRegister)
            .WithMany(r => r.Settlements)
            .HasForeignKey(s => s.CustodyRegisterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.JournalEntry)
            .WithMany()
            .HasForeignKey(s => s.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustodySettlementLineConfiguration : IEntityTypeConfiguration<CustodySettlementLine>
{
    public void Configure(EntityTypeBuilder<CustodySettlementLine> builder)
    {
        builder.ToTable("CustodySettlementLines");

        builder.Property(l => l.Amount).HasPrecision(18, 4);
        builder.Property(l => l.Description).HasMaxLength(500);

        builder.HasOne(l => l.CustodySettlement)
            .WithMany(s => s.Lines)
            .HasForeignKey(l => l.CustodySettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
