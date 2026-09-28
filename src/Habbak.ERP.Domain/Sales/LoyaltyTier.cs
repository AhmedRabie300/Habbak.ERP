using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Sales;

/// <summary>شريحة ولاء (04-Module-Sales.md, section 2.1) — screen #4. مثال: عادي، فضي، ذهبي، VIP.</summary>
public class LoyaltyTier : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public int DisplayOrder { get; set; }

    /// <summary>حد النقاط التراكمية للدخول في الشريحة.</summary>
    public decimal MinPointsThreshold { get; set; }

    public decimal EarnRateMultiplier { get; set; } = 1.0m;

    public bool IsActive { get; set; } = true;
}
