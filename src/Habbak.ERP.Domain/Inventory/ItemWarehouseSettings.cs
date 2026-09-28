using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// إعدادات الصنف لكل مخزن — الحد الأدنى/الأقصى مختلف لكل مخزن، مش قيمة عامة واحدة للصنف
/// (02-Module-Inventory-Manufacturing.md, section 2.1، قاعدة 21).
/// </summary>
public class ItemWarehouseSettings : AuditableEntity
{
    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public decimal? MinStockLevel { get; set; }
    public decimal? MaxStockLevel { get; set; }
    public decimal? ReorderPoint { get; set; }
}
