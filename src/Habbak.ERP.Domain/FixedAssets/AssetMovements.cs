using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;

namespace Habbak.ERP.Domain.FixedAssets;

/// <summary>
/// Moving an asset to another branch (section 2.1.5). The officer carrying it is the Inventory
/// module's CustodyOfficer — a real key, the same person/record the stock transfers use — with the
/// name kept as it was on the day. Posting changes the asset's branch only: its depreciation
/// schedule carries on untouched (rule 33).
/// </summary>
public class AssetTransfer : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string TransferNumber { get; set; } = null!;
    public long FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public long? FromBranchId { get; set; }
    public Branch? FromBranch { get; set; }
    public long ToBranchId { get; set; }
    public Branch? ToBranch { get; set; }
    public DateOnly TransferDate { get; set; }
    public string? Reason { get; set; }

    public long CustodyOfficerId { get; set; }
    public CustodyOfficer? CustodyOfficer { get; set; }
    public string CustodyOfficerNameSnapshot { get; set; } = null!;

    public AssetTransferStatus Status { get; set; } = AssetTransferStatus.Draft;
    public DateTime? PostedAtUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Disposing of an asset (section 2.1.6): sold, scrapped or lost. Posting takes the cost and the
/// depreciation so far off the books, books the gain or loss, and cancels the periods not yet
/// depreciated (rule 32). The figures are frozen at posting.
/// </summary>
public class AssetDisposal : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string DisposalNumber { get; set; } = null!;
    public long FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public DateOnly DisposalDate { get; set; }
    public DisposalType DisposalType { get; set; }

    public decimal? Proceeds { get; set; }

    /// <summary>Where the proceeds went — a treasury or the buyer's receivable. Required when there are proceeds.</summary>
    public long? ProceedsAccountId { get; set; }
    public Account? ProceedsAccount { get; set; }
    public string? BuyerName { get; set; }

    // Frozen when posted (base currency).
    public decimal CostAtDisposal { get; set; }
    public decimal AccumulatedAtDisposal { get; set; }
    public decimal BookValueAtDisposal { get; set; }

    /// <summary>Proceeds − book value: positive is a gain, negative a loss.</summary>
    public decimal GainOrLoss { get; set; }

    public AssetDisposalStatus Status { get; set; } = AssetDisposalStatus.Draft;
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A physical check of the assets of a branch (sections 2.1.7-2.1.8).</summary>
public class AssetPhysicalCount : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string CountNumber { get; set; } = null!;
    public DateOnly CountDate { get; set; }
    public AssetPhysicalCountStatus Status { get; set; } = AssetPhysicalCountStatus.Draft;
    public string? Notes { get; set; }

    public ICollection<AssetPhysicalCountLine> Lines { get; set; } = new List<AssetPhysicalCountLine>();
}

public class AssetPhysicalCountLine : AuditableEntity
{
    public long AssetPhysicalCountId { get; set; }
    public AssetPhysicalCount? AssetPhysicalCount { get; set; }
    public long FixedAssetId { get; set; }
    public FixedAsset? FixedAsset { get; set; }
    public string? ExpectedLocation { get; set; }
    public string? ActualLocation { get; set; }

    /// <summary>Null until the line is counted.</summary>
    public bool? IsFound { get; set; }
    public AssetCondition? Condition { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Asset and maintenance settings of a company (section 2.3.1).</summary>
public class AssetSettings : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    /// <summary>The daily job posts the month's depreciation run on <see cref="DepreciationRunDay"/>.</summary>
    public bool AutoDepreciationEnabled { get; set; }
    public int DepreciationRunDay { get; set; } = 28;

    /// <summary>Posting a disposal / transfer then needs the Approve permission (Edit is enough otherwise).</summary>
    public bool RequireApprovalForDisposal { get; set; } = true;
    public bool RequireApprovalForTransfer { get; set; }

    /// <summary>A maintenance request above this estimated cost must be approved before work starts (rule 15).</summary>
    public decimal? MaintenanceApprovalThreshold { get; set; }

    /// <summary>How often assets should be counted — the count screen shows when the next one is due.</summary>
    public AssetPhysicalCountFrequency? PhysicalCountFrequency { get; set; }

    public bool DefaultFirstMonthProrated { get; set; } = true;
}
