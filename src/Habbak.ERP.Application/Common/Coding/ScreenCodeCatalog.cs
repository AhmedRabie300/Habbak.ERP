using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Application.Common.Coding;

/// <summary>Every "create new record" screen's default numbering behavior — used when no
/// company-specific CodingRule row exists yet, so existing screens keep behaving exactly as
/// before until an admin reconfigures them from the coding-rules settings screen.</summary>
public sealed record ScreenCodeDefinition(
    string ScreenCode,
    string LabelAr,
    string LabelEn,
    bool DefaultIsAutomatic,
    CodeFormat DefaultFormat,
    string? DefaultPrefix,
    int DefaultSequenceLength);

public static class ScreenCodeCatalog
{
    public static readonly ScreenCodeDefinition[] All =
    [
        new("ACCOUNTING_CHART_OF_ACCOUNTS", "دليل الحسابات", "Chart of Accounts", false, CodeFormat.LettersAndNumbers, null, 5),
        new("ACCOUNTING_DIMENSIONS", "مراكز التكلفة", "Cost Centers", false, CodeFormat.LettersAndNumbers, null, 5),
        new("ACCOUNTING_DIMENSION_VALUES", "قيم مراكز التكلفة", "Cost Center Values", false, CodeFormat.LettersAndNumbers, null, 5),
        new("ORG_BRANCHES", "الفروع", "Branches", false, CodeFormat.LettersAndNumbers, null, 5),
        new("ACCOUNTING_PAYMENT_METHODS", "طرق الدفع", "Payment Methods", false, CodeFormat.LettersAndNumbers, null, 5),
        new("ACCOUNTING_JOURNAL_ENTRIES", "القيود اليومية", "Journal Entries", true, CodeFormat.LettersAndNumbers, "JV", 5),
        new("ACCOUNTING_RECEIPT_VOUCHERS", "سندات القبض", "Receipt Vouchers", true, CodeFormat.LettersAndNumbers, "RCT", 5),
        new("ACCOUNTING_PAYMENT_VOUCHERS", "سندات الصرف", "Payment Vouchers", true, CodeFormat.LettersAndNumbers, "PAY", 5),
        new("ACCOUNTING_OPENING_BALANCES", "أرصدة افتتاحية للحسابات", "Account Opening Balances", true, CodeFormat.LettersAndNumbers, "AOB", 5),
        new("INVENTORY_UNITS_OF_MEASURE", "وحدات القياس", "Units of Measure", false, CodeFormat.LettersAndNumbers, null, 5),
        new("INVENTORY_ITEM_GROUPS", "مجموعات الأصناف", "Item Groups", false, CodeFormat.LettersAndNumbers, null, 5),
        new("INVENTORY_POS_CATEGORIES", "تصنيفات نقطة البيع", "POS Categories", false, CodeFormat.LettersAndNumbers, null, 5),
        new("INVENTORY_WAREHOUSES", "المخازن", "Warehouses", false, CodeFormat.LettersAndNumbers, null, 5),
        new("INVENTORY_ITEMS", "الأصناف", "Items", false, CodeFormat.LettersAndNumbers, null, 5),
        new("INVENTORY_STOCK_IN", "إذن إضافة", "Stock In", true, CodeFormat.LettersAndNumbers, "GRN", 5),
        new("INVENTORY_OPENING_BALANCES", "أرصدة افتتاحية", "Opening Balances", true, CodeFormat.LettersAndNumbers, "OPB", 5),
        new("INVENTORY_STOCK_OUT", "إذن صرف", "Stock Out", true, CodeFormat.LettersAndNumbers, "ISS", 5),
        new("INVENTORY_CUSTODY_OFFICERS", "مسؤولو العهد", "Custody Officers", false, CodeFormat.LettersAndNumbers, null, 5),
        new("INVENTORY_TRANSFER_ORDER", "أمر تحويل", "Transfer Order", true, CodeFormat.LettersAndNumbers, "TRF", 5),
        new("INVENTORY_TRANSFER_RECEIPT", "استلام تحويل", "Transfer Receipt", true, CodeFormat.LettersAndNumbers, "TRC", 5),
        new("INVENTORY_BRANCH_REQUEST", "طلب توريد فرع", "Branch Request", true, CodeFormat.LettersAndNumbers, "BRQ", 5),
        new("INVENTORY_RECIPE", "وصفة", "Recipe", true, CodeFormat.LettersAndNumbers, "REC", 5),
        new("INVENTORY_PRODUCTION_ORDER", "أمر إنتاج", "Production Order", true, CodeFormat.LettersAndNumbers, "PRD", 5),
        new("INVENTORY_PRODUCTION_ISSUE", "صرف إنتاج", "Production Issue", true, CodeFormat.LettersAndNumbers, "PIS", 5),
        new("INVENTORY_PRODUCTION_RECEIPT", "استلام إنتاج", "Production Receipt", true, CodeFormat.LettersAndNumbers, "PRC", 5),
        new("INVENTORY_COUNT", "عملية جرد", "Inventory Count", true, CodeFormat.LettersAndNumbers, "CNT", 5),
        new("INVENTORY_ADJUSTMENT", "تسوية جرد مستقلة", "Inventory Adjustment", true, CodeFormat.LettersAndNumbers, "ADJ", 5),
        new("PURCHASING_SUPPLIERS", "الموردون", "Suppliers", false, CodeFormat.LettersAndNumbers, null, 5),
        new("PURCHASING_PURCHASE_REQUEST", "طلب شراء", "Purchase Request", true, CodeFormat.LettersAndNumbers, "PR", 5),
        new("PURCHASING_PURCHASE_ORDER", "أمر شراء", "Purchase Order", true, CodeFormat.LettersAndNumbers, "PO", 5),
        new("PURCHASING_GOODS_RECEIPT", "إذن إضافة", "Goods Receipt", true, CodeFormat.LettersAndNumbers, "GR", 5),
        new("PURCHASING_PURCHASE_INVOICE", "فاتورة شراء", "Purchase Invoice", true, CodeFormat.LettersAndNumbers, "PINV", 5),
        new("PURCHASING_PURCHASE_RETURN", "مردود مشتريات", "Purchase Return", true, CodeFormat.LettersAndNumbers, "PRET", 5),
        new("PURCHASING_SUPPLIER_CONTRACT", "عقد مورد", "Supplier Contract", true, CodeFormat.LettersAndNumbers, "SCON", 5),
        new("PURCHASING_RFQ", "طلب عروض أسعار", "Request for Quotation", true, CodeFormat.LettersAndNumbers, "RFQ", 5),
        new("SALES_CUSTOMERS", "العملاء", "Customers", false, CodeFormat.LettersAndNumbers, null, 5),
        new("SALES_LOYALTY_TIERS", "شرائح الولاء", "Loyalty Tiers", false, CodeFormat.LettersAndNumbers, null, 5),
        new("SALES_PRICE_LISTS", "قوائم الأسعار", "Price Lists", false, CodeFormat.LettersAndNumbers, null, 5),
        new("SALES_DISCOUNTS", "الخصومات", "Discounts", false, CodeFormat.LettersAndNumbers, null, 5),
        new("SALES_QUOTE", "عرض سعر", "Sales Quote", true, CodeFormat.LettersAndNumbers, "QT", 5),
        new("SALES_ORDER", "أمر بيع", "Sales Order", true, CodeFormat.LettersAndNumbers, "SO", 5),
        new("SALES_INVOICE", "فاتورة مبيعات", "Sales Invoice", true, CodeFormat.LettersAndNumbers, "SINV", 5),
        new("SALES_DELIVERY_ORDER", "أمر تسليم", "Delivery Order", true, CodeFormat.LettersAndNumbers, "DO", 5),
        new("SALES_RETURN", "مرتجع مبيعات", "Sales Return", true, CodeFormat.LettersAndNumbers, "SRET", 5),
        new("POS_TERMINALS", "نقاط البيع", "POS Terminals", false, CodeFormat.LettersAndNumbers, null, 5),
        new("POS_TABLES", "الطرابيزات", "Tables", false, CodeFormat.LettersAndNumbers, null, 5),
        new("POS_CHECKS", "شيك", "Check", true, CodeFormat.LettersAndNumbers, "CHK", 5),
        new("POS_INVOICES", "فاتورة نقطة بيع", "POS Invoice", true, CodeFormat.LettersAndNumbers, "POSI", 5),
        new("POS_RETURNS", "مرتجع نقطة بيع", "POS Return", true, CodeFormat.LettersAndNumbers, "POSR", 5),
        new("POS_BLEND_TYPES", "أنواع خلطات البن", "Blend Types", false, CodeFormat.LettersAndNumbers, null, 5),

        // Fixed assets & maintenance (08-Module-Maintenance-FixedAssets).
        new("FIXED_ASSETS_CATEGORIES", "فئات الأصول", "Asset Categories", false, CodeFormat.LettersAndNumbers, null, 5),
        new("FIXED_ASSETS", "الأصول الثابتة", "Fixed Assets", true, CodeFormat.LettersAndNumbers, "FA", 5),
        new("FIXED_ASSETS_DEPRECIATION_RUNS", "تشغيل الإهلاك", "Depreciation Runs", true, CodeFormat.LettersAndNumbers, "DEP", 5),
        new("FIXED_ASSETS_TRANSFERS", "نقل الأصول", "Asset Transfers", true, CodeFormat.LettersAndNumbers, "FAT", 5),
        new("FIXED_ASSETS_DISPOSALS", "استبعاد الأصول", "Asset Disposals", true, CodeFormat.LettersAndNumbers, "FAD", 5),
        new("FIXED_ASSETS_PHYSICAL_COUNTS", "جرد الأصول", "Asset Counts", true, CodeFormat.LettersAndNumbers, "FAC", 5),
        new("MAINTENANCE_CATEGORIES", "فئات الصيانة", "Maintenance Categories", false, CodeFormat.LettersAndNumbers, null, 5),
        new("MAINTENANCE_ISSUES", "بلاغات الأعطال", "Fault Reports", true, CodeFormat.LettersAndNumbers, "ISS", 5),
        new("MAINTENANCE_REQUESTS", "طلبات الصيانة", "Maintenance Requests", true, CodeFormat.LettersAndNumbers, "MRQ", 5),

        // HR Core (Docs/Implementation/HR-Core-Plan.md §1.1) — lookups manual like every other lookup
        // screen above (Suppliers, ItemGroups, Warehouses...); Employee manual too, since HR typically
        // assigns a meaningful employee number rather than a bare sequence.
        new("SETTINGS_COUNTRIES", "الدول", "Countries", false, CodeFormat.LettersAndNumbers, null, 5),
        new("SETTINGS_CITIES", "المدن", "Cities", false, CodeFormat.LettersAndNumbers, null, 5),
        new("SETTINGS_BANKS", "البنوك", "Banks", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_JOB_GRADES", "الدرجات الوظيفية", "Job Grades", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_JOB_POSITIONS", "الوظائف", "Job Positions", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_ORG_UNITS", "الهيكل التنظيمي", "Org Units", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_DOCUMENT_TYPES", "أنواع المستندات", "Document Types", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_INSURANCE_OFFICES", "مكاتب التأمينات", "Insurance Offices", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_EMPLOYEES", "الموظفين", "Employees", false, CodeFormat.LettersAndNumbers, null, 5),

        // Attendance & Leave (Docs/Implementation/HR-MASTER-PLAN.md §Phase 3) — نفس نمط باقي
        // Lookups: يدوي افتراضيًا.
        new("HR_WORK_SHIFTS", "ورديات العمل", "Work Shifts", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_LEAVE_TYPES", "أنواع الإجازات", "Leave Types", false, CodeFormat.LettersAndNumbers, null, 5),
        new("HR_HOLIDAYS", "العطلات", "Holidays", false, CodeFormat.LettersAndNumbers, null, 5)
    ];

    public static ScreenCodeDefinition? Find(string screenCode) => All.FirstOrDefault(s => s.ScreenCode == screenCode);
}
