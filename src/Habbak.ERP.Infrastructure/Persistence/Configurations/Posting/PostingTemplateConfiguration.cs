using Habbak.ERP.Domain.Posting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Posting;

public class PostingTemplateConfiguration : IEntityTypeConfiguration<PostingTemplate>
{
    public void Configure(EntityTypeBuilder<PostingTemplate> builder)
    {
        builder.ToTable("PostingTemplates");

        builder.Property(t => t.ScreenCode).IsRequired().HasMaxLength(100);
        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.TriggerFieldName).HasMaxLength(100);
        builder.Property(t => t.TriggerFieldValue).HasMaxLength(100);

        // A screen may carry several templates; what stays unique is one current version per template.
        builder.HasIndex(t => new { t.CompanyId, t.FamilyId })
            .IsUnique()
            .HasFilter("[IsCurrentVersion] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(t => new { t.CompanyId, t.ScreenCode });
    }
}

public class PostingTemplateLineConfiguration : IEntityTypeConfiguration<PostingTemplateLine>
{
    public void Configure(EntityTypeBuilder<PostingTemplateLine> builder)
    {
        builder.ToTable("PostingTemplateLines");

        builder.Property(l => l.AccountFieldName).HasMaxLength(100);
        builder.Property(l => l.AccountResolverKey).HasMaxLength(100);
        builder.Property(l => l.AmountFieldName).HasMaxLength(100);
        builder.Property(l => l.AmountFieldNames).HasMaxLength(500);
        builder.Property(l => l.AmountMultiplier).HasPrecision(18, 6);
        builder.Property(l => l.AmountPercentage).HasPrecision(9, 4);
        builder.Property(l => l.ConditionFieldName).HasMaxLength(100);
        builder.Property(l => l.ConditionFieldValue).HasMaxLength(200);
        builder.Property(l => l.LineDescription).HasMaxLength(500);

        builder.HasOne(l => l.PostingTemplate)
            .WithMany(t => t.Lines)
            .HasForeignKey(l => l.PostingTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.FixedAccount)
            .WithMany()
            .HasForeignKey(l => l.FixedAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PostingTemplateId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PostingTemplateLineCostCenterConfiguration : IEntityTypeConfiguration<PostingTemplateLineCostCenter>
{
    public void Configure(EntityTypeBuilder<PostingTemplateLineCostCenter> builder)
    {
        builder.ToTable("PostingTemplateLineCostCenters");

        builder.Property(c => c.ValueFieldName).HasMaxLength(100);
        builder.Property(c => c.ValueResolverKey).HasMaxLength(100);
        builder.Property(c => c.RelatedEntityType).HasMaxLength(100);
        builder.Property(c => c.RelatedEntityField).HasMaxLength(100);
        builder.Property(c => c.ContextKey).HasMaxLength(100);

        builder.HasOne(c => c.PostingTemplateLine)
            .WithMany(l => l.CostCenters)
            .HasForeignKey(c => c.PostingTemplateLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CostCenterDimension)
            .WithMany()
            .HasForeignKey(c => c.CostCenterDimensionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class JournalEntryTemplateSnapshotConfiguration : IEntityTypeConfiguration<JournalEntryTemplateSnapshot>
{
    public void Configure(EntityTypeBuilder<JournalEntryTemplateSnapshot> builder)
    {
        builder.ToTable("JournalEntryTemplateSnapshots");

        builder.Property(s => s.TemplateSnapshotJson).IsRequired();

        builder.HasOne(s => s.JournalEntry)
            .WithMany()
            .HasForeignKey(s => s.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PostingTemplate>()
            .WithMany()
            .HasForeignKey(s => s.PostingTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.JournalEntryId).IsUnique().HasFilter("[IsDeleted] = 0");

        // The idempotency guarantee itself: two requests racing with the same key cannot both land.
        builder.HasIndex(s => s.IdempotencyKey).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.RequestIdempotencyKey);
        builder.HasIndex(s => s.PostingGroupId);
    }
}

public class PostingFailureConfiguration : IEntityTypeConfiguration<PostingFailure>
{
    public void Configure(EntityTypeBuilder<PostingFailure> builder)
    {
        builder.ToTable("PostingFailures");

        builder.Property(f => f.ScreenCode).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(500).IsRequired();
        builder.Property(f => f.ErrorCode).HasMaxLength(100).IsRequired();
        builder.Property(f => f.ErrorMessage).HasMaxLength(2000).IsRequired();

        builder.HasOne(f => f.ResolvedByJournalEntry)
            .WithMany()
            .HasForeignKey(f => f.ResolvedByJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.CompanyId, f.ScreenCode, f.SourceDocumentId });
        builder.HasIndex(f => new { f.CompanyId, f.IsResolved, f.OccurredAtUtc });
    }
}
