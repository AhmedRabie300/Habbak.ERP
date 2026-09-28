using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class CostCenterDimensionConfiguration : IEntityTypeConfiguration<CostCenterDimension>
{
    public void Configure(EntityTypeBuilder<CostCenterDimension> builder)
    {
        builder.ToTable("CostCenterDimensions");

        builder.Property(d => d.Code).IsRequired().HasMaxLength(50);
        builder.Property(d => d.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(d => d.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(d => new { d.CompanyId, d.Code }).IsUnique();
    }
}

public class CostCenterDimensionValueConfiguration : IEntityTypeConfiguration<CostCenterDimensionValue>
{
    public void Configure(EntityTypeBuilder<CostCenterDimensionValue> builder)
    {
        builder.ToTable("CostCenterDimensionValues");

        builder.Property(v => v.Code).IsRequired().HasMaxLength(50);
        builder.Property(v => v.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(v => v.NameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(v => v.CostCenterDimension)
            .WithMany(d => d.Values)
            .HasForeignKey(v => v.CostCenterDimensionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Parent)
            .WithMany()
            .HasForeignKey(v => v.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(v => new { v.CostCenterDimensionId, v.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AccountDimensionLinkConfiguration : IEntityTypeConfiguration<AccountDimensionLink>
{
    public void Configure(EntityTypeBuilder<AccountDimensionLink> builder)
    {
        builder.ToTable("AccountDimensionLinks");

        builder.HasOne(l => l.Account)
            .WithMany(a => a.DimensionLinks)
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.CostCenterDimension)
            .WithMany()
            .HasForeignKey(l => l.CostCenterDimensionId)
            .OnDelete(DeleteBehavior.Restrict);

        // One link per dimension per account. The "max 5 dimensions per account" cap (rule 24)
        // is a cross-row count invariant and is enforced by the Application layer, not the DB.
        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE — re-linking a dimension the account
        // just unlinked must not collide with the row it just soft-deleted.
        builder.HasIndex(l => new { l.AccountId, l.CostCenterDimensionId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
