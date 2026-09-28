using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Infrastructure.Persistence.Seeding;

/// <summary>Initial sidebar tree — mirrors the Accounting module's previously-hardcoded nav array.</summary>
public static class MenuItemSeedData
{
    public static List<MenuItem> Build()
    {
        // Id is DB-identity; leaves reference the group via the Parent navigation (not a
        // hand-assigned ParentId) so EF can fix up the FK after the group's Id is generated.
        var group = new MenuItem
        {
            Code = "ACCOUNTING",
            NameAr = "الحسابات العامة",
            NameEn = "General Accounting",
            DisplayOrder = 1,
            RouteKey = null,
            IsActive = true
        };

        var inventoryGroup = new MenuItem
        {
            Code = "INVENTORY",
            NameAr = "المخزون والتصنيع",
            NameEn = "Inventory & Manufacturing",
            DisplayOrder = 2,
            RouteKey = null,
            IsActive = true
        };

        var purchasingGroup = new MenuItem
        {
            Code = "PURCHASING",
            NameAr = "المشتريات",
            NameEn = "Purchasing",
            DisplayOrder = 3,
            RouteKey = null,
            IsActive = true
        };

        var salesGroup = new MenuItem
        {
            Code = "SALES",
            NameAr = "المبيعات",
            NameEn = "Sales",
            DisplayOrder = 4,
            RouteKey = null,
            IsActive = true
        };

        var posGroup = new MenuItem
        {
            Code = "POS",
            NameAr = "نقاط البيع",
            NameEn = "POS & Shifts",
            DisplayOrder = 5,
            RouteKey = null,
            IsActive = true
        };

        var assetsGroup = new MenuItem
        {
            Code = "ASSETS_MAINTENANCE",
            NameAr = "الأصول والصيانة",
            NameEn = "Assets & Maintenance",
            DisplayOrder = 6,
            RouteKey = null,
            IsActive = true
        };

        var hrGroup = new MenuItem
        {
            Code = "HR",
            NameAr = "شئون العاملين",
            NameEn = "Human Resources",
            DisplayOrder = 7,
            RouteKey = null,
            IsActive = true
        };

        var payrollGroup = new MenuItem
        {
            Code = "PAYROLL",
            NameAr = "الرواتب",
            NameEn = "Payroll",
            DisplayOrder = 8,
            RouteKey = null,
            IsActive = true
        };

        var settingsGroup = new MenuItem
        {
            Code = "SETTINGS",
            NameAr = "الإعدادات",
            NameEn = "Settings",
            DisplayOrder = 9,
            RouteKey = null,
            IsActive = true
        };

        MenuItem Leaf(MenuItem parent, string code, string nameAr, string nameEn, int order, string routeKey) => new()
        {
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            Parent = parent,
            DisplayOrder = order,
            RouteKey = routeKey,
            IsActive = true
        };

        return
        [
            group,
            Leaf(group, "ACCOUNTING_JOURNAL_ENTRIES", "القيود اليومية", "Journal Entries", 1, "/accounting/journal-entries"),
            Leaf(group, "ACCOUNTING_RECEIPT_VOUCHERS", "سندات القبض", "Receipt Vouchers", 2, "/accounting/receipt-vouchers"),
            Leaf(group, "ACCOUNTING_PAYMENT_VOUCHERS", "سندات الصرف", "Payment Vouchers", 3, "/accounting/payment-vouchers"),
            Leaf(group, "ACCOUNTING_CHART_OF_ACCOUNTS", "دليل الحسابات", "Chart of Accounts", 4, "/accounting/chart-of-accounts"),
            Leaf(group, "ACCOUNTING_DIMENSIONS", "مراكز التكلفة", "Cost Centers", 5, "/accounting/dimensions"),
            Leaf(group, "ORG_BRANCHES", "الفروع", "Branches", 6, "/accounting/branches"),
            Leaf(group, "ACCOUNTING_CUSTODY", "العهد وتسويتها", "Custody & Settlement", 7, "/accounting/custody-registers"),
            Leaf(group, "ACCOUNTING_CASH_RECONCILIATIONS", "مطابقة الخزينة", "Cash Reconciliation", 8, "/accounting/cash-reconciliations"),
            Leaf(group, "ACCOUNTING_BANK_RECONCILIATIONS", "مطابقة البنك", "Bank Reconciliation", 9, "/accounting/bank-reconciliations"),
            Leaf(group, "ACCOUNTING_PERIODS", "الفترات المالية", "Accounting Periods", 10, "/accounting/accounting-periods"),
            Leaf(group, "ACCOUNTING_REPORTS", "التقارير", "Reports", 11, "/accounting/reports"),
            Leaf(group, "ACCOUNTING_PAYMENT_METHODS", "طرق الدفع", "Payment Methods", 12, "/accounting/payment-methods"),
            Leaf(group, "ACCOUNTING_TREASURY_TRANSFERS", "تحويلات الخزائن/البنوك", "Treasury Transfers", 13, "/accounting/treasury-transfers"),
            Leaf(group, "ACCOUNTING_OPENING_BALANCES", "أرصدة افتتاحية للحسابات", "Account Opening Balances", 14, "/accounting/opening-balances"),
            Leaf(group, "ACCOUNTING_ACCOUNT_MAPPINGS", "حسابات الترحيل الافتراضية", "Default Posting Accounts", 15, "/accounting/settings/account-mappings"),
            Leaf(group, "ACCOUNTING_POSTING_REPORTS", "تقارير الترحيل الآلي", "Posting Reports", 16, "/accounting/posting-reports"),
            inventoryGroup,
            Leaf(inventoryGroup, "INVENTORY_ITEMS", "الأصناف", "Items", 1, "/inventory/items"),
            Leaf(inventoryGroup, "INVENTORY_ITEM_GROUPS", "مجموعات الأصناف", "Item Groups", 2, "/inventory/item-groups"),
            Leaf(inventoryGroup, "INVENTORY_UNITS_OF_MEASURE", "وحدات القياس", "Units of Measure", 3, "/inventory/units-of-measure"),
            Leaf(inventoryGroup, "INVENTORY_POS_CATEGORIES", "تصنيفات نقطة البيع", "POS Categories", 4, "/inventory/pos-categories"),
            Leaf(inventoryGroup, "INVENTORY_WAREHOUSES", "المخازن", "Warehouses", 5, "/inventory/warehouses"),
            Leaf(inventoryGroup, "INVENTORY_OPENING_BALANCES", "أرصدة افتتاحية", "Opening Balances", 6, "/inventory/opening-balances"),
            Leaf(inventoryGroup, "INVENTORY_REPORTS", "تقارير المخزون", "Inventory Reports", 23, "/inventory/reports"),
            Leaf(inventoryGroup, "INVENTORY_STOCK_IN", "إذن إضافة", "Stock In", 7, "/inventory/stock-in"),
            Leaf(inventoryGroup, "INVENTORY_STOCK_OUT", "إذن صرف", "Stock Out", 8, "/inventory/stock-out"),
            Leaf(inventoryGroup, "INVENTORY_CUSTODY_OFFICERS", "مسؤولو العهد", "Custody Officers", 9, "/inventory/custody-officers"),
            Leaf(inventoryGroup, "INVENTORY_TRANSFER_ORDER", "أمر تحويل", "Transfer Order", 10, "/inventory/transfer-order"),
            Leaf(inventoryGroup, "INVENTORY_TRANSFER_RECEIPT", "استلام تحويل", "Transfer Receipt", 11, "/inventory/transfer-receipt"),
            Leaf(inventoryGroup, "INVENTORY_BRANCH_REQUEST", "طلبات توريد الفروع", "Branch Requests", 12, "/inventory/branch-requests"),
            Leaf(inventoryGroup, "INVENTORY_RECIPES", "الوصفات", "Recipes", 13, "/inventory/recipes"),
            Leaf(inventoryGroup, "INVENTORY_PRODUCTION_ORDERS", "أوامر الإنتاج", "Production Orders", 14, "/inventory/production-orders"),
            Leaf(inventoryGroup, "INVENTORY_WASTE_RECORDS", "متابعة الهالك", "Waste Tracking", 15, "/inventory/waste-records"),
            Leaf(inventoryGroup, "INVENTORY_PRODUCTION_ISSUES", "صرف الإنتاج", "Production Issues", 16, "/inventory/production-issues"),
            Leaf(inventoryGroup, "INVENTORY_PRODUCTION_RECEIPTS", "استلام الإنتاج", "Production Receipts", 17, "/inventory/production-receipts"),
            Leaf(inventoryGroup, "INVENTORY_COUNTS", "دورة الجرد", "Inventory Counts", 18, "/inventory/inventory-counts"),
            Leaf(inventoryGroup, "INVENTORY_ADJUSTMENTS", "تسوية جرد مستقلة", "Inventory Adjustments", 19, "/inventory/inventory-adjustments"),
            Leaf(inventoryGroup, "INVENTORY_SETTINGS_SHORTAGE_POLICY", "سياسة تجاوز الحدود والنقص", "Shortage Policy", 20, "/inventory/settings/shortage-policy"),
            Leaf(inventoryGroup, "INVENTORY_SETTINGS_GENERAL", "إعدادات المخزون العامة", "Inventory Settings", 21, "/inventory/settings/inventory-settings"),
            Leaf(inventoryGroup, "INVENTORY_SETTINGS_SALES_MODE", "إعدادات نماذج الإنتاج والبيع", "Production/Sales Mode", 22, "/inventory/settings/production-sales-mode"),
            purchasingGroup,
            Leaf(purchasingGroup, "PURCHASING_SUPPLIERS", "الموردون", "Suppliers", 1, "/purchasing/suppliers"),
            Leaf(purchasingGroup, "PURCHASING_PURCHASE_REQUESTS", "طلبات الشراء", "Purchase Requests", 2, "/purchasing/purchase-requests"),
            Leaf(purchasingGroup, "PURCHASING_PURCHASE_ORDERS", "أوامر الشراء", "Purchase Orders", 3, "/purchasing/purchase-orders"),
            Leaf(purchasingGroup, "PURCHASING_GOODS_RECEIPTS", "إذن إضافة (استلام مشتريات)", "Goods Receipts", 4, "/purchasing/goods-receipts"),
            Leaf(purchasingGroup, "PURCHASING_CYCLE_SETTINGS", "إعدادات دورة المشتريات", "Purchase Cycle Settings", 5, "/purchasing/settings/purchase-cycle"),
            Leaf(purchasingGroup, "PURCHASING_PURCHASE_INVOICES", "فواتير الشراء", "Purchase Invoices", 6, "/purchasing/purchase-invoices"),
            Leaf(purchasingGroup, "PURCHASING_SUPPLIER_PAYMENTS", "سداد الموردين", "Supplier Payments", 7, "/purchasing/supplier-payments"),
            Leaf(purchasingGroup, "PURCHASING_SUPPLIER_PRICE_HISTORY", "تاريخ أسعار الموردين", "Supplier Price History", 8, "/purchasing/supplier-price-history"),
            Leaf(purchasingGroup, "PURCHASING_PURCHASE_RETURNS", "مردودات المشتريات", "Purchase Returns", 9, "/purchasing/purchase-returns"),
            Leaf(purchasingGroup, "PURCHASING_SUPPLIER_CONTRACTS", "عقود الموردين", "Supplier Contracts", 10, "/purchasing/supplier-contracts"),
            Leaf(purchasingGroup, "PURCHASING_SUPPLIER_EVALUATIONS", "تقييم أداء الموردين", "Supplier Evaluations", 11, "/purchasing/supplier-evaluations"),
            Leaf(purchasingGroup, "PURCHASING_RFQS", "طلبات عروض الأسعار", "Requests for Quotation", 12, "/purchasing/rfqs"),
            Leaf(purchasingGroup, "PURCHASING_REPORTS", "تقارير المشتريات", "Purchasing Reports", 13, "/purchasing/reports"),
            Leaf(purchasingGroup, "PURCHASING_PURCHASE_EXPENSES", "مصروفات الشراء", "Purchase Expenses", 14, "/purchasing/purchase-expenses"),
            salesGroup,
            Leaf(salesGroup, "SALES_CUSTOMERS", "العملاء", "Customers", 1, "/sales/customers"),
            Leaf(salesGroup, "SALES_QUOTES", "عروض الأسعار", "Sales Quotes", 2, "/sales/quotes"),
            Leaf(salesGroup, "SALES_ORDERS", "أوامر البيع", "Sales Orders", 3, "/sales/sales-orders"),
            Leaf(salesGroup, "SALES_INVOICES", "فواتير المبيعات", "Sales Invoices", 4, "/sales/invoices"),
            Leaf(salesGroup, "SALES_DELIVERY_ORDERS", "أوامر التسليم", "Delivery Orders", 5, "/sales/delivery-orders"),
            Leaf(salesGroup, "SALES_RETURNS", "مرتجعات المبيعات", "Sales Returns", 6, "/sales/returns"),
            Leaf(salesGroup, "SALES_PRICE_LISTS", "قوائم الأسعار", "Price Lists", 7, "/sales/price-lists"),
            Leaf(salesGroup, "SALES_DISCOUNTS", "الخصومات", "Discounts", 8, "/sales/discounts"),
            Leaf(salesGroup, "SALES_LOYALTY_TIERS", "شرائح الولاء", "Loyalty Tiers", 9, "/sales/loyalty-tiers"),
            Leaf(salesGroup, "SALES_LOYALTY_PROGRAM_SETTINGS", "إعدادات برنامج الولاء", "Loyalty Program Settings", 10, "/sales/settings/loyalty-program"),
            Leaf(salesGroup, "SALES_CYCLE_SETTINGS", "إعدادات دورة المبيعات", "Sales Cycle Settings", 11, "/sales/settings/sales-cycle"),
            posGroup,
            Leaf(posGroup, "POS_TERMINALS", "نقاط البيع", "POS Terminals", 1, "/pos/terminals"),
            Leaf(posGroup, "POS_SHIFT_CONSOLE", "فتح/إغلاق وردية", "Open/Close Shift", 2, "/pos/shift-console"),
            Leaf(posGroup, "POS_SHIFTS", "سجل الورديات", "Shifts Log", 3, "/pos/shifts"),
            Leaf(posGroup, "POS_SHIFT_ASSIGNMENTS", "تعيين الكاشير على الجهاز", "Shift Assignments", 4, "/pos/shift-assignments"),
            Leaf(posGroup, "POS_BRANCH_SETTINGS", "إعدادات نقطة البيع للفرع", "POS Branch Settings", 5, "/pos/branch-settings"),
            Leaf(posGroup, "POS_TABLE_BOARD", "شاشة البيع", "Sales Screen", 6, "/pos/table-board"),
            Leaf(posGroup, "POS_CHECKS_OPEN", "الشيكات المفتوحة", "Open Checks", 7, "/pos/checks/open"),
            Leaf(posGroup, "POS_CHECKS_HELD", "الفواتير المعلّقة", "Held Invoices", 8, "/pos/checks/held"),
            Leaf(posGroup, "POS_PAYMENT_METHOD_CONFIGS", "إعداد طرق الدفع", "Payment Method Setup", 9, "/pos/payment-method-configs"),
            Leaf(posGroup, "POS_DRAWER_MOVEMENTS", "حركات الدرج", "Drawer Movements", 10, "/pos/drawer-movements"),
            Leaf(posGroup, "POS_DRAWER_EXPENSES", "مصروفات درج الكاشير", "Drawer Expenses", 11, "/pos/drawer-expenses"),
            Leaf(posGroup, "POS_RETURNS", "مرتجعات نقطة البيع", "POS Returns", 12, "/pos/returns"),
            Leaf(posGroup, "POS_DELIVERY_ORDERS", "طلبات الدليفري", "Delivery Orders", 13, "/pos/delivery-orders"),
            Leaf(posGroup, "POS_QR_TICKETS", "تذاكر QR", "QR Tickets", 14, "/pos/qr-tickets"),
            Leaf(posGroup, "POS_BLEND_CONSULTATION", "استشاري التصنيع", "Blend Consultation", 15, "/pos/blend-consultation"),
            Leaf(posGroup, "POS_BLEND_TYPES", "أنواع خلطات البن", "Blend Types", 16, "/pos/blend-types"),
            assetsGroup,
            Leaf(assetsGroup, "FIXED_ASSETS_CATEGORIES", "فئات الأصول", "Asset Categories", 1, "/fixed-assets/categories"),
            Leaf(assetsGroup, "FIXED_ASSETS", "الأصول الثابتة", "Fixed Assets", 2, "/fixed-assets/assets"),
            Leaf(assetsGroup, "FIXED_ASSETS_SCHEDULE", "جدول الإهلاك", "Depreciation Schedule", 3, "/fixed-assets/depreciation-schedule"),
            Leaf(assetsGroup, "FIXED_ASSETS_DEPRECIATION_RUNS", "تشغيل الإهلاك الشهري", "Depreciation Runs", 4, "/fixed-assets/depreciation-runs"),
            Leaf(assetsGroup, "FIXED_ASSETS_TRANSFERS", "نقل الأصول", "Asset Transfers", 5, "/fixed-assets/transfers"),
            Leaf(assetsGroup, "FIXED_ASSETS_DISPOSALS", "استبعاد الأصول", "Asset Disposals", 6, "/fixed-assets/disposals"),
            Leaf(assetsGroup, "FIXED_ASSETS_PHYSICAL_COUNTS", "جرد الأصول", "Asset Counts", 7, "/fixed-assets/physical-counts"),
            Leaf(assetsGroup, "MAINTENANCE_CATEGORIES", "فئات الصيانة", "Maintenance Categories", 8, "/maintenance/categories"),
            Leaf(assetsGroup, "MAINTENANCE_ISSUES", "بلاغات الأعطال", "Fault Reports", 9, "/maintenance/issues"),
            Leaf(assetsGroup, "MAINTENANCE_REQUESTS", "طلبات الصيانة", "Maintenance Requests", 10, "/maintenance/requests"),
            Leaf(assetsGroup, "MAINTENANCE_SCHEDULES", "الصيانة الدورية", "Preventive Schedules", 11, "/maintenance/schedules"),
            Leaf(assetsGroup, "MAINTENANCE_BOARD", "لوحة الصيانة", "Maintenance Board", 12, "/maintenance/board"),
            Leaf(assetsGroup, "FIXED_ASSETS_SETTINGS", "إعدادات الأصول والصيانة", "Assets & Maintenance Settings", 13, "/fixed-assets/settings"),

            // HR Core (Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1 — lookups only; controllers
            // are built later (1.1.4), so these routes 404 until then.
            hrGroup,
            Leaf(hrGroup, "HR_JOB_GRADES", "الدرجات الوظيفية", "Job Grades", 1, "/hr/job-grades"),
            Leaf(hrGroup, "HR_JOB_POSITIONS", "الوظائف", "Job Positions", 2, "/hr/job-positions"),
            Leaf(hrGroup, "HR_ORG_UNITS", "الهيكل التنظيمي", "Org Units", 3, "/hr/org-units"),
            Leaf(hrGroup, "HR_DOCUMENT_TYPES", "أنواع المستندات", "Document Types", 4, "/hr/document-types"),
            Leaf(hrGroup, "HR_INSURANCE_OFFICES", "مكاتب التأمينات", "Insurance Offices", 5, "/hr/insurance-offices"),
            // Batch B5 — was registered in ScreenCodeCatalog since Batch B1 (for ICodeGenerator) but
            // not here yet, since the Employee entity/controller didn't exist until now. Without this,
            // SetRoleScreenPermissionsCommand rejects HR_EMPLOYEES as SET-SCREEN-UNKNOWN and no role
            // could ever be granted it (only the SuperAdmin full-access bypass would work) — caught by
            // EmployeeApiTests.Reveal_pii_is_forbidden_without_the_button_permission_and_audited_....
            Leaf(hrGroup, "HR_EMPLOYEES", "الموظفين", "Employees", 6, "/hr/employees"),
            // Sub-Batch 1.5.0 — HrSettings entity/controller built now; not a codable document
            // (single settings row per company), so unlike the lookups above it has no
            // ScreenCodeCatalog entry, same precedent as PURCHASING_CYCLE_SETTINGS/FIXED_ASSETS_SETTINGS.
            Leaf(hrGroup, "HR_SETTINGS", "إعدادات HR", "HR Settings", 7, "/hr/settings"),
            // Sub-Batch 1.5.5 — HR_HIRING is a pure frontend orchestration page (Docs/Implementation/
            // HR-MASTER-PLAN.md §Phase 1.5): every step calls an existing HR_EMPLOYEES-gated endpoint
            // (CreateEmployee, CreateEmployeePersonalData, CreateEmploymentContract,
            // CreateEmployeeDocument, AssignUserToEmployee) — no dedicated wizard-orchestration
            // command or Controller, and so no ScreenCodeCatalog entry either (same precedent as
            // HR_SETTINGS above). Registered here only so the page has a menu entry and a resolvable
            // screen for the sidebar/permission-hiding convenience.
            Leaf(hrGroup, "HR_HIRING", "التعيين", "Hiring", 8, "/hr/hiring/new"),

            // Attendance & Leave (Docs/Implementation/HR-MASTER-PLAN.md §Phase 3).
            Leaf(hrGroup, "HR_WORK_SHIFTS", "ورديات العمل", "Work Shifts", 9, "/hr/work-shifts"),
            Leaf(hrGroup, "HR_SHIFT_SCHEDULES", "جداول الورديات", "Shift Schedules", 10, "/hr/shift-schedules"),
            // Remarks8 Item 7 — Phase 3 Amendment.
            Leaf(hrGroup, "HR_SHIFT_SCHEDULE_GENERATOR", "توليد جداول الورديات", "Shift Schedule Generator", 11, "/hr/shift-schedule-generator"),
            Leaf(hrGroup, "HR_TIME_ENTRIES", "تسجيلات الحضور", "Time Entries", 12, "/hr/time-entries"),
            // تقويم الحضور Toggle جوه نفس شاشة /hr/attendance (مش MenuItem منفصل — MenuItem.Code
            // لازم يكون فريد بالـDB، ومكرَّرش صلاحية HR_ATTENDANCE بكود تاني هيكسر resolveScreenCode
            // في الـFrontend، Phase-3-Research.md نمط).
            Leaf(hrGroup, "HR_ATTENDANCE", "الحضور اليومي", "Daily Attendance", 13, "/hr/attendance"),
            Leaf(hrGroup, "HR_ATTENDANCE_CONFLICTS", "تعارضات الوردية مع الحضور", "Attendance Conflicts", 14, "/hr/attendance-conflicts"),
            Leaf(hrGroup, "HR_LEAVE_TYPES", "أنواع الإجازات", "Leave Types", 15, "/hr/leave-types"),
            Leaf(hrGroup, "HR_HOLIDAYS", "العطلات", "Holidays", 16, "/hr/holidays"),
            Leaf(hrGroup, "HR_LEAVE_BALANCES", "أرصدة الإجازات", "Leave Balances", 17, "/hr/leave-balances"),
            Leaf(hrGroup, "HR_LEAVE_REQUESTS", "طلبات الإجازات", "Leave Requests", 18, "/hr/leave-requests"),
            Leaf(hrGroup, "HR_OVERTIME", "طلبات الإضافي", "Overtime Requests", 19, "/hr/overtime-requests"),

            // Fingerprint devices (Docs/Implementation/Phase-3B-Research.md §Phase 3B).
            Leaf(hrGroup, "HR_ATTENDANCE_DEVICES", "أجهزة البصمة", "Attendance Devices", 20, "/hr/attendance-devices"),
            Leaf(hrGroup, "HR_EMPLOYEE_DEVICE_MAPPINGS", "ربط الموظفين بالأجهزة", "Employee Device Mappings", 21, "/hr/employee-device-mappings"),
            Leaf(hrGroup, "HR_ATTENDANCE_DEVICE_LOGS", "سجل مزامنة الأجهزة", "Device Sync Log", 22, "/hr/attendance-device-logs"),
            Leaf(hrGroup, "HR_ATTENDANCE_RECONCILIATION", "تسوية بصمات الأجهزة", "Attendance Reconciliation", 23, "/hr/attendance-reconciliation"),
            // Payroll (Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6, §5.1 row 14).
            Leaf(hrGroup, "HR_SALARY_CHANGES", "تعديلات الرواتب", "Salary Changes", 24, "/hr/salary-changes"),

            payrollGroup,
            Leaf(payrollGroup, "PAY_SALARY_COMPONENTS", "بنود الرواتب", "Salary Components", 1, "/payroll/salary-components"),
            Leaf(payrollGroup, "PAY_SALARY_STRUCTURES", "هياكل الرواتب", "Salary Structures", 2, "/payroll/salary-structures"),
            Leaf(payrollGroup, "PAY_PERIODS", "فترات الرواتب", "Payroll Periods", 3, "/payroll/periods"),
            Leaf(payrollGroup, "PAY_PAYROLL_RUNS", "تشغيلات الرواتب", "Payroll Runs", 4, "/payroll/runs"),
            Leaf(payrollGroup, "PAY_PAYSLIPS", "قسائم الرواتب", "Payslips", 5, "/payroll/payslips"),
            Leaf(payrollGroup, "PAY_TIPS_DISTRIBUTION", "توزيع البقشيش", "Tips Distribution", 6, "/payroll/tips-distribution"),
            Leaf(payrollGroup, "PAY_LEGAL_TABLES", "الجداول القانونية", "Legal Tables", 7, "/payroll/legal-tables"),

            settingsGroup,
            Leaf(settingsGroup, "SETTINGS_CODING_RULES", "إعدادات الشاشات", "Screen Settings", 1, "/settings/coding-rules"),
            Leaf(settingsGroup, "SETTINGS_COMPANIES", "الشركات", "Companies", 2, "/settings/companies"),
            Leaf(settingsGroup, "SETTINGS_CURRENCIES", "العملات", "Currencies", 3, "/settings/currencies"),
            // Settings & Permissions (phase 2). Existing databases got these from migration SettingsPermissionsPhase2.
            Leaf(settingsGroup, "SETTINGS_USERS", "المستخدمون", "Users", 4, "/settings/users"),
            Leaf(settingsGroup, "SETTINGS_ROLES", "الأدوار والصلاحيات", "Roles & Permissions", 5, "/settings/roles"),
            Leaf(settingsGroup, "SETTINGS_SESSIONS", "الجلسات النشطة", "Active Sessions", 6, "/settings/sessions"),
            Leaf(settingsGroup, "SETTINGS_LOGIN_ATTEMPTS", "محاولات الدخول", "Login Attempts", 7, "/settings/login-attempts"),
            Leaf(settingsGroup, "SETTINGS_AUDIT_LOG", "سجل المراجعة", "Audit Log", 8, "/settings/audit-log"),
            Leaf(settingsGroup, "SETTINGS_SECURITY", "إعدادات الأمان", "Security Settings", 9, "/settings/security"),
            // Organization lookups (Docs/Implementation/HR-Core-Plan.md §1.1) — filed under Settings,
            // matching the existing precedent for Organization-domain entities (Companies/Currencies
            // above), not a new dedicated group.
            Leaf(settingsGroup, "SETTINGS_COUNTRIES", "الدول", "Countries", 10, "/settings/countries"),
            Leaf(settingsGroup, "SETTINGS_CITIES", "المدن", "Cities", 11, "/settings/cities"),
            Leaf(settingsGroup, "SETTINGS_BANKS", "البنوك", "Banks", 12, "/settings/banks"),
            // Phase 2 — Approval Workflow Engine (00-Project-Overview.md §12). Filed under Settings
            // like SETTINGS_CODING_RULES above (another cross-module engine's settings screen);
            // APPROVAL_MY_PENDING is a personal worklist, not a settings screen, but has no other
            // natural group yet — same reasoning as HR_HIRING's placement in 1.5.5.
            Leaf(settingsGroup, "SETTINGS_APPROVAL_WORKFLOWS", "سلاسل الاعتماد", "Approval Workflows", 13, "/settings/approval-workflows"),
            Leaf(settingsGroup, "APPROVAL_MY_PENDING", "بانتظار اعتمادي", "My Pending Approvals", 14, "/approvals/pending")
        ];
    }
}
