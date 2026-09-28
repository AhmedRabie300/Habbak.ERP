using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.FixedAssets;

/// <summary>
/// فئة أصل (section 2.1.1) — the defaults a new asset starts from and, above all, the accounts every
/// entry of its assets posts to: acquisition, depreciation, disposal and maintenance.
/// </summary>
public class FixedAssetCategory : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;

    /// <summary>Annual percentage for DecliningBalance — required with that method, otherwise unused.</summary>
    public decimal? DefaultDepreciationRate { get; set; }

    public int? DefaultUsefulLifeYears { get; set; }

    /// <summary>Salvage value as a percentage of cost.</summary>
    public decimal? DefaultSalvagePercentage { get; set; }

    public long AssetAccountId { get; set; }
    public Account? AssetAccount { get; set; }
    public long AccumulatedDepreciationAccountId { get; set; }
    public Account? AccumulatedDepreciationAccount { get; set; }
    public long DepreciationExpenseAccountId { get; set; }
    public Account? DepreciationExpenseAccount { get; set; }
    public long? DisposalGainAccountId { get; set; }
    public Account? DisposalGainAccount { get; set; }
    public long? DisposalLossAccountId { get; set; }
    public Account? DisposalLossAccount { get; set; }
    public long MaintenanceExpenseAccountId { get; set; }
    public Account? MaintenanceExpenseAccount { get; set; }

    public bool IsActive { get; set; } = true;
}
