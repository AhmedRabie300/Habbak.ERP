using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>الصنف (02-Module-Inventory-Manufacturing.md, section 2.1). Fields beyond the module
/// doc's own field table were added to match the reference mockup's item card
/// (Coffee_ERP_Full_System_Mockup.html, FORM_SCHEMAS.items) at the user's explicit request.</summary>
public class Item : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    /// <summary>تصنيف المخزون الداخلي.</summary>
    public long? ItemGroupId { get; set; }
    public ItemGroup? ItemGroup { get; set; }

    /// <summary>تصنيف تبويب الكاشير — null للأصناف غير المباعة مباشرة (خامات، قاعدة 38).</summary>
    public long? POSCategoryId { get; set; }
    public POSCategory? POSCategory { get; set; }

    public ItemType ItemType { get; set; }

    public string? Barcode { get; set; }

    /// <summary>وحدة القياس الأساسية — كل الكميات في StockBalance/StockTransaction مُخزَّنة بهذه
    /// الوحدة حصريًا (قاعدة 28).</summary>
    /// <summary>
    /// The item's code with the Egyptian Tax Authority (GS1 or EGS) — sent with e-invoices and
    /// e-receipts once the ETA module exists; optional until then (Remarks3, item 2).
    /// </summary>
    public string? TaxCode { get; set; }

    public long BaseUnitOfMeasureId { get; set; }
    public UnitOfMeasure BaseUnitOfMeasure { get; set; } = null!;

    /// <summary>الوحدة المستخدمة افتراضيًا عند الشراء — null يعني نفس الوحدة الأساسية.</summary>
    public long? PurchaseUnitOfMeasureId { get; set; }
    public UnitOfMeasure? PurchaseUnitOfMeasure { get; set; }

    /// <summary>الوحدة المستخدمة افتراضيًا عند البيع — null يعني نفس الوحدة الأساسية.</summary>
    public long? SellUnitOfMeasureId { get; set; }
    public UnitOfMeasure? SellUnitOfMeasure { get; set; }

    /// <summary>يتطلب تكامل الميزان وقت البيع لو ByWeight (00-Project-Overview.md, قسم 5.1/5.4).</summary>
    public SaleMethod SaleMethod { get; set; } = SaleMethod.ByPiece;

    public CostMethod CostMethod { get; set; } = CostMethod.WeightedAverage;

    /// <summary>سعر البيع الافتراضي — قوائم الأسعار (04-Module-Sales.md) تقدر تخصّصه لكل قناة.</summary>
    public decimal? DefaultPrice { get; set; }

    /// <summary>صنف غير مخزني (مثال: خدمة توصيل) لا يظهر في تقارير المخزون أو الجرد.</summary>
    public bool IsStocked { get; set; } = true;

    /// <summary>يتطلب رقم دفعة (BatchNumber) إلزاميًا على أي حركة (قاعدة 8).</summary>
    public bool IsTracked { get; set; }

    public bool TrackSerial { get; set; }

    /// <summary>عمر الصلاحية بالأيام من تاريخ الإنتاج/الاستلام.</summary>
    public int? ShelfLifeDays { get; set; }

    /// <summary>التكلفة المعيارية — بعملة/وحدة BaseUnitOfMeasureId دايمًا.</summary>
    public decimal? StandardCost { get; set; }

    public bool IsPurchasable { get; set; } = true;
    public bool IsSellable { get; set; } = true;

    /// <summary>له وصفة إنتاج — يُربط فعليًا من شاشة "تعريف وصفة" بعد الحفظ (section 2.6، لسه غير مبني).</summary>
    public bool IsManufacturable { get; set; }
    public bool AllowSubstitutes { get; set; }

    public ItemStatus Status { get; set; } = ItemStatus.Active;

    public bool IsActive { get; set; } = true;

    public ICollection<ItemUnitConversion> UnitConversions { get; set; } = new List<ItemUnitConversion>();
    public ICollection<ItemWarehouseSettings> WarehouseSettings { get; set; } = new List<ItemWarehouseSettings>();

    /// <summary>حدود حجم طلب التوريد لكل فرع (section 2.4) — منفصلة تمامًا عن WarehouseSettings
    /// (حدود المخزون الفعلي، قاعدة 29).</summary>
    public ICollection<BranchItemLimit> BranchItemLimits { get; set; } = new List<BranchItemLimit>();
}
