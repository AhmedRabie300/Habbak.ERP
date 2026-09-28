using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
    {
        builder.ToTable("JournalEntryLines");

        builder.Property(l => l.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(l => l.DebitAmount).HasPrecision(18, 4);
        builder.Property(l => l.CreditAmount).HasPrecision(18, 4);
        builder.Property(l => l.ExchangeRate).HasPrecision(18, 6);
        builder.Property(l => l.BaseCurrencyDebitAmount).HasPrecision(18, 4);
        builder.Property(l => l.BaseCurrencyCreditAmount).HasPrecision(18, 4);
        builder.Property(l => l.Description).HasMaxLength(500);

        builder.HasOne(l => l.JournalEntry)
            .WithMany(j => j.Lines)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE, so an edit that replaces a line and
        // reuses its LineNumber must not collide with the row it just soft-deleted.
        builder.HasIndex(l => new { l.JournalEntryId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(l => l.AccountId);
    }
}

public class JournalEntryLineDimensionValueConfiguration : IEntityTypeConfiguration<JournalEntryLineDimensionValue>
{
    public void Configure(EntityTypeBuilder<JournalEntryLineDimensionValue> builder)
    {
        builder.ToTable("JournalEntryLineDimensionValues");

        builder.HasOne(v => v.JournalEntryLine)
            .WithMany(l => l.DimensionValues)
            .HasForeignKey(v => v.JournalEntryLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.CostCenterDimension)
            .WithMany()
            .HasForeignKey(v => v.CostCenterDimensionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CostCenterDimensionValue)
            .WithMany()
            .HasForeignKey(v => v.CostCenterDimensionValueId)
            .OnDelete(DeleteBehavior.Restrict);

        // One value per dimension per line (rule 21 is further enforced at the Application
        // layer: CostCenterDimensionValueId must belong to CostCenterDimensionId).
        builder.HasIndex(v => new { v.JournalEntryLineId, v.CostCenterDimensionId }).IsUnique();
    }
}
