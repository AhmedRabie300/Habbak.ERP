using Habbak.ERP.Domain.FixedAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.FixedAssets;

// Fixed assets & maintenance (Docs/Modules/08-Module-Maintenance-FixedAssets-Request.md). Unique
// indexes are filtered on IsDeleted like everywhere else; every reference is Restrict — nothing in
// this module deletes in cascade except the lines of their own document.

public class FixedAssetCategoryConfiguration : IEntityTypeConfiguration<FixedAssetCategory>
{
    public void Configure(EntityTypeBuilder<FixedAssetCategory> builder)
    {
        builder.ToTable("FixedAssetCategories");
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(c => c.DefaultDepreciationRate).HasPrecision(9, 4);
        builder.Property(c => c.DefaultSalvagePercentage).HasPrecision(9, 4);

        builder.HasOne(c => c.AssetAccount).WithMany().HasForeignKey(c => c.AssetAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AccumulatedDepreciationAccount).WithMany().HasForeignKey(c => c.AccumulatedDepreciationAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DepreciationExpenseAccount).WithMany().HasForeignKey(c => c.DepreciationExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DisposalGainAccount).WithMany().HasForeignKey(c => c.DisposalGainAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.DisposalLossAccount).WithMany().HasForeignKey(c => c.DisposalLossAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.MaintenanceExpenseAccount).WithMany().HasForeignKey(c => c.MaintenanceExpenseAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> builder)
    {
        builder.ToTable("FixedAssets");
        builder.Property(a => a.AssetNumber).IsRequired().HasMaxLength(50);
        builder.Property(a => a.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(a => a.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(a => a.SerialNumber).HasMaxLength(100);
        builder.Property(a => a.Barcode).HasMaxLength(100);
        builder.Property(a => a.Location).HasMaxLength(200);
        builder.Property(a => a.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(a => a.AcquisitionCost).HasPrecision(18, 4);
        builder.Property(a => a.ExchangeRate).HasPrecision(18, 6);
        builder.Property(a => a.BaseCurrencyAmount).HasPrecision(18, 4);
        builder.Property(a => a.SalvageValue).HasPrecision(18, 4);
        builder.Property(a => a.DepreciationRate).HasPrecision(9, 4);
        builder.Property(a => a.AccumulatedDepreciation).HasPrecision(18, 4);
        builder.Property(a => a.DisposalProceeds).HasPrecision(18, 4);
        builder.Property(a => a.DisposalReason).HasMaxLength(500);
        builder.Property(a => a.CustodyOfficerName).HasMaxLength(200);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Ignore(a => a.NetBookValue);

        builder.HasOne(a => a.Branch).WithMany().HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Category).WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Supplier).WithMany().HasForeignKey(a => a.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.PurchaseInvoice).WithMany().HasForeignKey(a => a.PurchaseInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.FundingAccount).WithMany().HasForeignKey(a => a.FundingAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AcquisitionJournalEntry).WithMany().HasForeignKey(a => a.AcquisitionJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.DisposalJournalEntry).WithMany().HasForeignKey(a => a.DisposalJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.CustodyOfficer).WithMany().HasForeignKey(a => a.CustodyOfficerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.CostCenterValue).WithMany().HasForeignKey(a => a.CostCenterValueId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.CompanyId, a.AssetNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => new { a.CompanyId, a.Status });
    }
}

public class DepreciationScheduleConfiguration : IEntityTypeConfiguration<DepreciationSchedule>
{
    public void Configure(EntityTypeBuilder<DepreciationSchedule> builder)
    {
        builder.ToTable("DepreciationSchedules");
        builder.Property(s => s.Amount).HasPrecision(18, 4);
        builder.Property(s => s.AccumulatedAfter).HasPrecision(18, 4);
        builder.Property(s => s.BookValueAfter).HasPrecision(18, 4);
        builder.Ignore(s => s.IsPosted);

        builder.HasOne(s => s.FixedAsset).WithMany(a => a.Schedule).HasForeignKey(s => s.FixedAssetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.DepreciationRun).WithMany(r => r.Periods).HasForeignKey(s => s.DepreciationRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.JournalEntry).WithMany().HasForeignKey(s => s.JournalEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.FixedAssetId, s.PeriodNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => new { s.Status, s.PeriodEnd });
    }
}

public class DepreciationRunConfiguration : IEntityTypeConfiguration<DepreciationRun>
{
    public void Configure(EntityTypeBuilder<DepreciationRun> builder)
    {
        builder.ToTable("DepreciationRuns");
        builder.Property(r => r.RunNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.TotalDepreciation).HasPrecision(18, 4);

        builder.HasOne(r => r.JournalEntry).WithMany().HasForeignKey(r => r.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ReversalJournalEntry).WithMany().HasForeignKey(r => r.ReversalJournalEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.RunNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

        // One live run per company-month (rule 28): the key repeats only after the run holding it is reversed.
        builder.HasIndex(r => r.IdempotencyKey).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] <> 3");
    }
}

public class AssetTransferConfiguration : IEntityTypeConfiguration<AssetTransfer>
{
    public void Configure(EntityTypeBuilder<AssetTransfer> builder)
    {
        builder.ToTable("AssetTransfers");
        builder.Property(t => t.TransferNumber).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.Property(t => t.CustodyOfficerNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Notes).HasMaxLength(1000);

        builder.HasOne(t => t.FixedAsset).WithMany().HasForeignKey(t => t.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.FromBranch).WithMany().HasForeignKey(t => t.FromBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToBranch).WithMany().HasForeignKey(t => t.ToBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CustodyOfficer).WithMany().HasForeignKey(t => t.CustodyOfficerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.CompanyId, t.TransferNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AssetDisposalConfiguration : IEntityTypeConfiguration<AssetDisposal>
{
    public void Configure(EntityTypeBuilder<AssetDisposal> builder)
    {
        builder.ToTable("AssetDisposals");
        builder.Property(d => d.DisposalNumber).IsRequired().HasMaxLength(50);
        builder.Property(d => d.BuyerName).HasMaxLength(200);
        builder.Property(d => d.Notes).HasMaxLength(1000);
        builder.Property(d => d.Proceeds).HasPrecision(18, 4);
        builder.Property(d => d.CostAtDisposal).HasPrecision(18, 4);
        builder.Property(d => d.AccumulatedAtDisposal).HasPrecision(18, 4);
        builder.Property(d => d.BookValueAtDisposal).HasPrecision(18, 4);
        builder.Property(d => d.GainOrLoss).HasPrecision(18, 4);

        builder.HasOne(d => d.FixedAsset).WithMany().HasForeignKey(d => d.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.ProceedsAccount).WithMany().HasForeignKey(d => d.ProceedsAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.JournalEntry).WithMany().HasForeignKey(d => d.JournalEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.CompanyId, d.DisposalNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AssetPhysicalCountConfiguration : IEntityTypeConfiguration<AssetPhysicalCount>
{
    public void Configure(EntityTypeBuilder<AssetPhysicalCount> builder)
    {
        builder.ToTable("AssetPhysicalCounts");
        builder.Property(c => c.CountNumber).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.HasOne(c => c.Branch).WithMany().HasForeignKey(c => c.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.CompanyId, c.CountNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AssetPhysicalCountLineConfiguration : IEntityTypeConfiguration<AssetPhysicalCountLine>
{
    public void Configure(EntityTypeBuilder<AssetPhysicalCountLine> builder)
    {
        builder.ToTable("AssetPhysicalCountLines");
        builder.Property(l => l.ExpectedLocation).HasMaxLength(200);
        builder.Property(l => l.ActualLocation).HasMaxLength(200);
        builder.Property(l => l.Notes).HasMaxLength(500);
        builder.HasOne(l => l.AssetPhysicalCount).WithMany(c => c.Lines).HasForeignKey(l => l.AssetPhysicalCountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.FixedAsset).WithMany().HasForeignKey(l => l.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.AssetPhysicalCountId, l.FixedAssetId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AssetSettingsConfiguration : IEntityTypeConfiguration<AssetSettings>
{
    public void Configure(EntityTypeBuilder<AssetSettings> builder)
    {
        builder.ToTable("AssetSettings");
        builder.Property(s => s.MaintenanceApprovalThreshold).HasPrecision(18, 4);
        builder.HasIndex(s => s.CompanyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MaintenanceCategoryConfiguration : IEntityTypeConfiguration<MaintenanceCategory>
{
    public void Configure(EntityTypeBuilder<MaintenanceCategory> builder)
    {
        builder.ToTable("MaintenanceCategories");
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MaintenanceIssueConfiguration : IEntityTypeConfiguration<MaintenanceIssue>
{
    public void Configure(EntityTypeBuilder<MaintenanceIssue> builder)
    {
        builder.ToTable("MaintenanceIssues");
        builder.Property(i => i.IssueNumber).IsRequired().HasMaxLength(50);
        builder.Property(i => i.DeviceName).HasMaxLength(200);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(2000);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.HasOne(i => i.Branch).WithMany().HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.FixedAsset).WithMany().HasForeignKey(i => i.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.CompanyId, i.IssueNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(i => new { i.CompanyId, i.Status });
    }
}

public class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> builder)
    {
        builder.ToTable("MaintenanceRequests");
        builder.Property(r => r.RequestNumber).IsRequired().HasMaxLength(50);
        builder.Property(r => r.TechnicianName).HasMaxLength(200);
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.Property(r => r.EstimatedCost).HasPrecision(18, 4);
        builder.Property(r => r.LaborCost).HasPrecision(18, 4);
        builder.Property(r => r.SparePartsTotalCost).HasPrecision(18, 4);
        builder.Property(r => r.ActualCost).HasPrecision(18, 4);

        builder.HasOne(r => r.Issue).WithMany().HasForeignKey(r => r.IssueId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.FixedAsset).WithMany().HasForeignKey(r => r.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.MaintenanceCategory).WithMany().HasForeignKey(r => r.MaintenanceCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.MaintenanceSchedule).WithMany().HasForeignKey(r => r.MaintenanceScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Technician).WithMany().HasForeignKey(r => r.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ExternalCreditAccount).WithMany().HasForeignKey(r => r.ExternalCreditAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.JournalEntry).WithMany().HasForeignKey(r => r.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.SparePartsJournalEntry).WithMany().HasForeignKey(r => r.SparePartsJournalEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.RequestNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

        // One job-raised request per schedule due date (rule 29); hand-made requests carry no key.
        builder.HasIndex(r => r.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class MaintenanceSparePartConfiguration : IEntityTypeConfiguration<MaintenanceSparePart>
{
    public void Configure(EntityTypeBuilder<MaintenanceSparePart> builder)
    {
        builder.ToTable("MaintenanceSpareParts");
        builder.Property(p => p.Description).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Quantity).HasPrecision(18, 4);
        builder.Property(p => p.UnitCost).HasPrecision(18, 6);
        builder.Property(p => p.TotalCost).HasPrecision(18, 4);
        builder.Ignore(p => p.IsStocked);

        builder.HasOne(p => p.MaintenanceRequest).WithMany(r => r.SpareParts).HasForeignKey(p => p.MaintenanceRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Item).WithMany().HasForeignKey(p => p.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Warehouse).WithMany().HasForeignKey(p => p.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.StockTransaction).WithMany().HasForeignKey(p => p.StockTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ToTable("MaintenanceSchedules");
        builder.Property(s => s.TechnicianName).HasMaxLength(200);
        builder.Property(s => s.Notes).HasMaxLength(1000);
        builder.HasOne(s => s.FixedAsset).WithMany().HasForeignKey(s => s.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.MaintenanceCategory).WithMany().HasForeignKey(s => s.MaintenanceCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Technician).WithMany().HasForeignKey(s => s.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.IsActive, s.NextDueDate });
    }
}
