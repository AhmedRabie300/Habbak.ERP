using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Purchasing;

namespace Habbak.ERP.Domain.FixedAssets;

/// <summary>
/// أصل ثابت (section 2.1.2). Money is kept twice: the acquisition cost as bought (CurrencyCode /
/// ExchangeRate) and its base-currency equivalent, which is what is posted and depreciated (rule 30).
/// SalvageValue and AccumulatedDepreciation are in the base currency too.
/// </summary>
public class FixedAsset : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string AssetNumber { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public long CategoryId { get; set; }
    public FixedAssetCategory? Category { get; set; }

    public string? SerialNumber { get; set; }
    public string? Barcode { get; set; }

    /// <summary>Where the asset is kept — what a physical count expects to find it at.</summary>
    public string? Location { get; set; }

    // ------------------------------------------------------------------ acquisition

    public DateOnly AcquisitionDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1;

    /// <summary>AcquisitionCost × ExchangeRate — the cost in the company's base currency.</summary>
    public decimal BaseCurrencyAmount { get; set; }

    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public long? PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    /// <summary>
    /// The account the acquisition is credited to — the supplier's payable, a treasury, or a clearing
    /// account when the purchase invoice already carried it. Read by the acquisition template.
    /// </summary>
    public long? FundingAccountId { get; set; }
    public Account? FundingAccount { get; set; }

    public long? AcquisitionJournalEntryId { get; set; }
    public JournalEntry? AcquisitionJournalEntry { get; set; }

    // ------------------------------------------------------------------ depreciation

    public int? UsefulLifeYears { get; set; }
    public decimal SalvageValue { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }

    /// <summary>Annual percentage, DecliningBalance only (rule 4).</summary>
    public decimal? DepreciationRate { get; set; }

    public DateOnly DepreciationStartDate { get; set; }

    /// <summary>First month by days remaining in it (rule 26); otherwise a full month whatever the start day.</summary>
    public bool FirstMonthProrated { get; set; }

    /// <summary>Only ever changed by posting or reversing a depreciation run (rule 25).</summary>
    public decimal AccumulatedDepreciation { get; set; }

    public decimal NetBookValue => BaseCurrencyAmount - AccumulatedDepreciation;

    // ------------------------------------------------------------------ status

    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Draft;
    public DateOnly? DisposalDate { get; set; }
    public string? DisposalReason { get; set; }
    public decimal? DisposalProceeds { get; set; }
    public long? DisposalJournalEntryId { get; set; }
    public JournalEntry? DisposalJournalEntry { get; set; }

    /// <summary>The officer answerable for the asset — the Inventory module's CustodyOfficer (section 1.3).</summary>
    public long? CustodyOfficerId { get; set; }
    public CustodyOfficer? CustodyOfficer { get; set; }
    public string? CustodyOfficerName { get; set; }

    /// <summary>
    /// A cost center value every entry of this asset carries (rule 27). Without one, the asset's
    /// branch value in the branch-linked dimension is used.
    /// </summary>
    public long? CostCenterValueId { get; set; }
    public CostCenterDimensionValue? CostCenterValue { get; set; }

    public string? Notes { get; set; }

    public ICollection<DepreciationSchedule> Schedule { get; set; } = new List<DepreciationSchedule>();
}
