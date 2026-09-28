using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.FixedAssets;

/// <summary>Screen (MenuItem / numbering / posting) codes of the module.</summary>
public static class FixedAssetScreens
{
    public const string Categories = "FIXED_ASSETS_CATEGORIES";
    public const string Assets = "FIXED_ASSETS";
    public const string Schedule = "FIXED_ASSETS_SCHEDULE";
    public const string DepreciationRuns = "FIXED_ASSETS_DEPRECIATION_RUNS";
    public const string Transfers = "FIXED_ASSETS_TRANSFERS";
    public const string Disposals = "FIXED_ASSETS_DISPOSALS";
    public const string PhysicalCounts = "FIXED_ASSETS_PHYSICAL_COUNTS";
    public const string Settings = "FIXED_ASSETS_SETTINGS";
    public const string MaintenanceCategories = "MAINTENANCE_CATEGORIES";
    public const string MaintenanceIssues = "MAINTENANCE_ISSUES";
    public const string MaintenanceRequests = "MAINTENANCE_REQUESTS";
    public const string MaintenanceSchedules = "MAINTENANCE_SCHEDULES";
    public const string MaintenanceBoard = "MAINTENANCE_BOARD";
}

internal static class FixedAssetRules
{
    public static async Task<AssetSettings> SettingsAsync(IApplicationDbContext db, long companyId, CancellationToken ct) =>
        await db.AssetSettingsRows.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct) ?? new AssetSettings { CompanyId = companyId };

    public static async Task<FixedAsset> FindAssetAsync(IApplicationDbContext db, long id, CancellationToken ct) =>
        await db.FixedAssets.Include(a => a.Category).FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException(nameof(FixedAsset), id);

    /// <summary>
    /// The cost center every entry of the asset carries (rule 27): its own value, else its branch's
    /// value in the company's branch-linked dimension. Asked only when the screen actually posts —
    /// a company that has not set up posting for the module needs no cost centers either.
    /// </summary>
    public static async Task<IReadOnlyDictionary<long, long>> CostCenterAsync(IApplicationDbContext db, FixedAsset asset, CancellationToken ct)
    {
        if (asset.CostCenterValueId is { } valueId)
        {
            var dimensionId = await db.CostCenterDimensionValues.Where(v => v.Id == valueId).Select(v => (long?)v.CostCenterDimensionId).FirstOrDefaultAsync(ct)
                              ?? throw new NotFoundException(nameof(CostCenterDimensionValue), valueId);
            return new Dictionary<long, long> { [dimensionId] = valueId };
        }

        if (asset.BranchId is { } branchId)
        {
            var dimension = await db.CostCenterDimensions.FirstOrDefaultAsync(
                d => d.LinkedEntityType == CostCenterLinkedEntityType.Branch && d.IsActive, ct);
            if (dimension is not null && await PostingEntityValueMapper.MapAsync(db, dimension, branchId, ct) is { } branchValueId)
            {
                return new Dictionary<long, long> { [dimension.Id] = branchValueId };
            }
        }

        throw new BusinessRuleException(
            "FA-COSTCENTER-REQUIRED",
            $"الأصل {asset.AssetNumber} مالوش مركز تكلفة، ولا فرعه مربوط بقيمة في بُعد الفروع — كل قيود الأصول لازم تحمل مركز تكلفة.");
    }

    /// <summary>
    /// Posting a transfer or a disposal needs the Approve permission when the settings ask for an
    /// approval, and Edit otherwise — the endpoint itself only demands Edit.
    /// </summary>
    public static async Task EnsureApproverAsync(IUserAccessService access, bool approvalRequired, string screenCode, CancellationToken ct)
    {
        if (!approvalRequired)
        {
            return;
        }

        var rights = await access.GetCurrentAsync(ct);
        if (!rights.Can(ScreenAction.Approve, [screenCode]))
        {
            throw new ForbiddenException("FA-APPROVAL-REQUIRED", "الإعدادات بتطلب اعتماد للعملية دي — محتاج صلاحية الاعتماد على الشاشة.");
        }
    }
}

/// <summary>
/// Lays out an asset's monthly depreciation (sections 2.1.3, rules 3-7, 26). Every figure is in the
/// base currency and rounded to 2 decimals; the last period takes whatever rounding left so the
/// total is exactly cost − salvage.
///
/// StraightLine: (cost − salvage) ÷ months. DecliningBalance: the remaining book value × annual
/// rate ÷ 12, never below salvage, the last period bringing it to salvage at the end of the life.
/// A prorated first month counts the days left in it (start day included); its missing part is an
/// extra period at the end.
/// </summary>
public static class DepreciationCalculator
{
    public sealed record Period(int Number, DateOnly Start, DateOnly End, decimal Amount, decimal AccumulatedAfter, decimal BookValueAfter);

    public static IReadOnlyList<Period> Build(
        decimal cost, decimal salvage, DepreciationMethod method, int? usefulLifeYears, decimal? annualRate, DateOnly start, bool prorated)
    {
        if (method == DepreciationMethod.NoDepreciation || usefulLifeYears is not > 0 || cost - salvage <= 0)
        {
            return [];
        }

        var months = usefulLifeYears.Value * 12;
        var daysInFirst = DateTime.DaysInMonth(start.Year, start.Month);
        var firstFraction = prorated && start.Day > 1 ? (decimal)(daysInFirst - start.Day + 1) / daysInFirst : 1m;
        var periodCount = firstFraction < 1m ? months + 1 : months;
        var depreciable = cost - salvage;

        var periods = new List<Period>(periodCount);
        var accumulated = 0m;
        for (var n = 1; n <= periodCount; n++)
        {
            var periodStart = n == 1 ? start : new DateOnly(start.Year, start.Month, 1).AddMonths(n - 1);
            var periodEnd = new DateOnly(periodStart.Year, periodStart.Month, DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
            var fraction = n == 1 ? firstFraction : 1m;
            var bookValue = cost - accumulated;

            decimal amount;
            if (n == periodCount)
            {
                amount = depreciable - accumulated;
            }
            else if (method == DepreciationMethod.StraightLine)
            {
                amount = Math.Round(depreciable / months * fraction, 2);
            }
            else
            {
                amount = Math.Round(bookValue * (annualRate ?? 0m) / 100m / 12m * fraction, 2);
            }

            amount = Math.Max(0m, Math.Min(amount, depreciable - accumulated));
            accumulated += amount;
            periods.Add(new Period(n, periodStart, periodEnd, amount, accumulated, cost - accumulated));
        }

        return periods;
    }
}

/// <summary>What every new company starts with (section 6.1, item 6).</summary>
public static class FixedAssetDefaults
{
    public static readonly (string Code, string NameAr, string NameEn, MaintenanceType Type)[] MaintenanceCategories =
    [
        ("PREV", "صيانة وقائية", "Preventive maintenance", MaintenanceType.Preventive),
        ("CORR", "إصلاح أعطال", "Corrective repair", MaintenanceType.Corrective),
        ("INSP", "فحص دوري", "Inspection", MaintenanceType.Inspection)
    ];

    /// <summary>
    /// Asset settings and the maintenance categories. Asset categories are not seeded: each needs
    /// its accounts, and mapping accounts is the finance manager's decision.
    /// </summary>
    public static void Add(IApplicationDbContext db, long companyId)
    {
        db.AssetSettingsRows.Add(new AssetSettings { CompanyId = companyId });
        foreach (var (code, ar, en, type) in MaintenanceCategories)
        {
            db.MaintenanceCategories.Add(new MaintenanceCategory { CompanyId = companyId, Code = code, NameAr = ar, NameEn = en, MaintenanceType = type });
        }
    }
}
