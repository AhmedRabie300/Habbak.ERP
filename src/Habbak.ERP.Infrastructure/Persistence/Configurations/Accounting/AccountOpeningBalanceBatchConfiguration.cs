using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class AccountOpeningBalanceBatchConfiguration : IEntityTypeConfiguration<AccountOpeningBalanceBatch>
{
    public void Configure(EntityTypeBuilder<AccountOpeningBalanceBatch> builder)
    {
        builder.ToTable("AccountOpeningBalanceBatches");

        builder.Property(b => b.BatchNumber).IsRequired().HasMaxLength(50);
        builder.Property(b => b.Notes).HasMaxLength(1000);

        builder.HasOne(b => b.JournalEntry)
            .WithMany()
            .HasForeignKey(b => b.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.CompanyId, b.BatchNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AccountOpeningBalanceLineConfiguration : IEntityTypeConfiguration<AccountOpeningBalanceLine>
{
    public void Configure(EntityTypeBuilder<AccountOpeningBalanceLine> builder)
    {
        builder.ToTable("AccountOpeningBalanceLines");

        builder.Property(l => l.Amount).HasPrecision(18, 4);
        builder.Property(l => l.Notes).HasMaxLength(500);

        builder.HasOne(l => l.Batch)
            .WithMany(b => b.Lines)
            .HasForeignKey(l => l.AccountOpeningBalanceBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.AccountOpeningBalanceBatchId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
