using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// تحويل وحدات القياس لكل صنف — يسمح بإدخال/شراء الصنف بوحدة مختلفة عن وحدته الأساسية
/// (02-Module-Inventory-Manufacturing.md, section 2.1، قاعدة 32).
/// </summary>
public class ItemUnitConversion : AuditableEntity
{
    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public long AlternateUnitOfMeasureId { get; set; }
    public UnitOfMeasure AlternateUnitOfMeasure { get; set; } = null!;

    /// <summary>عدد الوحدات الأساسية في الوحدة البديلة الواحدة — مثال: 50 يعني شكارة واحدة = 50 كجم
    /// (قاعدة 32).</summary>
    public decimal ConversionFactor { get; set; }
}
