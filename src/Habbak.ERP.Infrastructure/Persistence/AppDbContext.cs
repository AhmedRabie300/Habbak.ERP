using System.Linq.Expressions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Habbak.ERP.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the whole Modular Monolith (00-Project-Overview.md, section 8.2).
/// Applies two Global Query Filters generically, by reflection, to every entity that opts in via
/// a marker interface: IsDeleted == false (section 16) and CompanyId == current company
/// (section 3). This is the FIRST line of defense against cross-company leaks — the mandatory,
/// independent second line lives in the API layer (section 3, last bullet), not here.
/// </summary>
public class AppDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentCompanyContext? _currentCompanyContext;
    private readonly IRequestInfo? _requestInfo;
    private readonly Habbak.ERP.Application.Settings.Access.IAuditFieldPolicy? _auditFieldPolicy;
    private readonly ISecretProtector _piiProtector;
    private bool _writingAudit;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentCompanyContext? currentCompanyContext = null,
        IRequestInfo? requestInfo = null,
        Habbak.ERP.Application.Settings.Access.IAuditFieldPolicy? auditFieldPolicy = null,
        [FromKeyedServices("HR.PII")] ISecretProtector? piiProtector = null)
        : base(options)
    {
        _currentCompanyContext = currentCompanyContext;
        _requestInfo = requestInfo;
        _auditFieldPolicy = auditFieldPolicy;
        // Falls back to a pass-through, never-encrypting protector when none is supplied — every one
        // of the ~380 pre-existing tests across the solution constructs AppDbContext directly with
        // none of these optional parameters, and OnModelCreating must still build a valid model for
        // them (it configures every entity's conversions regardless of which ones a given test
        // actually queries). Real requests always get the keyed "HR.PII" PiiSecretProtector via DI
        // (Infrastructure/DependencyInjection.cs) — this fallback is never reachable in production.
        _piiProtector = piiProtector ?? new NoOpSecretProtector();
    }

    private sealed class NoOpSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string? Unprotect(string protectedValue) => protectedValue;
        public string ProtectFor(string plaintext, TimeSpan lifetime) => plaintext;
        public string? UnprotectTimed(string token) => token;
    }

    /// <summary>
    /// EF Core caches the compiled model per DbContext CLR type by default (Microsoft.EntityFrameworkCore.Infrastructure.ModelCacheKeyFactory),
    /// but OnModelCreating below closes over _piiProtector to build EmployeePersonalData's
    /// EncryptedStringConverter — so without this, whichever AppDbContext instance happens to build
    /// the model FIRST in the process (real protector, or the NoOp fallback most of the ~380
    /// pre-existing tests get) silently wins that converter for every other instance for the rest of
    /// the process, regardless of what protector THEY were given. Caught via a real test failure: the
    /// encryption round-trip test passed in isolation but failed inside the full suite, because a
    /// NoOp-protector context built (and cached) the model first. Production is unaffected (one
    /// singleton protector for the app's whole lifetime — see DependencyInjection.cs), but the design
    /// is unsafe in-process wherever more than one AppDbContext with a different protector can exist,
    /// which every test fixture does. Keying the cache by protector instance identity (not just type)
    /// keeps every distinct protector — including two different real ones, e.g. across two ephemeral
    /// test DataProtectionProviders — from ever sharing a converter that isn't theirs.
    /// </summary>
    private sealed class PiiProtectorModelCacheKeyFactory : Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory
    {
        public object Create(Microsoft.EntityFrameworkCore.DbContext context, bool designTime) =>
            context is AppDbContext appDbContext
                ? (context.GetType(), Identity: appDbContext._piiProtector, designTime)
                : (context.GetType(), designTime);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, PiiProtectorModelCacheKeyFactory>();

    /// <summary>
    /// Referenced (via Expression.Constant(this)) from the generated query filters below. Null — so
    /// a company-scoped query matches nothing — when the request has no signed-in user (login,
    /// token refresh), rather than throwing.
    /// </summary>
    public long? CurrentCompanyId
    {
        get
        {
            try
            {
                return _currentCompanyContext?.CompanyId;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The branch the session is limited to (UserScope.BranchId, carried in the token), or null for
    /// a company-wide session. Referenced from the branch query filters below.
    /// </summary>
    public long? CurrentBranchId
    {
        get
        {
            try
            {
                return _currentCompanyContext?.BranchId;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The Employee linked to the current session's user (Employee.UserId, once the HR module
    /// exists), or null when the user has none — always null before then (no request populates the
    /// EmployeeId claim yet). Referenced from the IEmployeeScopedEntity query filter below
    /// (Docs/Implementation/HR-Core-Plan.md §0.1).
    /// </summary>
    public long? CurrentEmployeeId
    {
        get
        {
            try
            {
                return _currentCompanyContext?.EmployeeId;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    // Accounting module
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CostCenterDimension> CostCenterDimensions => Set<CostCenterDimension>();
    public DbSet<CostCenterDimensionValue> CostCenterDimensionValues => Set<CostCenterDimensionValue>();
    public DbSet<AccountDimensionLink> AccountDimensionLinks => Set<AccountDimensionLink>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<JournalEntryLineDimensionValue> JournalEntryLineDimensionValues => Set<JournalEntryLineDimensionValue>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<TreasuryTransfer> TreasuryTransfers => Set<TreasuryTransfer>();
    public DbSet<CustodyRegister> CustodyRegisters => Set<CustodyRegister>();
    public DbSet<CustodySettlement> CustodySettlements => Set<CustodySettlement>();
    public DbSet<CustodySettlementLine> CustodySettlementLines => Set<CustodySettlementLine>();
    public DbSet<CashReconciliation> CashReconciliations => Set<CashReconciliation>();
    public DbSet<CashReconciliationDenomination> CashReconciliationDenominations => Set<CashReconciliationDenomination>();
    public DbSet<BankReconciliationRun> BankReconciliationRuns => Set<BankReconciliationRun>();
    public DbSet<BankReconciliationLine> BankReconciliationLines => Set<BankReconciliationLine>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<CompanyAccountMapping> CompanyAccountMappings => Set<CompanyAccountMapping>();
    public DbSet<Habbak.ERP.Domain.Posting.PostingTemplate> PostingTemplates => Set<Habbak.ERP.Domain.Posting.PostingTemplate>();
    public DbSet<Habbak.ERP.Domain.Posting.PostingTemplateLine> PostingTemplateLines => Set<Habbak.ERP.Domain.Posting.PostingTemplateLine>();
    public DbSet<Habbak.ERP.Domain.Posting.PostingTemplateLineCostCenter> PostingTemplateLineCostCenters => Set<Habbak.ERP.Domain.Posting.PostingTemplateLineCostCenter>();
    public DbSet<Habbak.ERP.Domain.Posting.JournalEntryTemplateSnapshot> JournalEntryTemplateSnapshots => Set<Habbak.ERP.Domain.Posting.JournalEntryTemplateSnapshot>();
    public DbSet<Habbak.ERP.Domain.Posting.PostingFailure> PostingFailures => Set<Habbak.ERP.Domain.Posting.PostingFailure>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    public DbSet<PeriodCloseChecklistItem> PeriodCloseChecklistItems => Set<PeriodCloseChecklistItem>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<AccountOpeningBalanceBatch> AccountOpeningBalanceBatches => Set<AccountOpeningBalanceBatch>();
    public DbSet<AccountOpeningBalanceLine> AccountOpeningBalanceLines => Set<AccountOpeningBalanceLine>();

    // Approval Workflow Engine (Docs/Modules/00-Project-Overview.md §12, Phase 2)
    public DbSet<Screen> Screens => Set<Screen>();
    public DbSet<Habbak.ERP.Domain.Approvals.ApprovalWorkflow> ApprovalWorkflows => Set<Habbak.ERP.Domain.Approvals.ApprovalWorkflow>();
    public DbSet<Habbak.ERP.Domain.Approvals.ApprovalWorkflowStep> ApprovalWorkflowSteps => Set<Habbak.ERP.Domain.Approvals.ApprovalWorkflowStep>();
    public DbSet<Habbak.ERP.Domain.Approvals.ApprovalStepApprover> ApprovalStepApprovers => Set<Habbak.ERP.Domain.Approvals.ApprovalStepApprover>();
    public DbSet<Habbak.ERP.Domain.Approvals.ApprovalWorkflowAssignment> ApprovalWorkflowAssignments => Set<Habbak.ERP.Domain.Approvals.ApprovalWorkflowAssignment>();
    public DbSet<Habbak.ERP.Domain.Approvals.ApprovalInstance> ApprovalInstances => Set<Habbak.ERP.Domain.Approvals.ApprovalInstance>();
    public DbSet<Habbak.ERP.Domain.Approvals.ApprovalAction> ApprovalActions => Set<Habbak.ERP.Domain.Approvals.ApprovalAction>();

    // Minimal In-App Notifications (Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5)
    public DbSet<Notification> Notifications => Set<Notification>();

    // Organization
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Nationality> Nationalities => Set<Nationality>();
    public DbSet<Bank> Banks => Set<Bank>();

    // HR Core (Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1 — lookups only; Employee and its
    // dependents follow in Batches B2/B3).
    public DbSet<JobGrade> JobGrades => Set<JobGrade>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<EmployeeDocumentType> EmployeeDocumentTypes => Set<EmployeeDocumentType>();
    public DbSet<RelationshipType> RelationshipTypes => Set<RelationshipType>();
    public DbSet<MilitaryStatus> MilitaryStatuses => Set<MilitaryStatus>();
    public DbSet<QualificationType> QualificationTypes => Set<QualificationType>();
    public DbSet<TerminationReason> TerminationReasons => Set<TerminationReason>();
    public DbSet<InsuranceOffice> InsuranceOffices => Set<InsuranceOffice>();

    // HR Core, Batch B2.
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeePersonalData> EmployeePersonalDataRows => Set<EmployeePersonalData>();

    // HR Core, Batch B3 — the Employee "followers" (IEmployeeScopedEntity's first real use).
    public DbSet<EmploymentContract> EmploymentContracts => Set<EmploymentContract>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeCertification> EmployeeCertifications => Set<EmployeeCertification>();
    public DbSet<HrSettings> HrSettingsRows => Set<HrSettings>();

    // Attendance & Leave (Docs/Implementation/HR-MASTER-PLAN.md §Phase 3)
    public DbSet<Habbak.ERP.Domain.Attendance.WorkShiftDefinition> WorkShiftDefinitions => Set<Habbak.ERP.Domain.Attendance.WorkShiftDefinition>();
    public DbSet<Habbak.ERP.Domain.Attendance.ShiftSchedule> ShiftSchedules => Set<Habbak.ERP.Domain.Attendance.ShiftSchedule>();
    public DbSet<Habbak.ERP.Domain.Attendance.TimeEntry> TimeEntries => Set<Habbak.ERP.Domain.Attendance.TimeEntry>();
    public DbSet<Habbak.ERP.Domain.Attendance.Attendance> Attendances => Set<Habbak.ERP.Domain.Attendance.Attendance>();
    public DbSet<Habbak.ERP.Domain.Attendance.LeaveType> LeaveTypes => Set<Habbak.ERP.Domain.Attendance.LeaveType>();
    public DbSet<Habbak.ERP.Domain.Attendance.LeaveBalance> LeaveBalances => Set<Habbak.ERP.Domain.Attendance.LeaveBalance>();
    public DbSet<Habbak.ERP.Domain.Attendance.LeaveBalanceHistory> LeaveBalanceHistories => Set<Habbak.ERP.Domain.Attendance.LeaveBalanceHistory>();
    public DbSet<Habbak.ERP.Domain.Attendance.LeaveRequest> LeaveRequests => Set<Habbak.ERP.Domain.Attendance.LeaveRequest>();
    public DbSet<Habbak.ERP.Domain.Attendance.Holiday> Holidays => Set<Habbak.ERP.Domain.Attendance.Holiday>();
    public DbSet<Habbak.ERP.Domain.Attendance.OvertimeRequest> OvertimeRequests => Set<Habbak.ERP.Domain.Attendance.OvertimeRequest>();
    // Remarks8 Item 6 — Pattern الراحة الأسبوعية (Phase 3 Amendment).
    public DbSet<Habbak.ERP.Domain.Attendance.EmployeeWeeklyRestDays> EmployeeWeeklyRestDays => Set<Habbak.ERP.Domain.Attendance.EmployeeWeeklyRestDays>();

    // Phase 3B — تكامل أجهزة البصمة (Docs/Implementation/Phase-3B-Research.md).
    public DbSet<Habbak.ERP.Domain.Attendance.AttendanceDevice> AttendanceDevices => Set<Habbak.ERP.Domain.Attendance.AttendanceDevice>();
    public DbSet<Habbak.ERP.Domain.Attendance.AttendanceDeviceLog> AttendanceDeviceLogs => Set<Habbak.ERP.Domain.Attendance.AttendanceDeviceLog>();
    public DbSet<Habbak.ERP.Domain.Attendance.EmployeeDeviceMapping> EmployeeDeviceMappings => Set<Habbak.ERP.Domain.Attendance.EmployeeDeviceMapping>();
    public DbSet<Habbak.ERP.Domain.Attendance.RawPunch> RawPunches => Set<Habbak.ERP.Domain.Attendance.RawPunch>();

    // System-wide (00-System-Wide-Corrections-01.md, sections 3-4) — not company-scoped.
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<FieldLabel> FieldLabels => Set<FieldLabel>();
    public DbSet<CodingRule> CodingRules => Set<CodingRule>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ProcessedIdempotencyKey> ProcessedIdempotencyKeys => Set<ProcessedIdempotencyKey>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.1)
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ItemGroup> ItemGroups => Set<ItemGroup>();
    public DbSet<POSCategory> POSCategories => Set<POSCategory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemUnitConversion> ItemUnitConversions => Set<ItemUnitConversion>();
    public DbSet<ItemWarehouseSettings> ItemWarehouseSettings => Set<ItemWarehouseSettings>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.2)
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.3)
    public DbSet<WarehouseDocument> WarehouseDocuments => Set<WarehouseDocument>();
    public DbSet<WarehouseDocumentLine> WarehouseDocumentLines => Set<WarehouseDocumentLine>();
    public DbSet<CustodyOfficer> CustodyOfficers => Set<CustodyOfficer>();

    // Fixed assets & maintenance (08-Module-Maintenance-FixedAssets).
    public DbSet<FixedAssetCategory> FixedAssetCategories => Set<FixedAssetCategory>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<DepreciationSchedule> DepreciationSchedules => Set<DepreciationSchedule>();
    public DbSet<DepreciationRun> DepreciationRuns => Set<DepreciationRun>();
    public DbSet<AssetTransfer> AssetTransfers => Set<AssetTransfer>();
    public DbSet<AssetDisposal> AssetDisposals => Set<AssetDisposal>();
    public DbSet<AssetPhysicalCount> AssetPhysicalCounts => Set<AssetPhysicalCount>();
    public DbSet<AssetPhysicalCountLine> AssetPhysicalCountLines => Set<AssetPhysicalCountLine>();
    public DbSet<AssetSettings> AssetSettingsRows => Set<AssetSettings>();
    public DbSet<MaintenanceCategory> MaintenanceCategories => Set<MaintenanceCategory>();
    public DbSet<MaintenanceIssue> MaintenanceIssues => Set<MaintenanceIssue>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceSparePart> MaintenanceSpareParts => Set<MaintenanceSparePart>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.4)
    public DbSet<BranchRequest> BranchRequests => Set<BranchRequest>();
    public DbSet<BranchRequestLine> BranchRequestLines => Set<BranchRequestLine>();
    public DbSet<BranchItemLimit> BranchItemLimits => Set<BranchItemLimit>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.6)
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeLine> RecipeLines => Set<RecipeLine>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<WasteRecord> WasteRecords => Set<WasteRecord>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.5)
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<InventoryCountLine> InventoryCountLines => Set<InventoryCountLine>();

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.7)
    public DbSet<ProductionSalesModeSetting> ProductionSalesModeSettings => Set<ProductionSalesModeSetting>();
    public DbSet<ShortagePolicy> ShortagePolicies => Set<ShortagePolicy>();
    public DbSet<InventorySettings> InventorySettingsRows => Set<InventorySettings>();

    // Purchasing (03-Module-Purchasing.md, section 4.1)
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    // Purchasing (03-Module-Purchasing.md, section 2.3)
    public DbSet<PurchaseCycleSettings> PurchaseCycleSettingsRows => Set<PurchaseCycleSettings>();

    // Purchasing (03-Module-Purchasing.md, section 4.2)
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestLine> PurchaseRequestLines => Set<PurchaseRequestLine>();

    // Purchasing (03-Module-Purchasing.md, section 4.4)
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    // Purchasing (03-Module-Purchasing.md, section 4.6)
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();

    // Purchasing (03-Module-Purchasing.md, section 4.5)
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();

    // Purchasing (03-Module-Purchasing.md, section 4.9)
    public DbSet<SupplierPriceHistory> SupplierPriceHistories => Set<SupplierPriceHistory>();

    // Purchasing (03-Module-Purchasing.md, section 4.8)
    public DbSet<PurchaseExpense> PurchaseExpenses => Set<PurchaseExpense>();

    // Purchasing (03-Module-Purchasing.md, section 4.7)
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations => Set<SupplierPaymentAllocation>();
    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();

    // Purchasing (03-Module-Purchasing.md, section 4.10)
    public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();
    public DbSet<ContractItem> ContractItems => Set<ContractItem>();

    // Purchasing (03-Module-Purchasing.md, section 8, screen #12)
    public DbSet<SupplierEvaluation> SupplierEvaluations => Set<SupplierEvaluation>();

    // Purchasing (03-Module-Purchasing.md, section 4.3)
    public DbSet<RequestForQuotation> RequestsForQuotation => Set<RequestForQuotation>();
    public DbSet<RFQLine> RFQLines => Set<RFQLine>();
    public DbSet<RFQSupplier> RFQSuppliers => Set<RFQSupplier>();
    public DbSet<RFQSupplierQuote> RFQSupplierQuotes => Set<RFQSupplierQuote>();

    // Sales (04-Module-Sales.md, section 2.1)
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();
    public DbSet<LoyaltyProgramSettings> LoyaltyProgramSettingsRows => Set<LoyaltyProgramSettings>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    // Sales (04-Module-Sales.md, section 2.2)
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListBranch> PriceListBranches => Set<PriceListBranch>();
    public DbSet<PriceListLine> PriceListLines => Set<PriceListLine>();
    public DbSet<Discount> Discounts => Set<Discount>();

    // Sales (04-Module-Sales.md, section 2.3)
    public DbSet<SalesQuote> SalesQuotes => Set<SalesQuote>();
    public DbSet<SalesQuoteLine> SalesQuoteLines => Set<SalesQuoteLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    // Sales (04-Module-Sales.md, section 2.4)
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<DeliveryOrderLine> DeliveryOrderLines => Set<DeliveryOrderLine>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnLine> SalesReturnLines => Set<SalesReturnLine>();

    // Sales (04-Module-Sales.md, section 2.6)
    public DbSet<SalesCycleSettings> SalesCycleSettingsRows => Set<SalesCycleSettings>();

    // POS & Shifts (05-Module-POS-Shifts.md, section 2.1)
    public DbSet<POSTerminal> POSTerminals => Set<POSTerminal>();
    public DbSet<BranchPOSSettings> BranchPOSSettingsRows => Set<BranchPOSSettings>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftDenominationCount> ShiftDenominationCounts => Set<ShiftDenominationCount>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

    // POS & Shifts (05-Module-POS-Shifts.md, section 2.2)
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Check> Checks => Set<Check>();
    public DbSet<CheckLine> CheckLines => Set<CheckLine>();
    public DbSet<CheckLineVoid> CheckLineVoids => Set<CheckLineVoid>();

    // POS & Shifts (05-Module-POS-Shifts.md, section 2.3)
    public DbSet<POSPaymentMethodConfig> POSPaymentMethodConfigs => Set<POSPaymentMethodConfig>();
    public DbSet<POSInvoice> POSInvoices => Set<POSInvoice>();
    public DbSet<POSInvoiceLine> POSInvoiceLines => Set<POSInvoiceLine>();
    public DbSet<POSPayment> POSPayments => Set<POSPayment>();
    public DbSet<DrawerMovement> DrawerMovements => Set<DrawerMovement>();
    public DbSet<DrawerExpense> DrawerExpenses => Set<DrawerExpense>();
    public DbSet<POSReturn> POSReturns => Set<POSReturn>();
    public DbSet<POSReturnLine> POSReturnLines => Set<POSReturnLine>();
    public DbSet<DeliveryPlatformOrder> DeliveryPlatformOrders => Set<DeliveryPlatformOrder>();
    public DbSet<QRTicket> QRTickets => Set<QRTicket>();
    public DbSet<QRTicketLine> QRTicketLines => Set<QRTicketLine>();
    public DbSet<BlendType> BlendTypes => Set<BlendType>();

    // Settings & Permissions (phase 1). User, RefreshToken and LoginAttempt are system-wide — no
    // company filter; AuditLog has no filter at all.
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<ScreenPermission> ScreenPermissions => Set<ScreenPermission>();
    public DbSet<FieldPermission> FieldPermissions => Set<FieldPermission>();
    public DbSet<ButtonPermission> ButtonPermissions => Set<ButtonPermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuditLogArchive> AuditLogArchives => Set<AuditLogArchive>();
    public DbSet<SystemSettings> SystemSettingsRows => Set<SystemSettings>();

    /// <summary>
    /// The audit log is append-only. Guarded here rather than in the audit interceptor so it holds
    /// for every context — including ones built without interceptors (tests, tooling).
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveWithAuditAsync(acceptAllChangesOnSuccess, sync: true, CancellationToken.None).GetAwaiter().GetResult();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        SaveWithAuditAsync(acceptAllChangesOnSuccess, sync: false, cancellationToken);

    // Types whose rows are themselves a log (or pure plumbing) — auditing them would only log the logging.
    private static readonly HashSet<Type> NotAudited = [typeof(RefreshToken), typeof(LoginAttempt), typeof(ProcessedIdempotencyKey), typeof(UserRecoveryCode)];

    // Bookkeeping columns every row carries; their changes say nothing about what the user did.
    private static readonly HashSet<string> NotAuditedProperties =
    [
        nameof(IAuditableEntity.RowVersion), nameof(IAuditableEntity.CreatedAtUtc), nameof(IAuditableEntity.CreatedBy),
        nameof(IAuditableEntity.UpdatedAtUtc), nameof(IAuditableEntity.UpdatedBy), nameof(IAuditableEntity.DeletedAtUtc),
        nameof(IAuditableEntity.DeletedBy), nameof(User.FailedLoginAttempts), nameof(User.LastLoginAtUtc), nameof(User.TwoFactorLastStep)
    ];

    // Never written in clear text, whoever changes them.
    private static readonly HashSet<string> SecretProperties = [nameof(User.PasswordHash), nameof(User.PasswordSalt), nameof(User.TwoFactorSecret), nameof(User.TwoFactorPendingSecret)];

    /// <summary>
    /// The central audit log (Settings &amp; Permissions phase 2): every create, delete and changed
    /// field of every auditable row, written in the same transaction as the change itself. A create
    /// needs the new row's id, so the log goes in a second save right after the first — inside the
    /// caller's transaction if there is one, otherwise inside one opened here. Only for contexts
    /// that know who is acting (the app, API tests); a bare context built by a test has no one to
    /// attribute changes to and skips it.
    /// </summary>
    private async Task<int> SaveWithAuditAsync(bool acceptAllChangesOnSuccess, bool sync, CancellationToken cancellationToken)
    {
        GuardAuditLogIsAppendOnly();
        GuardBranchScope();

        if (_writingAudit || _currentCompanyContext is null)
        {
            return sync ? base.SaveChanges(acceptAllChangesOnSuccess) : await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        var pending = await CaptureAuditAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return sync ? base.SaveChanges(acceptAllChangesOnSuccess) : await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        var ownTransaction = Database.CurrentTransaction is null
            ? (sync ? Database.BeginTransaction() : await Database.BeginTransactionAsync(cancellationToken))
            : null;
        try
        {
            var result = sync ? base.SaveChanges(acceptAllChangesOnSuccess) : await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            foreach (var (log, entity) in pending)
            {
                log.EntityId ??= entity?.Id;
                AuditLogs.Add(log);
            }

            _writingAudit = true;
            _ = sync ? base.SaveChanges(true) : await base.SaveChangesAsync(true, cancellationToken);

            if (ownTransaction is not null)
            {
                if (sync) ownTransaction.Commit(); else await ownTransaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            _writingAudit = false;
            if (ownTransaction is not null)
            {
                if (sync) ownTransaction.Dispose(); else await ownTransaction.DisposeAsync();
            }
        }
    }

    private async Task<List<(AuditLog Log, IAuditableEntity? Entity)>> CaptureAuditAsync(CancellationToken cancellationToken)
    {
        var pending = new List<(AuditLog, IAuditableEntity?)>();
        var now = DateTime.UtcNow;
        long userId = 0;
        long? contextCompanyId = null, contextBranchId = null;
        try
        {
            userId = _currentCompanyContext!.UserId;
            contextCompanyId = _currentCompanyContext.CompanyId;
            contextBranchId = _currentCompanyContext.BranchId;
        }
        catch (InvalidOperationException)
        {
            // Nobody signed in yet (login): attributed to 0 = the system.
        }

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) || NotAudited.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            var entityType = entry.Entity.GetType().Name;
            var companyId = entry.Entity is ICompanyScopedEntity scoped ? scoped.CompanyId : contextCompanyId is 0 ? null : contextCompanyId;
            var branchId = entry.Entity is IBranchScopedEntity branched ? branched.BranchId : contextBranchId;
            var entityId = entry.State == EntityState.Added ? (long?)null : entry.Entity.Id;
            var userAgent = _requestInfo?.UserAgent is { Length: > 500 } ua ? ua[..500] : _requestInfo?.UserAgent;

            AuditLog Log(AuditActionType action, string? field = null, string? oldValue = null, string? newValue = null) => new()
            {
                CompanyId = companyId,
                BranchId = branchId,
                UserId = userId,
                ActionType = action,
                EntityType = entityType,
                EntityId = entityId,
                FieldName = field,
                OldValue = oldValue,
                NewValue = newValue,
                IpAddress = _requestInfo?.IpAddress,
                UserAgent = userAgent,
                OccurredAtUtc = now
            };

            var softDeleted = entry.State == EntityState.Modified
                              && entry.Property(nameof(IAuditableEntity.IsDeleted)) is { IsModified: true, CurrentValue: true };

            if (entry.State == EntityState.Added)
            {
                pending.Add((Log(AuditActionType.Create), entry.Entity));
                continue;
            }

            if (entry.State == EntityState.Deleted || softDeleted)
            {
                pending.Add((Log(AuditActionType.Delete), null));
                continue;
            }

            foreach (var property in entry.Properties.Where(p => p.IsModified && !NotAuditedProperties.Contains(p.Metadata.Name)))
            {
                var name = property.Metadata.Name;
                var oldValue = Format(property.OriginalValue);
                var newValue = Format(property.CurrentValue);
                if (oldValue == newValue)
                {
                    continue;
                }

                if (SecretProperties.Contains(name))
                {
                    pending.Add((Log(AuditActionType.Update, name, AuditLog.Redacted, AuditLog.Redacted), null));
                }
                else if (FieldPermissionCatalog.IsSensitive(entityType, name))
                {
                    if (_auditFieldPolicy is null || await _auditFieldPolicy.ShouldAuditAsync(entityType, name, cancellationToken))
                    {
                        var change = AuditLog.FieldChange(companyId, userId, entityType, entry.Entity.Id, name, oldValue, newValue, now);
                        change.BranchId = branchId;
                        change.IpAddress = _requestInfo?.IpAddress;
                        change.UserAgent = userAgent;
                        pending.Add((change, null));
                    }
                }
                else
                {
                    pending.Add((Log(AuditActionType.Update, name, oldValue, newValue), null));
                }
            }
        }

        return pending;
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        // Values read back from SQL Server come without a Kind; every DateTime here is UTC, so both sides print alike.
        DateTime d => (d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        byte[] => "[binary]",
        _ => value.ToString() is { Length: > 2000 } text ? text[..2000] : value.ToString()
    };

    private void GuardBranchScope()
    {
        var branch = CurrentBranchId;
        if (branch is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IBranchScopedEntity>())
        {
            var moved = entry.State == EntityState.Modified && entry.Property(nameof(IBranchScopedEntity.BranchId)).IsModified;
            if ((entry.State == EntityState.Added || moved) && entry.Entity.BranchId is { } target && target != branch)
            {
                throw new Habbak.ERP.Application.Common.Exceptions.ForbiddenException(
                    "BRANCH-OUT-OF-SCOPE", "صلاحيتك على فرعك بس — مينفعش تسجّل على فرع تاني.");
            }
        }
    }

    private void GuardAuditLogIsAppendOnly()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<AuditLogArchive>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("AUDIT-LOG-APPEND-ONLY: audit log entries cannot be changed or deleted.");
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Conventions.Replace<Microsoft.EntityFrameworkCore.Metadata.Conventions.ForeignKeyIndexConvention>(
            services => new Configurations.Settings.AuditColumnsForeignKeyIndexConvention(
                services.GetRequiredService<Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure.ProviderConventionSetBuilderDependencies>()));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        Configurations.Settings.UserReferences.Apply(modelBuilder);

        // Docs/Implementation/HR-Core-Plan.md §1.1 (Batch B2) — needs the keyed "HR.PII" protector
        // instance, so it cannot live in EmployeePersonalDataConfiguration (assembly-scanned
        // IEntityTypeConfiguration classes have no constructor-injection path).
        modelBuilder.Entity<EmployeePersonalData>(b =>
        {
            Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter converter =
                new Persistence.Converters.EncryptedStringConverter(_piiProtector);
            b.Property(d => d.NationalIdEncrypted).HasConversion(converter);
            b.Property(d => d.BankIbanEncrypted).HasConversion(converter);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var entityBuilder = modelBuilder.Entity(clrType);

            if (typeof(IAuditableEntity).IsAssignableFrom(clrType))
            {
                // SQL Server auto-generates this on every update — the Optimistic Concurrency
                // token for every sensitive table (00-Project-Overview.md, section 13).
                entityBuilder.Property(nameof(IAuditableEntity.RowVersion)).IsRowVersion();
            }

            if (typeof(ICompanyScopedEntity).IsAssignableFrom(clrType))
            {
                // Mandatory index per section 15 of the Overview; entity-specific configurations
                // extend this into composite indexes (CompanyId, BranchId, Status...) as needed.
                entityBuilder.HasIndex(nameof(ICompanyScopedEntity.CompanyId));
            }

            // Account is the one exception where CompanyId alone cannot decide visibility: a
            // row is visible if it belongs to the current company OR is shared across every
            // company (rule 12). Built here (not in AccountConfiguration) because only an
            // AppDbContext instance — captured below via Expression.Constant(this) — can
            // resolve "the current company" per request.
            if (clrType == typeof(Account))
            {
                entityBuilder.HasQueryFilter(BuildAccountVisibilityFilter());
                continue;
            }

            LambdaExpression? filter = null;

            if (typeof(IAuditableEntity).IsAssignableFrom(clrType))
            {
                filter = BuildSoftDeleteFilter(clrType);
            }

            if (typeof(ICompanyScopedEntity).IsAssignableFrom(clrType))
            {
                var companyFilter = BuildCompanyFilter(clrType);
                filter = filter is null ? companyFilter : CombineAnd(clrType, filter, companyFilter);
            }

            foreach (var branchFilter in BuildBranchFilters(entityType))
            {
                filter = filter is null ? branchFilter : CombineAnd(clrType, filter, branchFilter);
            }

            if (typeof(IEmployeeScopedEntity).IsAssignableFrom(clrType))
            {
                var employeeFilter = BuildEmployeeFilter(clrType);
                filter = filter is null ? employeeFilter : CombineAnd(clrType, filter, employeeFilter);
            }

            if (filter is not null)
            {
                entityBuilder.HasQueryFilter(filter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Branch-level data scope (Settings &amp; Permissions): a session limited to one branch sees that
    /// branch's rows and rows that belong to no branch (company-wide records, central warehouses).
    /// A company-wide session (CurrentBranchId null) sees everything, as before. Three shapes:
    /// <list type="bullet">
    /// <item>IBranchScopedEntity — its own BranchId.</item>
    /// <item>Branch itself — only the session's branch, so every branch picker offers just that one.</item>
    /// <item>A row with a required link to a branch-scoped parent (a journal entry line, a check
    /// line, a stock balance through its warehouse) — the parent's BranchId, one level up. Only
    /// required links: an optional reference (say, to a customer) does not decide who may see a row.</item>
    /// </list>
    /// </summary>
    /// <summary>
    /// Branch-tagged, but shared by every branch: a customer registered at one branch buys, and
    /// spends loyalty points, at all of them. Their BranchId says where they were registered.
    /// </summary>
    private static readonly HashSet<Type> SharedAcrossBranches = [typeof(Customer)];

    private IEnumerable<LambdaExpression> BuildBranchFilters(Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
    {
        var clrType = entityType.ClrType;
        var parameter = Expression.Parameter(clrType, "e");
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentBranchId));
        var noBranchLimit = Expression.Equal(current, Expression.Constant(null, typeof(long?)));

        Expression Visible(Expression branchId)
        {
            var nullable = branchId.Type == typeof(long?) ? branchId : Expression.Convert(branchId, typeof(long?));
            return Expression.OrElse(
                noBranchLimit,
                Expression.OrElse(Expression.Equal(nullable, Expression.Constant(null, typeof(long?))), Expression.Equal(nullable, current)));
        }

        if (clrType == typeof(Branch))
        {
            var id = Expression.Convert(Expression.Property(parameter, nameof(Branch.Id)), typeof(long?));
            yield return Expression.Lambda(Expression.OrElse(noBranchLimit, Expression.Equal(id, current)), parameter);
            yield break;
        }

        if (SharedAcrossBranches.Contains(clrType))
        {
            yield break;
        }

        if (typeof(IBranchScopedEntity).IsAssignableFrom(clrType))
        {
            yield return Expression.Lambda(Visible(Expression.Property(parameter, nameof(IBranchScopedEntity.BranchId))), parameter);
            yield break;
        }

        foreach (var fk in entityType.GetForeignKeys()
                     .Where(fk => fk.IsRequired
                                  && fk.DependentToPrincipal?.PropertyInfo is not null
                                  && fk.PrincipalEntityType != entityType
                                  && typeof(IBranchScopedEntity).IsAssignableFrom(fk.PrincipalEntityType.ClrType)
                                  && !SharedAcrossBranches.Contains(fk.PrincipalEntityType.ClrType)))
        {
            var parent = Expression.Property(parameter, fk.DependentToPrincipal!.PropertyInfo!);
            yield return Expression.Lambda(Visible(Expression.Property(parent, nameof(IBranchScopedEntity.BranchId))), parameter);
        }
    }

    /// <summary>
    /// Builds "e => CurrentEmployeeId == null || e.EmployeeId == this.CurrentEmployeeId" for an
    /// IEmployeeScopedEntity: DataScope.Self (Docs/Implementation/HR-Core-Plan.md §0.1). Unlike
    /// branch, there is no "row belongs to no employee" case to let through — EmployeeId is
    /// non-nullable — so a session with an EmployeeId sees only its own rows, and a session with
    /// none (not in Self scope) sees everything, same as CurrentBranchId null does for branch.
    /// </summary>
    private LambdaExpression BuildEmployeeFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var employeeId = Expression.Convert(Expression.Property(parameter, nameof(IEmployeeScopedEntity.EmployeeId)), typeof(long?));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentEmployeeId));
        var noEmployeeLimit = Expression.Equal(current, Expression.Constant(null, typeof(long?)));
        var body = Expression.OrElse(noEmployeeLimit, Expression.Equal(employeeId, current));
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Builds "e => e.IsDeleted == false" for the given CLR type.</summary>
    private static LambdaExpression BuildSoftDeleteFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var isDeleted = Expression.Property(parameter, nameof(IAuditableEntity.IsDeleted));
        var body = Expression.Equal(isDeleted, Expression.Constant(false));
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Builds "e => e.CompanyId == this.CurrentCompanyId" for the given CLR type.</summary>
    private LambdaExpression BuildCompanyFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var companyId = Expression.Property(parameter, nameof(ICompanyScopedEntity.CompanyId));
        var currentCompanyId = Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId));
        var body = Expression.Equal(companyId, currentCompanyId);
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Builds "a => !a.IsDeleted && (a.CompanyId == this.CurrentCompanyId || a.IsSharedAcrossCompanies)".</summary>
    private Expression<Func<Account, bool>> BuildAccountVisibilityFilter()
    {
        var parameter = Expression.Parameter(typeof(Account), "a");
        var currentCompanyId = Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId));

        var isDeleted = Expression.Not(Expression.Property(parameter, nameof(Account.IsDeleted)));
        var companyMatches = Expression.Equal(Expression.Property(parameter, nameof(Account.CompanyId)), currentCompanyId);
        var isShared = Expression.Property(parameter, nameof(Account.IsSharedAcrossCompanies));
        var body = Expression.AndAlso(isDeleted, Expression.OrElse(companyMatches, isShared));

        return Expression.Lambda<Func<Account, bool>>(body, parameter);
    }

    private static LambdaExpression CombineAnd(Type clrType, LambdaExpression left, LambdaExpression right)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body);
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body);
        var body = Expression.AndAlso(leftBody!, rightBody!);
        return Expression.Lambda(body, parameter);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
