using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>وحدة القياس (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
public class UnitOfMeasure : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    /// <summary>Informational grouping only — the actual conversion between two units stays
    /// per-item on ItemUnitConversion (rule 32), since the same alternate unit (e.g. "carton")
    /// can mean a different quantity for different items.</summary>
    public UnitCategory Category { get; set; } = UnitCategory.Count;

    public bool IsActive { get; set; } = true;
}
