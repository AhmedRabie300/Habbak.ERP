using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// رصيد الصنف بالمخزن — كيان حساس للتزامن، بيتحدّث من مصادر متعددة في نفس اللحظة
/// (02-Module-Inventory-Manufacturing.md, section 2.2). RowVersion (من AuditableEntity) هو أهم
/// استخدام لـ Optimistic Concurrency في النظام كله عمليًا.
/// </summary>
public class StockBalance : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    /// <summary>لا يُقبل بالسالب (قاعدة 1) — مُخزَّن دايمًا بوحدة Item.BaseUnitOfMeasureId (قاعدة 28).</summary>
    public decimal QuantityOnHand { get; set; }

    /// <summary>
    /// The live weighted-average cost of this item **in this warehouse** (rule 39), recalculated on
    /// every inbound movement inside the same transaction that moves the quantity. This — not
    /// <see cref="Item.StandardCost"/> — is the single source of actual cost: COGS, inventory
    /// valuation, waste value and transfer cost all read it.
    ///
    /// Per warehouse rather than per item (rules 40 and 21): stock bought cheaply into the main
    /// warehouse and stock bought expensively into a branch are genuinely worth different amounts,
    /// and a transfer has to carry the source warehouse's cost rather than re-derive one.
    /// </summary>
    public decimal AverageCost { get; set; }

    public DateTime? LastCostUpdateAtUtc { get; set; }
}
