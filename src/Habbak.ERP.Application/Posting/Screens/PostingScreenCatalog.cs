using Habbak.ERP.Application.Posting.Templates;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Posting;
using Habbak.ERP.Shared.Enums;

namespace Habbak.ERP.Application.Posting.Screens;

public enum PostingFieldKind
{
    /// <summary>A money figure a line can post or test.</summary>
    Amount = 1,

    /// <summary>A record id — a supplier, a branch — that resolvers and cost centers read.</summary>
    Id = 2,

    /// <summary>An account id carried on the document itself (a drawer expense's expense account).</summary>
    Account = 3,

    /// <summary>A coded value (a sale's PaymentType) that a trigger or condition can compare.</summary>
    Text = 4
}

/// <summary>
/// A field a screen's templates may read. <paramref name="EntityType"/> says what an Id field points
/// at, which is what lets a cost center be taken from it. <paramref name="IsContext"/> marks a value
/// the source module supplies alongside the document rather than a field of the document itself —
/// a POS entry's cashier comes from the shift, not the invoice (design notes, note 3).
/// </summary>
public sealed record PostingScreenField(
    string Name, string LabelAr, string LabelEn, PostingFieldKind Kind, decimal? Sample = null,
    CostCenterLinkedEntityType EntityType = CostCenterLinkedEntityType.None, bool IsContext = false,
    IReadOnlyList<string>? Choices = null);

public sealed record PostingScreenGroup(string Name, string LabelAr, string LabelEn, decimal Sample);

/// <summary>
/// One screen that posts: what its templates may read (the design notes' ScreenFieldCatalog), and the
/// starting templates offered to the finance manager. The documents build their context from exactly
/// these names, so the list the editor shows is the list the document supplies.
/// </summary>
public sealed record PostingScreenDefinition(
    string ScreenCode,
    string NameAr,
    string NameEn,
    SourceModule SourceModule,
    string WhenAr,
    bool CanMoveStock,
    IReadOnlyList<PostingScreenField> Fields,
    IReadOnlyList<PostingScreenGroup> Groups,
    IReadOnlyList<PostingTemplateDefinition> DefaultTemplates)
{
    /// <summary>Related records reachable from this screen's fields (the shift of a POS entry, a warehouse).</summary>
    public IReadOnlyList<RelatedEntityDefinition> RelatedEntities =>
        RelatedEntityCatalog.All.Where(r => Fields.Any(f => string.Equals(f.Name, r.IdField, StringComparison.OrdinalIgnoreCase))).ToList();
}

public static class PostingScreenCatalog
{
    public const string PurchaseInvoice = "PURCHASING_PURCHASE_INVOICE";
    public const string PurchaseReturn = "PURCHASING_PURCHASE_RETURN";
    public const string SalesInvoice = "SALES_INVOICE";
    public const string DeliveryOrder = "SALES_DELIVERY_ORDER";
    public const string SalesReturn = "SALES_RETURN";
    public const string PosInvoice = "POS_INVOICES";
    public const string PosReturn = "POS_RETURNS";
    public const string PosShiftVariance = "POS_SHIFT_VARIANCE";
    public const string PosDrawerExpense = "POS_DRAWER_EXPENSE";
    public const string StockIn = "INVENTORY_STOCK_IN";
    public const string StockOut = "INVENTORY_STOCK_OUT";
    public const string OpeningBalance = "INVENTORY_OPENING_BALANCES";
    public const string InventoryAdjustment = "INVENTORY_ADJUSTMENT";
    public const string InventoryCount = "INVENTORY_COUNT";
    public const string Waste = "INVENTORY_WASTE";

    // Fixed assets & maintenance (08-Module-Maintenance-FixedAssets, section 6.1 item 7). The accounts
    // come from the asset's category, carried on the document; the cost center of every line is the
    // asset's (or its branch's), supplied by the module itself (rule 27).
    public const string FixedAssetAcquisition = "FIXED_ASSETS";
    public const string Depreciation = "FIXED_ASSETS_DEPRECIATION_RUNS";
    public const string AssetDisposal = "FIXED_ASSETS_DISPOSALS";
    public const string MaintenanceExternalCost = "MAINTENANCE_REQUESTS";
    public const string MaintenanceSpareParts = "MAINTENANCE_SPARE_PARTS";

    public const string DepreciationExpenseGroup = "DepreciationExpense";
    public const string AccumulatedDepreciationGroup = "AccumulatedDepreciation";

    // Codes match the screen-settings (coding rules) codes wherever the screen has one, so the
    // posting section sits on the same screen as its numbering. Shift variance, drawer expenses and
    // waste have no numbering of their own and appear in screen settings only for posting.

    public const string PaymentsGroup = "Payments";
    public const string CommissionsGroup = "Commissions";

    /// <summary>Set by every stock-moving screen: true when the document moved stock at a cost.</summary>
    public const string HasStockMovementField = PostingTemplateEngine.HasStockMovementField;

    public static PostingScreenDefinition? Find(string screenCode) =>
        All.FirstOrDefault(s => string.Equals(s.ScreenCode, screenCode, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------ building blocks

    private static PostingScreenField Amount(string name, string ar, string en, decimal sample) => new(name, ar, en, PostingFieldKind.Amount, sample);

    private static PostingScreenField Entity(string name, string ar, string en, CostCenterLinkedEntityType type, bool isContext = false) =>
        new(name, ar, en, PostingFieldKind.Id, EntityType: type, IsContext: isContext);

    private static readonly PostingScreenField BranchId = Entity("BranchId", "الفرع", "Branch", CostCenterLinkedEntityType.Branch);
    private static readonly PostingScreenField WarehouseId = Entity("WarehouseId", "المخزن", "Warehouse", CostCenterLinkedEntityType.Warehouse);
    private static readonly PostingScreenField SupplierId = Entity("SupplierId", "المورد", "Supplier", CostCenterLinkedEntityType.Supplier);
    private static readonly PostingScreenField CustomerId = Entity("CustomerId", "العميل", "Customer", CostCenterLinkedEntityType.Customer);
    private static readonly PostingScreenField TerminalId = Entity("POSTerminalId", "نقطة البيع", "Terminal", CostCenterLinkedEntityType.POSTerminal);
    private static readonly PostingScreenField ShiftId = Entity("ShiftId", "الوردية", "Shift", CostCenterLinkedEntityType.None);

    /// <summary>The cashier comes from the shift the module is posting, not from a field on the document.</summary>
    private static readonly PostingScreenField CashierUserId =
        Entity("CashierUserId", "الكاشير (من الوردية)", "Cashier (from the shift)", CostCenterLinkedEntityType.Cashier, isContext: true);

    private static PostingTemplateLineInput Role(int n, PostingDirection d, CompanyAccountRole role, string field, bool optional = false) =>
        new(n, d, AccountSourceType.FromCompany, null, null, role.ToString(), AmountFormulaType.DirectField, field,
            optional ? ConditionType.FieldGreaterThanZero : ConditionType.None, optional ? field : null, null, null, []);

    private static PostingTemplateLineInput Resolver(int n, PostingDirection d, string key, string field, bool optional = false) =>
        new(n, d, AccountSourceType.Resolver, null, null, key, AmountFormulaType.DirectField, field,
            optional ? ConditionType.FieldGreaterThanZero : ConditionType.None, optional ? field : null, null, null, []);

    private static PostingTemplateLineInput Group(int n, PostingDirection d, string group) =>
        new(n, d, AccountSourceType.FromGroup, null, null, null, AmountFormulaType.GroupItemAmount, group,
            ConditionType.None, null, null, null, []);

    private static PostingTemplateLineInput FromField(int n, PostingDirection d, string accountField, string amountField) =>
        new(n, d, AccountSourceType.FromDocument, null, accountField, null, AmountFormulaType.DirectField, amountField,
            ConditionType.None, null, null, null, []);

    private static PostingTemplateLineInput FromFieldIf(int n, PostingDirection d, string accountField, string amountField) =>
        new(n, d, AccountSourceType.FromDocument, null, accountField, null, AmountFormulaType.DirectField, amountField,
            ConditionType.FieldGreaterThanZero, amountField, null, null, []);

    private static readonly PostingScreenField FixedAssetId = Entity("FixedAssetId", "الأصل", "Asset", CostCenterLinkedEntityType.None);

    private static PostingScreenField AccountField(string name, string ar, string en) => new(name, ar, en, PostingFieldKind.Account);

    private const string DefaultNote = "القالب القياسي — راجع الحسابات ومراكز التكلفة قبل التفعيل.";

    private static PostingTemplateDefinition Template(string ar, string en, params PostingTemplateLineInput[] lines) =>
        new(ar, en, DefaultNote, lines);

    /// <summary>A cost-of-sales style template: runs only when the document moved stock at a cost.</summary>
    private static PostingTemplateDefinition StockTemplate(string ar, string en, params PostingTemplateLineInput[] lines) =>
        new(ar, en, DefaultNote, lines, PostingTriggerType.HasStockMovement, ExecutionOrder: 2);

    private const PostingDirection Dr = PostingDirection.Debit;
    private const PostingDirection Cr = PostingDirection.Credit;

    // ------------------------------------------------------------------ the screens

    public static readonly IReadOnlyList<PostingScreenDefinition> All =
    [
        new(PurchaseInvoice, "فاتورة شراء", "Purchase invoice", SourceModule.Purchasing,
            "عند ترحيل الفاتورة. دايمًا دائنة للمورد — السداد (حتى النقدي) بسند صرف بيرحّل لوحده.",
            CanMoveStock: false,
            [
                SupplierId, BranchId, WarehouseId,
                Amount("Subtotal", "الإجمالي قبل الخصم", "Subtotal", 1000m),
                Amount("DiscountAmount", "الخصم", "Discount", 50m),
                Amount("NetAmount", "الصافي بعد الخصم", "Net amount", 950m),
                Amount("TaxAmount", "الضريبة", "Tax", 133m),
                Amount("TotalAmount", "الإجمالي النهائي (المستحق للمورد)", "Total", 1083m),
                Amount("AdditionalCosts", "مصاريف إضافية (مستحقة لطرف تالت)", "Additional costs", 0m),
                Amount("CommissionAmount", "عمولة", "Commission", 0m),
                new("CommissionAccountId", "حساب العمولة", "Commission account", PostingFieldKind.Account),
                new("PaymentTerms", "شروط الدفع", "Payment terms", PostingFieldKind.Text, Choices: ["Cash", "Net15", "Net30", "Net60"])
            ],
            [],
            [
                Template("فاتورة شراء", "Purchase invoice",
                    Role(1, Dr, CompanyAccountRole.Inventory, "NetAmount"),
                    Role(2, Dr, CompanyAccountRole.VatReceivable, "TaxAmount", optional: true),
                    Resolver(3, Cr, "Supplier.PayableAccountId", "TotalAmount"))
            ]),

        new(PurchaseReturn, "مرتجع مشتريات", "Purchase return", SourceModule.Purchasing,
            "عند ترحيل المرتجع. المخزون بيخرج بمتوسط التكلفة، والفرق عن سعر المرتجع فروق أسعار.",
            CanMoveStock: true,
            [
                SupplierId, BranchId, WarehouseId,
                Amount("ReturnValue", "قيمة المرتجع بسعر المورد", "Return value", 500m),
                Amount("TaxAmount", "الضريبة", "Tax", 70m),
                Amount("TotalAmount", "الإجمالي (بيتخصم من المورد)", "Total", 570m),
                Amount("CostAmount", "تكلفة البضاعة الخارجة من المخزن", "Stock cost out", 480m),
                Amount("PriceVarianceGain", "فرق سعر لصالحنا", "Price variance gain", 20m),
                Amount("PriceVarianceLoss", "فرق سعر علينا", "Price variance loss", 0m)
            ],
            [],
            [
                // One template: the supplier credit and the stock leaving are one event whose two
                // sides only balance through the price variance.
                Template("مرتجع مشتريات", "Purchase return",
                    Resolver(1, Dr, "Supplier.PayableAccountId", "TotalAmount"),
                    Role(2, Cr, CompanyAccountRole.Inventory, "CostAmount", optional: true),
                    Role(3, Cr, CompanyAccountRole.VatReceivable, "TaxAmount", optional: true),
                    Role(4, Cr, CompanyAccountRole.PurchasePriceVariance, "PriceVarianceGain", optional: true),
                    Role(5, Dr, CompanyAccountRole.PurchasePriceVariance, "PriceVarianceLoss", optional: true))
            ]),

        new(SalesInvoice, "فاتورة مبيعات", "Sales invoice", SourceModule.Sales,
            "عند ترحيل الفاتورة. الإيراد والضريبة والعميل — تكلفة البضاعة بتترحّل مع أمر التسليم، لأن تكلفتها مش معروفة غير لما البضاعة تخرج من المخزن.",
            CanMoveStock: false,
            [
                CustomerId, BranchId,
                new("PaymentType", "نوع الفاتورة", "Payment type", PostingFieldKind.Text, Choices: ["Cash", "Credit"]),
                Amount("Subtotal", "الإجمالي قبل الخصم", "Subtotal", 1000m),
                Amount("DiscountAmount", "الخصم", "Discount", 0m),
                Amount("NetAmount", "صافي المبيعات", "Net sales", 1000m),
                Amount("TaxAmount", "الضريبة", "Tax", 140m),
                Amount("TotalAmount", "الإجمالي النهائي (على العميل)", "Total", 1140m)
            ],
            [],
            [
                Template("فاتورة مبيعات", "Sales invoice",
                    Resolver(1, Dr, "Customer.ReceivableAccountId", "TotalAmount"),
                    Role(2, Cr, CompanyAccountRole.SalesRevenue, "NetAmount"),
                    Role(3, Cr, CompanyAccountRole.VatPayable, "TaxAmount", optional: true))
            ]),

        new(DeliveryOrder, "أمر تسليم", "Delivery order", SourceModule.Sales,
            "عند ترحيل أمر التسليم — لحظة خروج البضاعة فعلًا ومعرفة تكلفتها.",
            CanMoveStock: true,
            [CustomerId, BranchId, WarehouseId, Amount("CostAmount", "تكلفة البضاعة المسلَّمة", "Cost of goods delivered", 620m)],
            [],
            [
                new("تكلفة المبيعات", "Cost of sales", DefaultNote,
                    [Role(1, Dr, CompanyAccountRole.CostOfGoodsSold, "CostAmount"), Role(2, Cr, CompanyAccountRole.Inventory, "CostAmount")],
                    PostingTriggerType.HasStockMovement)
            ]),

        new(SalesReturn, "مرتجع مبيعات", "Sales return", SourceModule.Sales,
            "عند ترحيل المرتجع. الضريبة بنفس نسبة فاتورة المصدر، والبضاعة بترجع المخزن بمتوسط تكلفته.",
            CanMoveStock: true,
            [
                CustomerId, BranchId, WarehouseId,
                Amount("ReturnValue", "قيمة المرتجع", "Return value", 300m),
                Amount("TaxAmount", "الضريبة", "Tax", 42m),
                Amount("TotalAmount", "الإجمالي (لصالح العميل)", "Total", 342m),
                Amount("CostAmount", "تكلفة البضاعة الراجعة", "Stock cost back", 180m)
            ],
            [],
            [
                Template("مرتجع مبيعات", "Sales return",
                    Role(1, Dr, CompanyAccountRole.SalesReturn, "ReturnValue"),
                    Role(2, Dr, CompanyAccountRole.VatPayable, "TaxAmount", optional: true),
                    Resolver(3, Cr, "Customer.ReceivableAccountId", "TotalAmount")),
                StockTemplate("تكلفة المرتجع", "Returned stock",
                    Role(1, Dr, CompanyAccountRole.Inventory, "CostAmount"),
                    Role(2, Cr, CompanyAccountRole.CostOfGoodsSold, "CostAmount"))
            ]),

        new(PosInvoice, "فواتير نقطة البيع", "POS invoices", SourceModule.POS,
            "حسب وضع ترحيل الفرع: مع كل فاتورة، أو عند إقفال الوردية، أو بزرار إقفال اليوم. القيد الواحد دايمًا لفرع واحد.",
            CanMoveStock: true,
            [
                BranchId, TerminalId, ShiftId, CashierUserId,
                Amount("GrossSales", "المبيعات قبل الخصم", "Gross sales", 2000m),
                Amount("TotalDiscount", "إجمالي الخصومات (بنود + يدوي + ولاء)", "Total discount", 100m),
                Amount("NetSales", "صافي المبيعات", "Net sales", 1900m),
                Amount("ServiceCharge", "خدمة الصالة", "Service charge", 190m),
                Amount("Tips", "الإكراميات", "Tips", 20m),
                Amount("TaxAmount", "الضريبة", "Tax", 266m),
                Amount("TotalAmount", "الإجمالي المحصّل", "Total collected", 2376m),
                Amount("CostAmount", "تكلفة المبيعات", "Cost of sales", 760m),
                Amount("CommissionAmount", "عمولة ماكينات الدفع", "Card commission", 0m)
            ],
            [
                new(PaymentsGroup, "المحصّل لكل خزينة (حسب الجهاز وطريقة الدفع)", "Collected per treasury", 2376m),
                new(CommissionsGroup, "العمولة لكل خزينة", "Commission per treasury", 0m)
            ],
            [
                Template("مبيعات نقطة البيع", "POS sales",
                    Group(1, Dr, PaymentsGroup),
                    Role(2, Cr, CompanyAccountRole.SalesRevenue, "NetSales", optional: true),
                    Role(3, Cr, CompanyAccountRole.ServiceChargeRevenue, "ServiceCharge", optional: true),
                    Role(4, Cr, CompanyAccountRole.TipsPayable, "Tips", optional: true),
                    Role(5, Cr, CompanyAccountRole.VatPayable, "TaxAmount", optional: true),
                    Role(6, Dr, CompanyAccountRole.BankCommission, "CommissionAmount", optional: true),
                    Group(7, Cr, CommissionsGroup)),
                StockTemplate("تكلفة مبيعات نقطة البيع", "POS cost of sales",
                    Role(1, Dr, CompanyAccountRole.CostOfGoodsSold, "CostAmount"),
                    Role(2, Cr, CompanyAccountRole.Inventory, "CostAmount"))
            ]),

        new(PosReturn, "مرتجع نقطة البيع", "POS return", SourceModule.POS,
            "مع كل مرتجع. المبلغ المردود بيطلع من خزينة الكاش بتاعة الجهاز.",
            CanMoveStock: true,
            [
                BranchId, TerminalId, ShiftId,
                Amount("ReturnValue", "قيمة المرتجع", "Return value", 100m),
                Amount("TaxAmount", "الضريبة", "Tax", 14m),
                Amount("RefundTotal", "المبلغ المردود للعميل", "Refund", 114m),
                Amount("CostAmount", "تكلفة البضاعة الراجعة", "Stock cost back", 40m)
            ],
            [],
            [
                Template("مرتجع نقطة البيع", "POS return",
                    Role(1, Dr, CompanyAccountRole.SalesReturn, "ReturnValue"),
                    Role(2, Dr, CompanyAccountRole.VatPayable, "TaxAmount", optional: true),
                    Resolver(3, Cr, "POSTerminal.CashTreasuryAccount", "RefundTotal")),
                StockTemplate("تكلفة مرتجع نقطة البيع", "POS returned stock",
                    Role(1, Dr, CompanyAccountRole.Inventory, "CostAmount"),
                    Role(2, Cr, CompanyAccountRole.CostOfGoodsSold, "CostAmount"))
            ]),

        new(PosShiftVariance, "فروق الوردية", "Shift variance", SourceModule.POS,
            "عند إقفال الوردية (أو عند اعتماد الإقفال لو الفرق محتاج اعتماد) — في كل أوضاع الترحيل، ومنفصل عن قيد المبيعات.",
            CanMoveStock: false,
            [
                BranchId, TerminalId, ShiftId,
                Entity("CashierUserId", "الكاشير", "Cashier", CostCenterLinkedEntityType.Cashier),
                Amount("MinorShortage", "عجز في حدود المسموح (على الشركة)", "Shortage within tolerance", 0m),
                Amount("MajorShortage", "عجز أكبر من الحد (على الكاشير)", "Shortage over tolerance", 35m),
                Amount("TotalShortage", "إجمالي العجز", "Total shortage", 35m),
                Amount("Surplus", "زيادة", "Surplus", 0m)
            ],
            [],
            [
                Template("فروق الوردية", "Shift variance",
                    Role(1, Dr, CompanyAccountRole.CashShortage, "MinorShortage", optional: true),
                    Role(2, Dr, CompanyAccountRole.EmployeeReceivable, "MajorShortage", optional: true),
                    Resolver(3, Cr, "POSTerminal.CashTreasuryAccount", "TotalShortage", optional: true),
                    Resolver(4, Dr, "POSTerminal.CashTreasuryAccount", "Surplus", optional: true),
                    Role(5, Cr, CompanyAccountRole.SalesVariance, "Surplus", optional: true))
            ]),

        new(PosDrawerExpense, "مصروفات درج الكاشير", "Drawer expense", SourceModule.POS,
            "مع كل مصروف يتسجّل من الدرج.",
            CanMoveStock: false,
            [
                BranchId, TerminalId, ShiftId,
                Amount("Amount", "المبلغ", "Amount", 75m),
                new("ExpenseAccountId", "حساب المصروف (من المستند)", "Expense account", PostingFieldKind.Account)
            ],
            [],
            [
                Template("مصروف درج", "Drawer expense",
                    FromField(1, Dr, "ExpenseAccountId", "Amount"),
                    Resolver(2, Cr, "POSTerminal.CashTreasuryAccount", "Amount"))
            ]),

        new(StockIn, "إذن إضافة (مخزن)", "Stock in", SourceModule.Inventory,
            "عند ترحيل الإذن. قيمة الإضافة بالتكلفة المُدخلة على السطور.",
            CanMoveStock: true,
            [BranchId, WarehouseId, Amount("CostAmount", "قيمة الإضافة", "Stock value in", 400m)],
            [],
            [
                Template("إذن إضافة", "Stock in",
                    Role(1, Dr, CompanyAccountRole.Inventory, "CostAmount"),
                    Role(2, Cr, CompanyAccountRole.InventoryAdjustment, "CostAmount"))
            ]),

        new(StockOut, "إذن صرف (مخزن)", "Stock out", SourceModule.Inventory,
            "عند ترحيل الإذن. الصرف بمتوسط تكلفة المخزن.",
            CanMoveStock: true,
            [BranchId, WarehouseId, Amount("CostAmount", "تكلفة المنصرف", "Stock cost out", 400m)],
            [],
            [
                Template("إذن صرف", "Stock out",
                    Role(1, Dr, CompanyAccountRole.InventoryAdjustment, "CostAmount"),
                    Role(2, Cr, CompanyAccountRole.Inventory, "CostAmount"))
            ]),

        // No standard template: the other side of an opening stock balance (an opening-balance
        // equity account, usually) is the finance manager's choice, not something to guess.
        new(OpeningBalance, "أرصدة افتتاحية (مخزن)", "Opening stock", SourceModule.Inventory,
            "عند ترحيل المستند. الطرف الدائن يحدده المدير المالي — مفيش قالب قياسي.",
            CanMoveStock: true,
            [BranchId, WarehouseId, Amount("CostAmount", "قيمة الرصيد الافتتاحي", "Opening value", 5000m)],
            [],
            []),

        new(InventoryAdjustment, "تسوية مخزون مستقلة", "Inventory adjustment", SourceModule.Inventory,
            "عند ترحيل التسوية — زيادة أو نقص حسب اتجاه المستند.",
            CanMoveStock: true,
            [
                BranchId, WarehouseId,
                Amount("IncreaseAmount", "قيمة الزيادة", "Increase", 120m),
                Amount("DecreaseAmount", "قيمة النقص", "Decrease", 0m)
            ],
            [],
            [
                Template("تسوية مخزون", "Inventory adjustment",
                    Role(1, Dr, CompanyAccountRole.Inventory, "IncreaseAmount", optional: true),
                    Role(2, Cr, CompanyAccountRole.InventoryAdjustment, "IncreaseAmount", optional: true),
                    Role(3, Dr, CompanyAccountRole.InventoryAdjustment, "DecreaseAmount", optional: true),
                    Role(4, Cr, CompanyAccountRole.Inventory, "DecreaseAmount", optional: true))
            ]),

        new(InventoryCount, "تسوية الجرد", "Inventory count", SourceModule.Inventory,
            "عند إقفال الجرد وترحيل فروقه.",
            CanMoveStock: true,
            [
                BranchId, WarehouseId,
                Amount("IncreaseAmount", "قيمة زيادة الجرد", "Count surplus", 60m),
                Amount("DecreaseAmount", "قيمة عجز الجرد", "Count shortage", 150m)
            ],
            [],
            [
                Template("تسوية الجرد", "Inventory count",
                    Role(1, Dr, CompanyAccountRole.Inventory, "IncreaseAmount", optional: true),
                    Role(2, Cr, CompanyAccountRole.InventoryAdjustment, "IncreaseAmount", optional: true),
                    Role(3, Dr, CompanyAccountRole.InventoryAdjustment, "DecreaseAmount", optional: true),
                    Role(4, Cr, CompanyAccountRole.Inventory, "DecreaseAmount", optional: true))
            ]),

        new(Waste, "الهالك", "Waste", SourceModule.Inventory,
            "مع تسجيل الهالك.",
            CanMoveStock: true,
            [BranchId, WarehouseId, Amount("CostAmount", "تكلفة الهالك", "Waste cost", 45m)],
            [],
            [
                Template("هالك", "Waste",
                    Role(1, Dr, CompanyAccountRole.InventoryAdjustment, "CostAmount"),
                    Role(2, Cr, CompanyAccountRole.Inventory, "CostAmount"))
            ]),

        // ------------------------------------------------------------------ fixed assets & maintenance

        new(FixedAssetAcquisition, "اقتناء أصل ثابت", "Fixed asset acquisition", SourceModule.FixedAssets,
            "عند اعتماد الأصل. بالتكلفة المعادلة بعملة الشركة الأساسية.",
            CanMoveStock: false,
            [
                BranchId, FixedAssetId, SupplierId,
                Amount("AcquisitionAmount", "تكلفة الاقتناء (بالعملة الأساسية)", "Acquisition cost (base currency)", 120000m),
                AccountField("AssetAccountId", "حساب الأصل (من الفئة)", "Asset account (category)"),
                AccountField("FundingAccountId", "حساب التمويل (المورد/الخزينة — من الأصل)", "Funding account (asset)")
            ],
            [],
            [
                Template("اقتناء أصل", "Asset acquisition",
                    FromField(1, Dr, "AssetAccountId", "AcquisitionAmount"),
                    FromField(2, Cr, "FundingAccountId", "AcquisitionAmount"))
            ]),

        new(Depreciation, "تشغيل الإهلاك الشهري", "Monthly depreciation", SourceModule.FixedAssets,
            "عند ترحيل تشغيل الشهر — قيد واحد لكل الأصول، سطر لكل أصل بمركز تكلفته.",
            CanMoveStock: false,
            [Amount("TotalDepreciation", "إجمالي الإهلاك", "Total depreciation", 3500m)],
            [
                new(DepreciationExpenseGroup, "مصروف الإهلاك لكل أصل (حساب فئته)", "Depreciation expense per asset", 3500m),
                new(AccumulatedDepreciationGroup, "مجمع الإهلاك لكل أصل (حساب فئته)", "Accumulated depreciation per asset", 3500m)
            ],
            [
                Template("إهلاك شهري", "Monthly depreciation",
                    Group(1, Dr, DepreciationExpenseGroup),
                    Group(2, Cr, AccumulatedDepreciationGroup))
            ]),

        new(AssetDisposal, "استبعاد أصل", "Asset disposal", SourceModule.FixedAssets,
            "عند ترحيل الاستبعاد: يشيل التكلفة ومجمع الإهلاك ويسجّل الربح أو الخسارة.",
            CanMoveStock: false,
            [
                BranchId, FixedAssetId,
                Amount("CostAmount", "تكلفة الأصل", "Asset cost", 120000m),
                Amount("AccumulatedDepreciation", "مجمع الإهلاك", "Accumulated depreciation", 90000m),
                Amount("Proceeds", "المحصَّل", "Proceeds", 35000m),
                Amount("Gain", "ربح الاستبعاد", "Gain", 5000m),
                Amount("Loss", "خسارة الاستبعاد", "Loss", 0m),
                AccountField("AssetAccountId", "حساب الأصل (من الفئة)", "Asset account"),
                AccountField("AccumulatedDepreciationAccountId", "حساب مجمع الإهلاك (من الفئة)", "Accumulated depreciation account"),
                AccountField("ProceedsAccountId", "حساب المحصَّل (من الاستبعاد)", "Proceeds account"),
                AccountField("GainAccountId", "حساب أرباح الاستبعاد (من الفئة)", "Gain account"),
                AccountField("LossAccountId", "حساب خسائر الاستبعاد (من الفئة)", "Loss account")
            ],
            [],
            [
                Template("استبعاد أصل", "Asset disposal",
                    FromFieldIf(1, Dr, "AccumulatedDepreciationAccountId", "AccumulatedDepreciation"),
                    FromFieldIf(2, Dr, "ProceedsAccountId", "Proceeds"),
                    FromFieldIf(3, Dr, "LossAccountId", "Loss"),
                    FromField(4, Cr, "AssetAccountId", "CostAmount"),
                    FromFieldIf(5, Cr, "GainAccountId", "Gain"))
            ]),

        new(MaintenanceExternalCost, "صيانة — تكلفة خارجية", "Maintenance — external cost", SourceModule.FixedAssets,
            "عند إكمال طلب الصيانة، لو فيه عمالة أو قطع اتشرت مخصوص (مش من المخزون).",
            CanMoveStock: false,
            [
                BranchId, FixedAssetId, SupplierId,
                Amount("ExternalCost", "التكلفة الخارجية (عمالة + قطع مشتراة)", "External cost", 900m),
                Amount("LaborCost", "العمالة", "Labor", 600m),
                Amount("BoughtPartsCost", "قطع اتشرت للطلب", "Parts bought for the job", 300m),
                AccountField("MaintenanceExpenseAccountId", "حساب مصروف الصيانة (من فئة الأصل)", "Maintenance expense account"),
                AccountField("ExternalCreditAccountId", "الطرف الدائن (المورد/الخزينة — من الطلب)", "Credit account (request)")
            ],
            [],
            [
                Template("صيانة — تكلفة خارجية", "Maintenance — external cost",
                    FromField(1, Dr, "MaintenanceExpenseAccountId", "ExternalCost"),
                    FromField(2, Cr, "ExternalCreditAccountId", "ExternalCost"))
            ]),

        new(MaintenanceSpareParts, "صيانة — قطع غيار من المخزون", "Maintenance — stocked spare parts", SourceModule.FixedAssets,
            "عند إكمال طلب الصيانة، لو اتصرف قطع غيار من المخزون (بمتوسط التكلفة وقت الصرف).",
            CanMoveStock: true,
            [
                BranchId, FixedAssetId,
                Amount("StockPartsCost", "تكلفة القطع المصروفة من المخزون", "Stocked parts cost", 450m),
                AccountField("MaintenanceExpenseAccountId", "حساب مصروف الصيانة (من فئة الأصل)", "Maintenance expense account")
            ],
            [],
            [
                StockTemplate("صيانة — قطع غيار", "Maintenance — spare parts",
                    FromField(1, Dr, "MaintenanceExpenseAccountId", "StockPartsCost"),
                    Role(2, Cr, CompanyAccountRole.Inventory, "StockPartsCost"))
            ])
    ];
}
