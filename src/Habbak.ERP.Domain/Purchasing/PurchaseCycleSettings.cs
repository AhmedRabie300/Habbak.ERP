using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>نمط دورة الشراء (03-Module-Purchasing.md, section 2.2) — يحدد أي خطوات الدورة إلزامية
/// وأيها اختيارية أو آلية.</summary>
public enum PurchaseCycleType
{
    Full = 1,
    Direct = 2,
    OrderBased = 3,
    RequestBased = 4,
    Simplified = 5
}

/// <summary>
/// إعدادات دورة المشتريات (03-Module-Purchasing.md, section 2.3) — screen #11. صف واحد لكل
/// شركة، بنفس نمط ShortagePolicy/InventorySettings في موديول المخازن.
///
/// القسم 2.4 من المواصفة الأصلية ("إعدادات الترقيم لكل مستند" — PurchaseRequestCodingRuleId إلخ)
/// اتلغى عمدًا: النظام أصلًا عنده آلية ترقيم عامة موحّدة لكل شاشة (`CodingRule` + `ScreenCodeCatalog`،
/// نفس الآلية المستخدمة في كل شاشة "إنشاء" بالنظام) — كل مستند من مستندات الموديول ده (طلب شراء،
/// أمر شراء، فاتورة، إذن إضافة...) هيسجّل كوده الخاص في `ScreenCodeCatalog` ويتظبط من شاشة "قواعد
/// الترقيم" العامة الموجودة بالفعل، بدل ما نبني نظام ترقيم مواز خاص بالمشتريات لوحده.
/// </summary>
public class PurchaseCycleSettings : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public PurchaseCycleType CycleType { get; set; } = PurchaseCycleType.Full;

    public bool RequiresPurchaseRequest { get; set; }
    public bool RequiresQuotation { get; set; }
    public bool RequiresPurchaseOrder { get; set; } = true;
    public bool RequiresGoodsReceipt { get; set; } = true;

    public bool AllowInvoiceWithoutOrder { get; set; }
    public bool AllowReceiptWithoutInvoice { get; set; } = true;
    public bool AutoCreateReceiptOnInvoicePost { get; set; }
    public bool AutoCreateInvoiceOnReceipt { get; set; }

    public bool RequiresApprovalForPurchaseOrder { get; set; }
    public bool RequiresApprovalForInvoice { get; set; }

    public SupplierPaymentTerms DefaultPaymentTerms { get; set; } = SupplierPaymentTerms.Net30;

    /// <summary>المصروفات الإضافية (نقل، شحن، جمارك) تُخانة على تكلفة المخزون (True) أو تُسجَّل
    /// كمصروف فترة مباشر (False).</summary>
    public bool CapitalizeAdditionalCosts { get; set; } = true;

    /// <summary>
    /// الفاتورة المرتبطة بأمر شراء تقدر تاخد سطور مش من الأمر (Remarks6، قرار المستخدم). الافتراضي
    /// مسموح — ده اللي بيحصل دلوقتي، والمورد أحيانًا بيسلّم صنف زيادة أو رسوم مش في الأمر. لما
    /// يتقفل: أي سطر من غير PurchaseOrderLineId في فاتورة عليها أمر بيترفض.
    /// </summary>
    public bool AllowManualInvoiceLines { get; set; } = true;

    /// <summary>
    /// أمر الشراء المرتبط بطلب شراء يقدر ياخد سطور مش من الطلب (Remarks7، نظير
    /// AllowManualInvoiceLines). الافتراضي مسموح. لما يتقفل: أي سطر من غير PurchaseRequestLineId في
    /// أمر عليه طلب شراء بيترفض.
    /// </summary>
    public bool AllowManualOrderLines { get; set; } = true;
}
