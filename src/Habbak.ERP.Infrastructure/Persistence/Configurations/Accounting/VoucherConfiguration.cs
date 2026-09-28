using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers");

        builder.Property(v => v.VoucherNumber).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Description).HasMaxLength(500);
        builder.Property(v => v.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(v => v.Amount).HasPrecision(18, 4);
        builder.Property(v => v.ExchangeRate).HasPrecision(18, 6);
        builder.Property(v => v.BaseCurrencyAmount).HasPrecision(18, 4);

        builder.HasOne(v => v.TreasuryAccount)
            .WithMany()
            .HasForeignKey(v => v.TreasuryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.DirectAccount)
            .WithMany()
            .HasForeignKey(v => v.DirectAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.JournalEntry)
            .WithMany()
            .HasForeignKey(v => v.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // CounterpartyId (Customer/Supplier/Employee) and RelatedInvoiceId reference aggregates
        // owned by other modules not yet modeled in this solution — plain columns, no FK.

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(v => new { v.CompanyId, v.VoucherNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(v => new { v.CompanyId, v.BranchId, v.Status });
    }
}

public class TreasuryTransferConfiguration : IEntityTypeConfiguration<TreasuryTransfer>
{
    public void Configure(EntityTypeBuilder<TreasuryTransfer> builder)
    {
        builder.ToTable("TreasuryTransfers");

        builder.Property(t => t.Amount).HasPrecision(18, 4);

        builder.HasOne(t => t.FromTreasuryAccount)
            .WithMany()
            .HasForeignKey(t => t.FromTreasuryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToTreasuryAccount)
            .WithMany()
            .HasForeignKey(t => t.ToTreasuryAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.JournalEntry)
            .WithMany()
            .HasForeignKey(t => t.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.CompanyId, t.BranchId, t.Status });
    }
}
