using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Sales;

/// <summary>
/// إعدادات برنامج الولاء (04-Module-Sales.md, section 2.1) — screen #4. صف واحد لكل شركة، بنفس
/// نمط `PurchaseCycleSettings`/`InventorySettings`.
/// </summary>
public class LoyaltyProgramSettings : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    /// <summary>كام جنيه إنفاق = نقطة واحدة.</summary>
    public decimal PointsEarnRate { get; set; }

    /// <summary>قيمة النقطة الواحدة بالجنيه عند الاستبدال.</summary>
    public decimal PointsRedemptionValue { get; set; }
}
