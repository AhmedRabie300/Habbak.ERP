using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");

        builder.Property(j => j.EntryNumber).IsRequired().HasMaxLength(50);
        builder.Property(j => j.Description).IsRequired().HasMaxLength(500);
        builder.Property(j => j.TotalDebit).HasPrecision(18, 4);
        builder.Property(j => j.TotalCredit).HasPrecision(18, 4);

        builder.HasOne(j => j.ReversalOfEntry)
            .WithMany()
            .HasForeignKey(j => j.ReversalOfEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.CompanyId, j.EntryNumber }).IsUnique();

        // Common filter combinations per 00-Project-Overview.md, section 15.
        builder.HasIndex(j => new { j.CompanyId, j.BranchId, j.Status });
        builder.HasIndex(j => new { j.CompanyId, j.EntryDate });
        builder.HasIndex(j => j.PostingGroupId);
    }
}
