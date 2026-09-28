using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Payroll;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction so Application-layer command/query handlers depend only on this
/// interface, never on the concrete EF Core AppDbContext (which lives in Infrastructure).
/// Implemented by AppDbContext; registered in DI as the same scoped instance so every handler
/// and IPostingService share one change tracker/transaction per request.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<CostCenterDimension> CostCenterDimensions { get; }
    DbSet<CostCenterDimensionValue> CostCenterDimensionValues { get; }
    DbSet<AccountDimensionLink> AccountDimensionLinks { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<JournalEntryLineDimensionValue> JournalEntryLineDimensionValues { get; }
    DbSet<Voucher> Vouchers { get; }
    DbSet<TreasuryTransfer> TreasuryTransfers { get; }
    DbSet<CustodyRegister> CustodyRegisters { get; }
    DbSet<CustodySettlement> CustodySettlements { get; }
    DbSet<CustodySettlementLine> CustodySettlementLines { get; }
    DbSet<CashReconciliation> CashReconciliations { get; }
    DbSet<CashReconciliationDenomination> CashReconciliationDenominations { get; }
    DbSet<BankReconciliationRun> BankReconciliationRuns { get; }
    DbSet<BankReconciliationLine> BankReconciliationLines { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<CompanyAccountMapping> CompanyAccountMappings { get; }
    DbSet<Habbak.ERP.Domain.Posting.PostingTemplate> PostingTemplates { get; }
    DbSet<Habbak.ERP.Domain.Posting.PostingTemplateLine> PostingTemplateLines { get; }
    DbSet<Habbak.ERP.Domain.Posting.PostingTemplateLineCostCenter> PostingTemplateLineCostCenters { get; }
    DbSet<Habbak.ERP.Domain.Posting.JournalEntryTemplateSnapshot> JournalEntryTemplateSnapshots { get; }
    DbSet<Habbak.ERP.Domain.Posting.PostingFailure> PostingFailures { get; }
    DbSet<PeriodCloseChecklistItem> PeriodCloseChecklistItems { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<AccountOpeningBalanceBatch> AccountOpeningBalanceBatches { get; }
    DbSet<AccountOpeningBalanceLine> AccountOpeningBalanceLines { get; }

    // Approval Workflow Engine (Docs/Modules/00-Project-Overview.md §12, Phase 2)
    DbSet<Screen> Screens { get; }
    DbSet<Habbak.ERP.Domain.Approvals.ApprovalWorkflow> ApprovalWorkflows { get; }
    DbSet<Habbak.ERP.Domain.Approvals.ApprovalWorkflowStep> ApprovalWorkflowSteps { get; }
    DbSet<Habbak.ERP.Domain.Approvals.ApprovalStepApprover> ApprovalStepApprovers { get; }
    DbSet<Habbak.ERP.Domain.Approvals.ApprovalWorkflowAssignment> ApprovalWorkflowAssignments { get; }
    DbSet<Habbak.ERP.Domain.Approvals.ApprovalInstance> ApprovalInstances { get; }
    DbSet<Habbak.ERP.Domain.Approvals.ApprovalAction> ApprovalActions { get; }

    // Minimal In-App Notifications (Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5)
    DbSet<Notification> Notifications { get; }

    // Organization
    DbSet<Company> Companies { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Country> Countries { get; }
    DbSet<City> Cities { get; }
    DbSet<Nationality> Nationalities { get; }
    DbSet<Bank> Banks { get; }

    // HR Core (Docs/Implementation/HR-Core-Plan.md §1.1)
    DbSet<JobGrade> JobGrades { get; }
    DbSet<JobPosition> JobPositions { get; }
    DbSet<OrgUnit> OrgUnits { get; }
    DbSet<EmployeeDocumentType> EmployeeDocumentTypes { get; }
    DbSet<RelationshipType> RelationshipTypes { get; }
    DbSet<MilitaryStatus> MilitaryStatuses { get; }
    DbSet<QualificationType> QualificationTypes { get; }
    DbSet<TerminationReason> TerminationReasons { get; }
    DbSet<InsuranceOffice> InsuranceOffices { get; }
    DbSet<Employee> Employees { get; }
    DbSet<EmployeePersonalData> EmployeePersonalDataRows { get; }
    DbSet<EmploymentContract> EmploymentContracts { get; }
    DbSet<EmploymentContractLine> EmploymentContractLines { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }
    DbSet<EmployeeCertification> EmployeeCertifications { get; }
    DbSet<HrSettings> HrSettingsRows { get; }

    // Attendance & Leave (Docs/Implementation/HR-MASTER-PLAN.md §Phase 3)
    DbSet<Habbak.ERP.Domain.Attendance.WorkShiftDefinition> WorkShiftDefinitions { get; }
    DbSet<Habbak.ERP.Domain.Attendance.ShiftSchedule> ShiftSchedules { get; }
    DbSet<Habbak.ERP.Domain.Attendance.TimeEntry> TimeEntries { get; }
    DbSet<Habbak.ERP.Domain.Attendance.Attendance> Attendances { get; }
    DbSet<Habbak.ERP.Domain.Attendance.LeaveType> LeaveTypes { get; }
    DbSet<Habbak.ERP.Domain.Attendance.LeaveBalance> LeaveBalances { get; }
    DbSet<Habbak.ERP.Domain.Attendance.LeaveBalanceHistory> LeaveBalanceHistories { get; }
    DbSet<Habbak.ERP.Domain.Attendance.LeaveRequest> LeaveRequests { get; }
    DbSet<Habbak.ERP.Domain.Attendance.Holiday> Holidays { get; }
    DbSet<Habbak.ERP.Domain.Attendance.OvertimeRequest> OvertimeRequests { get; }
    DbSet<Habbak.ERP.Domain.Attendance.EmployeeWeeklyRestDays> EmployeeWeeklyRestDays { get; }

    // Phase 3B — تكامل أجهزة البصمة (Docs/Implementation/Phase-3B-Research.md).
    DbSet<Habbak.ERP.Domain.Attendance.AttendanceDevice> AttendanceDevices { get; }
    DbSet<Habbak.ERP.Domain.Attendance.AttendanceDeviceLog> AttendanceDeviceLogs { get; }
    DbSet<Habbak.ERP.Domain.Attendance.EmployeeDeviceMapping> EmployeeDeviceMappings { get; }
    DbSet<Habbak.ERP.Domain.Attendance.RawPunch> RawPunches { get; }

    // Phase 4 — Payroll legal tables (Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.2).
    DbSet<MinimumWage> MinimumWages { get; }
    DbSet<SocialInsuranceRate> SocialInsuranceRates { get; }
    DbSet<InsurableWageLimit> InsurableWageLimits { get; }
    DbSet<PayrollTaxBracketSet> PayrollTaxBracketSets { get; }
    DbSet<PayrollTaxBracket> PayrollTaxBrackets { get; }
    DbSet<MartyrsFundRate> MartyrsFundRates { get; }
    DbSet<OvertimeRate> OvertimeRates { get; }
    DbSet<LeaveEntitlementRule> LeaveEntitlementRules { get; }
    DbSet<PenaltyDeductionCap> PenaltyDeductionCaps { get; }
    DbSet<NoticePeriodRule> NoticePeriodRules { get; }
    DbSet<EndOfServicePolicy> EndOfServicePolicies { get; }

    // Phase 4 — Payroll core (Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.3).
    DbSet<SalaryComponent> SalaryComponents { get; }
    DbSet<SalaryStructure> SalaryStructures { get; }
    DbSet<SalaryStructureLine> SalaryStructureLines { get; }
    DbSet<EmployeeSalary> EmployeeSalaries { get; }
    DbSet<PayrollPeriod> PayrollPeriods { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollLine> PayrollLines { get; }
    DbSet<Payslip> Payslips { get; }
    DbSet<EmployeeTaxProfile> EmployeeTaxProfiles { get; }
    DbSet<TipsDistribution> TipsDistributions { get; }
    DbSet<TipsDistributionLine> TipsDistributionLines { get; }

    // System-wide (00-System-Wide-Corrections-01.md, sections 3-4)
    DbSet<MenuItem> MenuItems { get; }
    DbSet<FieldLabel> FieldLabels { get; }
    DbSet<CodingRule> CodingRules { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<ProcessedIdempotencyKey> ProcessedIdempotencyKeys { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.1)
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<ItemGroup> ItemGroups { get; }
    DbSet<POSCategory> POSCategories { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Item> Items { get; }
    DbSet<ItemUnitConversion> ItemUnitConversions { get; }
    DbSet<ItemWarehouseSettings> ItemWarehouseSettings { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.2)
    DbSet<StockBalance> StockBalances { get; }
    DbSet<StockTransaction> StockTransactions { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.3)
    DbSet<WarehouseDocument> WarehouseDocuments { get; }
    DbSet<WarehouseDocumentLine> WarehouseDocumentLines { get; }
    DbSet<CustodyOfficer> CustodyOfficers { get; }

    // Fixed assets & maintenance (08-Module-Maintenance-FixedAssets).
    DbSet<FixedAssetCategory> FixedAssetCategories { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<DepreciationSchedule> DepreciationSchedules { get; }
    DbSet<DepreciationRun> DepreciationRuns { get; }
    DbSet<AssetTransfer> AssetTransfers { get; }
    DbSet<AssetDisposal> AssetDisposals { get; }
    DbSet<AssetPhysicalCount> AssetPhysicalCounts { get; }
    DbSet<AssetPhysicalCountLine> AssetPhysicalCountLines { get; }
    DbSet<AssetSettings> AssetSettingsRows { get; }
    DbSet<MaintenanceCategory> MaintenanceCategories { get; }
    DbSet<MaintenanceIssue> MaintenanceIssues { get; }
    DbSet<MaintenanceRequest> MaintenanceRequests { get; }
    DbSet<MaintenanceSparePart> MaintenanceSpareParts { get; }
    DbSet<MaintenanceSchedule> MaintenanceSchedules { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.4)
    DbSet<BranchRequest> BranchRequests { get; }
    DbSet<BranchRequestLine> BranchRequestLines { get; }
    DbSet<BranchItemLimit> BranchItemLimits { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.6)
    DbSet<Recipe> Recipes { get; }
    DbSet<RecipeLine> RecipeLines { get; }
    DbSet<ProductionOrder> ProductionOrders { get; }
    DbSet<WasteRecord> WasteRecords { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.5)
    DbSet<InventoryCount> InventoryCounts { get; }
    DbSet<InventoryCountLine> InventoryCountLines { get; }

    // Inventory & Manufacturing (02-Module-Inventory-Manufacturing.md, section 2.7)
    DbSet<ProductionSalesModeSetting> ProductionSalesModeSettings { get; }
    DbSet<ShortagePolicy> ShortagePolicies { get; }
    DbSet<InventorySettings> InventorySettingsRows { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.1)
    DbSet<Supplier> Suppliers { get; }

    // Purchasing (03-Module-Purchasing.md, section 2.3)
    DbSet<PurchaseCycleSettings> PurchaseCycleSettingsRows { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.2)
    DbSet<PurchaseRequest> PurchaseRequests { get; }
    DbSet<PurchaseRequestLine> PurchaseRequestLines { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.4)
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.6)
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.5)
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }
    DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.9)
    DbSet<SupplierPriceHistory> SupplierPriceHistories { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.8)
    DbSet<PurchaseExpense> PurchaseExpenses { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.7)
    DbSet<PurchaseReturn> PurchaseReturns { get; }
    DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations { get; }
    DbSet<PurchaseReturnLine> PurchaseReturnLines { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.10)
    DbSet<SupplierContract> SupplierContracts { get; }
    DbSet<ContractItem> ContractItems { get; }

    // Purchasing (03-Module-Purchasing.md, section 8, screen #12)
    DbSet<SupplierEvaluation> SupplierEvaluations { get; }

    // Purchasing (03-Module-Purchasing.md, section 4.3)
    DbSet<RequestForQuotation> RequestsForQuotation { get; }
    DbSet<RFQLine> RFQLines { get; }
    DbSet<RFQSupplier> RFQSuppliers { get; }
    DbSet<RFQSupplierQuote> RFQSupplierQuotes { get; }

    // Sales (04-Module-Sales.md, section 2.1)
    DbSet<Customer> Customers { get; }
    DbSet<LoyaltyTier> LoyaltyTiers { get; }
    DbSet<LoyaltyProgramSettings> LoyaltyProgramSettingsRows { get; }
    DbSet<LoyaltyTransaction> LoyaltyTransactions { get; }

    // Sales (04-Module-Sales.md, section 2.2)
    DbSet<PriceList> PriceLists { get; }
    DbSet<PriceListBranch> PriceListBranches { get; }
    DbSet<PriceListLine> PriceListLines { get; }
    DbSet<Discount> Discounts { get; }

    // Sales (04-Module-Sales.md, section 2.3)
    DbSet<SalesQuote> SalesQuotes { get; }
    DbSet<SalesQuoteLine> SalesQuoteLines { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }

    // Sales (04-Module-Sales.md, section 2.4)
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceLine> SalesInvoiceLines { get; }
    DbSet<DeliveryOrder> DeliveryOrders { get; }
    DbSet<DeliveryOrderLine> DeliveryOrderLines { get; }
    DbSet<SalesReturn> SalesReturns { get; }
    DbSet<SalesReturnLine> SalesReturnLines { get; }

    // Sales (04-Module-Sales.md, section 2.6)
    DbSet<SalesCycleSettings> SalesCycleSettingsRows { get; }

    // POS & Shifts (05-Module-POS-Shifts.md, section 2.1)
    DbSet<POSTerminal> POSTerminals { get; }
    DbSet<BranchPOSSettings> BranchPOSSettingsRows { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<ShiftDenominationCount> ShiftDenominationCounts { get; }
    DbSet<ShiftAssignment> ShiftAssignments { get; }

    // POS & Shifts (05-Module-POS-Shifts.md, section 2.2)
    DbSet<Table> Tables { get; }
    DbSet<Check> Checks { get; }
    DbSet<CheckLine> CheckLines { get; }
    DbSet<CheckLineVoid> CheckLineVoids { get; }

    // POS & Shifts (05-Module-POS-Shifts.md, section 2.3)
    DbSet<POSPaymentMethodConfig> POSPaymentMethodConfigs { get; }
    DbSet<POSInvoice> POSInvoices { get; }
    DbSet<POSInvoiceLine> POSInvoiceLines { get; }
    DbSet<POSPayment> POSPayments { get; }
    DbSet<DrawerMovement> DrawerMovements { get; }
    DbSet<DrawerExpense> DrawerExpenses { get; }
    DbSet<POSReturn> POSReturns { get; }
    DbSet<POSReturnLine> POSReturnLines { get; }
    DbSet<DeliveryPlatformOrder> DeliveryPlatformOrders { get; }
    DbSet<QRTicket> QRTickets { get; }
    DbSet<BlendType> BlendTypes { get; }
    DbSet<QRTicketLine> QRTicketLines { get; }

    // Settings & Permissions (phase 1)
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserRecoveryCode> UserRecoveryCodes { get; }
    DbSet<LoginAttempt> LoginAttempts { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserScope> UserScopes { get; }
    DbSet<ScreenPermission> ScreenPermissions { get; }
    DbSet<FieldPermission> FieldPermissions { get; }
    DbSet<ButtonPermission> ButtonPermissions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemSettings> SystemSettingsRows { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// For the rare command that needs two saves to succeed or fail together — e.g. retiring a
    /// template version and inserting its successor, which a filtered unique index forbids from
    /// ever coexisting as "current" even for the length of one statement.
    /// </summary>
    Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Exposed for optimistic-concurrency checks (setting RowVersion's OriginalValue before an update).</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
