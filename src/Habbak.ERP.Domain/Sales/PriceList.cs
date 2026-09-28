using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;

namespace Habbak.ERP.Domain.Sales;

/// <summary>
/// قائمة أسعار (04-Module-Sales.md, section 2.2) — screen #2، شاشة تفاعلية (Multi-select فروع +
/// شبكة أصناف). القائمة الواحدة ممكن تُفعَّل على أكتر من فرع معًا (قاعدة 5)، وكل بند فيها يحمل
/// 3 أعمدة سعر مستقلة على نفس السطر — مش 3 قوائم منفصلة (قاعدة 4).
/// </summary>
public class PriceList : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public DateOnly EffectiveFromDate { get; set; }
    public DateOnly? EffectiveToDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PriceListBranch> Branches { get; set; } = new List<PriceListBranch>();
    public ICollection<PriceListLine> Lines { get; set; } = new List<PriceListLine>();
}

/// <summary>ربط متعدد (Multi-select) بين قائمة الأسعار والفروع (قاعدة 5).</summary>
public class PriceListBranch : AuditableEntity
{
    public long PriceListId { get; set; }
    public PriceList? PriceList { get; set; }

    public long BranchId { get; set; }
    public Branch? Branch { get; set; }
}

/// <summary>بند قائمة الأسعار — 3 أعمدة سعر مستقلة على نفس السطر (قاعدة 4).</summary>
public class PriceListLine : AuditableEntity
{
    public long PriceListId { get; set; }
    public PriceList? PriceList { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal DineInPrice { get; set; }
    public decimal TakeawayPrice { get; set; }
    public decimal DeliveryPrice { get; set; }
}
