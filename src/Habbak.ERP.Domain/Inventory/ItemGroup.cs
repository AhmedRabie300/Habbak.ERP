using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>مجموعة الأصناف — هرمية (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
public class ItemGroup : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public long? ParentId { get; set; }
    public ItemGroup? Parent { get; set; }
    public bool IsActive { get; set; } = true;
}
