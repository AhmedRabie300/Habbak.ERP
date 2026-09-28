using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// تصنيف نقطة البيع — منفصل تمامًا عن ItemGroup: ده لتبويبات شاشة الكاشير فقط
/// (02-Module-Inventory-Manufacturing.md, section 2.1).
/// </summary>
public class POSCategory : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
