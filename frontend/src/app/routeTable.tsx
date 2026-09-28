import { Navigate, Route } from 'react-router-dom';
import { JournalEntriesListPage } from '../features/accounting/journalEntries/JournalEntriesListPage';
import { JournalEntryEditPage } from '../features/accounting/journalEntries/JournalEntryEditPage';
import { VouchersListPage } from '../features/accounting/vouchers/VouchersListPage';
import { VoucherEditPage } from '../features/accounting/vouchers/VoucherEditPage';
import { DimensionsListPage } from '../features/accounting/dimensions/DimensionsListPage';
import { DimensionEditPage } from '../features/accounting/dimensions/DimensionEditPage';
import { BranchesListPage } from '../features/organization/branches/BranchesListPage';
import { BranchEditPage } from '../features/organization/branches/BranchEditPage';
import { PeriodsListPage } from '../features/accounting/periods/PeriodsListPage';
import { PeriodEditPage } from '../features/accounting/periods/PeriodEditPage';
import { CustodyPage } from '../features/accounting/custody/CustodyPage';
import { CashReconciliationsPage } from '../features/accounting/cashReconciliations/CashReconciliationsPage';
import { BankReconciliationsPage } from '../features/accounting/bankReconciliations/BankReconciliationsPage';
import { ChartOfAccountsPage } from '../features/accounting/chartOfAccounts/ChartOfAccountsPage';
import { ReportsPage } from '../features/accounting/reports/ReportsPage';
import { PaymentMethodsListPage } from '../features/accounting/paymentMethods/PaymentMethodsListPage';
import { PaymentMethodEditPage } from '../features/accounting/paymentMethods/PaymentMethodEditPage';
import { TreasuryTransfersListPage } from '../features/accounting/treasuryTransfers/TreasuryTransfersListPage';
import { TreasuryTransferEditPage } from '../features/accounting/treasuryTransfers/TreasuryTransferEditPage';
import { AccountOpeningBalancesListPage } from '../features/accounting/openingBalances/AccountOpeningBalancesListPage';
import { AccountOpeningBalanceBatchEditPage } from '../features/accounting/openingBalances/AccountOpeningBalanceBatchEditPage';
import { AssetCategoriesListPage, AssetCategoryEditPage } from '../features/fixedAssets/AssetCategoryPages';
import { FixedAssetEditPage, FixedAssetsListPage } from '../features/fixedAssets/FixedAssetPages';
import { DepreciationRunDetailPage, DepreciationRunsListPage, DepreciationSchedulePage } from '../features/fixedAssets/DepreciationPages';
import {
  AssetDisposalEditPage,
  AssetDisposalsListPage,
  AssetPhysicalCountEditPage,
  AssetPhysicalCountsListPage,
  AssetSettingsPage,
  AssetTransferEditPage,
  AssetTransfersListPage
} from '../features/fixedAssets/MovementPages';
import {
  MaintenanceCategoriesListPage,
  MaintenanceCategoryEditPage,
  MaintenanceIssueEditPage,
  MaintenanceIssuesListPage,
  MaintenanceRequestEditPage,
  MaintenanceRequestsListPage,
  MaintenanceScheduleEditPage,
  MaintenanceSchedulesListPage
} from '../features/maintenance/MaintenancePages';
import { MaintenanceBoardPage } from '../features/maintenance/MaintenanceBoardPage';
import { CodingRulesSettingsPage } from '../features/settings/codingRules/CodingRulesSettingsPage';
import { UsersListPage } from '../features/settings/security/UsersListPage';
import { UserEditPage } from '../features/settings/security/UserEditPage';
import { RolesListPage } from '../features/settings/security/RolesListPage';
import { RoleEditPage } from '../features/settings/security/RoleEditPage';
import { AuditLogPage, LoginAttemptsPage, SecuritySettingsPage, SessionsPage } from '../features/settings/security/SecurityLogPages';
import { CompaniesListPage } from '../features/organization/companies/CompaniesListPage';
import { CompanyEditPage } from '../features/organization/companies/CompanyEditPage';
import { CurrenciesListPage } from '../features/organization/currencies/CurrenciesListPage';
import { CurrencyEditPage } from '../features/organization/currencies/CurrencyEditPage';
import { UnitsOfMeasureListPage } from '../features/inventory/unitsOfMeasure/UnitsOfMeasureListPage';
import { UnitOfMeasureEditPage } from '../features/inventory/unitsOfMeasure/UnitOfMeasureEditPage';
import { POSCategoriesListPage } from '../features/inventory/posCategories/POSCategoriesListPage';
import { POSCategoryEditPage } from '../features/inventory/posCategories/POSCategoryEditPage';
import { ItemGroupsListPage } from '../features/inventory/itemGroups/ItemGroupsListPage';
import { ItemGroupEditPage } from '../features/inventory/itemGroups/ItemGroupEditPage';
import { WarehousesListPage } from '../features/inventory/warehouses/WarehousesListPage';
import { SalesReturnsListPage } from '../features/sales/salesReturns/SalesReturnsListPage';
import { SalesReturnEditPage } from '../features/sales/salesReturns/SalesReturnEditPage';
import { DeliveryOrdersListPage } from '../features/sales/deliveryOrders/DeliveryOrdersListPage';
import { DeliveryOrderEditPage } from '../features/sales/deliveryOrders/DeliveryOrderEditPage';
import { SalesInvoicesListPage } from '../features/sales/salesInvoices/SalesInvoicesListPage';
import { SalesInvoiceEditPage } from '../features/sales/salesInvoices/SalesInvoiceEditPage';
import { SalesQuotesListPage } from '../features/sales/salesQuotes/SalesQuotesListPage';
import { SalesQuoteEditPage } from '../features/sales/salesQuotes/SalesQuoteEditPage';
import { SalesOrdersListPage } from '../features/sales/salesOrders/SalesOrdersListPage';
import { SalesOrderEditPage } from '../features/sales/salesOrders/SalesOrderEditPage';
import { PriceListsListPage } from '../features/sales/priceLists/PriceListsListPage';
import { PriceListEditPage } from '../features/sales/priceLists/PriceListEditPage';
import { DiscountsListPage } from '../features/sales/discounts/DiscountsListPage';
import { DiscountEditPage } from '../features/sales/discounts/DiscountEditPage';
import { CustomersListPage } from '../features/sales/customers/CustomersListPage';
import { CustomerEditPage } from '../features/sales/customers/CustomerEditPage';
import { LoyaltyTiersListPage } from '../features/sales/loyaltyTiers/LoyaltyTiersListPage';
import { LoyaltyTierEditPage } from '../features/sales/loyaltyTiers/LoyaltyTierEditPage';
import { SalesCycleSettingsPage } from '../features/sales/settings/SalesCycleSettingsPage';
import { LoyaltyProgramSettingsPage } from '../features/sales/settings/LoyaltyProgramSettingsPage';
import { SuppliersListPage } from '../features/purchasing/suppliers/SuppliersListPage';
import { SupplierEditPage } from '../features/purchasing/suppliers/SupplierEditPage';
import { PurchaseCycleSettingsPage } from '../features/purchasing/settings/PurchaseCycleSettingsPage';
import { AccountMappingsPage } from '../features/accounting/settings/AccountMappingsPage';
import { PostingReportsPage } from '../features/accounting/postingReports/PostingReportsPage';
import { PurchaseRequestsListPage } from '../features/purchasing/purchaseRequests/PurchaseRequestsListPage';
import { PurchaseRequestEditPage } from '../features/purchasing/purchaseRequests/PurchaseRequestEditPage';
import { PurchaseOrdersListPage } from '../features/purchasing/purchaseOrders/PurchaseOrdersListPage';
import { PurchaseOrderEditPage } from '../features/purchasing/purchaseOrders/PurchaseOrderEditPage';
import { GoodsReceiptsListPage } from '../features/purchasing/goodsReceipts/GoodsReceiptsListPage';
import { GoodsReceiptEditPage } from '../features/purchasing/goodsReceipts/GoodsReceiptEditPage';
import { PurchaseInvoicesListPage } from '../features/purchasing/purchaseInvoices/PurchaseInvoicesListPage';
import { PurchaseInvoiceEditPage } from '../features/purchasing/purchaseInvoices/PurchaseInvoiceEditPage';
import { SupplierPaymentsListPage } from '../features/purchasing/supplierPayments/SupplierPaymentsListPage';
import { SupplierPaymentEditPage } from '../features/purchasing/supplierPayments/SupplierPaymentEditPage';
import { SupplierPriceHistoryPage } from '../features/purchasing/supplierPriceHistory/SupplierPriceHistoryPage';
import { PurchaseReturnsListPage } from '../features/purchasing/purchaseReturns/PurchaseReturnsListPage';
import { PurchaseReturnEditPage } from '../features/purchasing/purchaseReturns/PurchaseReturnEditPage';
import { SupplierContractsListPage } from '../features/purchasing/supplierContracts/SupplierContractsListPage';
import { SupplierContractEditPage } from '../features/purchasing/supplierContracts/SupplierContractEditPage';
import { SupplierEvaluationsListPage } from '../features/purchasing/supplierEvaluations/SupplierEvaluationsListPage';
import { SupplierEvaluationEditPage } from '../features/purchasing/supplierEvaluations/SupplierEvaluationEditPage';
import { RFQsListPage } from '../features/purchasing/rfqs/RFQsListPage';
import { RFQEditPage } from '../features/purchasing/rfqs/RFQEditPage';
import { PurchasingReportsPage } from '../features/purchasing/reports/PurchasingReportsPage';
import { InventoryReportsPage } from '../features/inventory/reports/InventoryReportsPage';
import { PurchaseExpensesListPage } from '../features/purchasing/purchaseExpenses/PurchaseExpensesListPage';
import { PurchaseExpenseEditPage } from '../features/purchasing/purchaseExpenses/PurchaseExpenseEditPage';
import { WarehouseEditPage } from '../features/inventory/warehouses/WarehouseEditPage';
import { ItemsListPage } from '../features/inventory/items/ItemsListPage';
import { ItemEditPage } from '../features/inventory/items/ItemEditPage';
import { WarehouseDocumentsListPage } from '../features/inventory/warehouseDocuments/WarehouseDocumentsListPage';
import { WarehouseDocumentEditPage } from '../features/inventory/warehouseDocuments/WarehouseDocumentEditPage';
import { BranchRequestsListPage } from '../features/inventory/branchRequests/BranchRequestsListPage';
import { BranchRequestEditPage } from '../features/inventory/branchRequests/BranchRequestEditPage';
import { BranchRequestApprovalPage } from '../features/inventory/branchRequests/BranchRequestApprovalPage';
import { RecipesListPage } from '../features/inventory/recipes/RecipesListPage';
import { RecipeEditPage } from '../features/inventory/recipes/RecipeEditPage';
import { ProductionOrdersListPage } from '../features/inventory/productionOrders/ProductionOrdersListPage';
import { ProductionOrderEditPage } from '../features/inventory/productionOrders/ProductionOrderEditPage';
import { WasteRecordsListPage } from '../features/inventory/wasteRecords/WasteRecordsListPage';
import { WasteRecordEditPage } from '../features/inventory/wasteRecords/WasteRecordEditPage';
import { ProductionDocumentsListPage } from '../features/inventory/productionDocuments/ProductionDocumentsListPage';
import { ProductionDocumentDetailPage } from '../features/inventory/productionDocuments/ProductionDocumentDetailPage';
import { InventoryCountsListPage } from '../features/inventory/inventoryCounts/InventoryCountsListPage';
import { InventoryCountEditPage } from '../features/inventory/inventoryCounts/InventoryCountEditPage';
import { ShortagePolicySettingsPage } from '../features/inventory/settings/ShortagePolicySettingsPage';
import { GeneralInventorySettingsPage } from '../features/inventory/settings/GeneralInventorySettingsPage';
import { ProductionSalesModeSettingsPage } from '../features/inventory/settings/ProductionSalesModeSettingsPage';
import { TransferReceiptsListPage } from '../features/inventory/warehouseDocuments/TransferReceiptsListPage';
import { TransferReceiptEditPage } from '../features/inventory/warehouseDocuments/TransferReceiptEditPage';
import { CustodyOfficersListPage } from '../features/inventory/custodyOfficers/CustodyOfficersListPage';
import { CustodyOfficerEditPage } from '../features/inventory/custodyOfficers/CustodyOfficerEditPage';
import { POSTerminalsListPage } from '../features/pos/terminals/POSTerminalsListPage';
import { POSTerminalEditPage } from '../features/pos/terminals/POSTerminalEditPage';
import { BranchPOSSettingsPage } from '../features/pos/settings/BranchPOSSettingsPage';
import { ShiftAssignmentsListPage } from '../features/pos/shiftAssignments/ShiftAssignmentsListPage';
import { ShiftsListPage } from '../features/pos/shifts/ShiftsListPage';
import { ShiftConsolePage } from '../features/pos/shifts/ShiftConsolePage';
import { ShiftDetailPage } from '../features/pos/shifts/ShiftDetailPage';
import { TableBoardPage } from '../features/pos/tables/TableBoardPage';
import { ChecksListPage } from '../features/pos/checks/ChecksListPage';
import { CheckEditPage } from '../features/pos/checks/CheckEditPage';
import { PaymentPage } from '../features/pos/checks/PaymentPage';
import { DrawerMovementsPage } from '../features/pos/drawerMovements/DrawerMovementsPage';
import { DrawerExpensesPage } from '../features/pos/drawerExpenses/DrawerExpensesPage';
import { POSReturnsListPage } from '../features/pos/returns/POSReturnsListPage';
import { POSReturnEditPage } from '../features/pos/returns/POSReturnEditPage';
import { PaymentMethodConfigsPage } from '../features/pos/paymentMethodConfigs/PaymentMethodConfigsPage';
import { POSInvoiceReceiptPage } from '../features/pos/invoices/POSInvoiceReceiptPage';
import { DeliveryOrdersListPage as POSDeliveryOrdersListPage } from '../features/pos/deliveryOrders/DeliveryOrdersListPage';
import { QRTicketsPage } from '../features/pos/qrTickets/QRTicketsPage';
import { BlendTypesListPage } from '../features/pos/blendTypes/BlendTypesListPage';
import { BlendTypeEditPage } from '../features/pos/blendTypes/BlendTypeEditPage';
import { BlendConsultationPage } from '../features/pos/blendTypes/BlendConsultationPage';
import { JobGradesListPage, JobGradeEditPage } from '../features/hr/jobGrades/JobGradePages';
import { JobPositionsListPage, JobPositionEditPage } from '../features/hr/jobPositions/JobPositionPages';
import { OrgUnitsPage } from '../features/hr/orgUnits/OrgUnitsPage';
import { DocumentTypesListPage, DocumentTypeEditPage } from '../features/hr/documentTypes/DocumentTypePages';
import { InsuranceOfficesListPage, InsuranceOfficeEditPage } from '../features/hr/insuranceOffices/InsuranceOfficePages';
import { CountriesListPage, CountryEditPage } from '../features/settings/countries/CountryPages';
import { CitiesListPage, CityEditPage } from '../features/settings/cities/CityPages';
import { BanksListPage, BankEditPage } from '../features/settings/banks/BankPages';
import { EmployeesListPage } from '../features/hr/employees/EmployeesListPage';
import { EmployeeEditPage } from '../features/hr/employees/EmployeeEditPage';
import { HiringWizardPage } from '../features/hr/hiring/HiringWizardPage';
import { HrSettingsPage } from '../features/hr/hrSettings/HrSettingsPage';
import { WorkShiftsListPage, WorkShiftEditPage } from '../features/hr/workShifts/WorkShiftPages';
import { ShiftSchedulesListPage, ShiftScheduleEditPage } from '../features/hr/shiftSchedules/ShiftSchedulePages';
import { BulkShiftScheduleGeneratorPage } from '../features/hr/shiftSchedules/BulkShiftScheduleGeneratorPage';
import { TimeEntriesListPage } from '../features/hr/timeEntries/TimeEntryPages';
import { AttendanceListPage } from '../features/hr/attendance/AttendancePages';
import { AttendanceConflictsPage } from '../features/hr/attendance/AttendanceConflictsPage';
import { LeaveTypesListPage, LeaveTypeEditPage } from '../features/hr/leaveTypes/LeaveTypePages';
import { HolidaysListPage, HolidayEditPage } from '../features/hr/holidays/HolidayPages';
import { LeaveBalancesListPage } from '../features/hr/leaveBalances/LeaveBalancePages';
import { LeaveRequestsListPage, LeaveRequestEditPage } from '../features/hr/leaveRequests/LeaveRequestPages';
import { OvertimeRequestsListPage, OvertimeRequestEditPage } from '../features/hr/overtimeRequests/OvertimeRequestPages';
import { AttendanceDevicesListPage, AttendanceDeviceEditPage } from '../features/hr/attendanceDevices/AttendanceDevicePages';
import { EmployeeDeviceMappingsPage } from '../features/hr/employeeDeviceMappings/EmployeeDeviceMappingsPage';
import { AttendanceDeviceLogsPage } from '../features/hr/attendanceDeviceLogs/AttendanceDeviceLogsPage';
import { AttendanceReconciliationPage } from '../features/hr/attendanceReconciliation/AttendanceReconciliationPage';
import { ApprovalWorkflowsListPage } from '../features/settings/approvals/ApprovalWorkflowsListPage';
import { ApprovalWorkflowEditPage } from '../features/settings/approvals/ApprovalWorkflowEditPage';
import { MyPendingApprovalsPage } from '../features/approvals/MyPendingApprovalsPage';
import { NotificationsPage } from '../features/notifications/NotificationsPage';

/** The tab a "screen" opens in when nothing else is open yet, and where a closed last tab
 * falls back to (My Remarks/Remarks2.md, remark 3.4). */
export const DEFAULT_PATH = '/accounting/journal-entries';

/**
 * Every authenticated screen's route, shared by every open workspace tab (app/AppLayout.tsx
 * renders one independent `<Routes location={...}>` per tab so each tab keeps its own mounted
 * component state). Previously this list lived directly under App.tsx's single `<Outlet/>`;
 * moving it here lets it be reused once per tab instead of once per browser location.
 */
export const appRoutes = (
  <>
    <Route path="/accounting/journal-entries" element={<JournalEntriesListPage />} />
    <Route path="/accounting/journal-entries/:id" element={<JournalEntryEditPage />} />

    <Route path="/accounting/receipt-vouchers" element={<VouchersListPage kind="receipt-vouchers" />} />
    <Route path="/accounting/receipt-vouchers/:id" element={<VoucherEditPage kind="receipt-vouchers" />} />

    <Route path="/accounting/payment-vouchers" element={<VouchersListPage kind="payment-vouchers" />} />
    <Route path="/accounting/payment-vouchers/:id" element={<VoucherEditPage kind="payment-vouchers" />} />

    <Route path="/accounting/dimensions" element={<DimensionsListPage />} />
    <Route path="/accounting/dimensions/:id" element={<DimensionEditPage />} />
    <Route path="/accounting/branches" element={<BranchesListPage />} />
    <Route path="/accounting/branches/:id" element={<BranchEditPage />} />
    <Route path="/accounting/accounting-periods" element={<PeriodsListPage />} />
    <Route path="/accounting/accounting-periods/:id" element={<PeriodEditPage />} />
    <Route path="/accounting/custody-registers" element={<CustodyPage />} />
    <Route path="/accounting/cash-reconciliations" element={<CashReconciliationsPage />} />
    <Route path="/accounting/bank-reconciliations" element={<BankReconciliationsPage />} />
    <Route path="/accounting/chart-of-accounts" element={<ChartOfAccountsPage />} />
    <Route path="/accounting/reports" element={<ReportsPage />} />
    <Route path="/accounting/payment-methods" element={<PaymentMethodsListPage />} />
    <Route path="/accounting/payment-methods/:id" element={<PaymentMethodEditPage />} />
    <Route path="/accounting/treasury-transfers" element={<TreasuryTransfersListPage />} />
    <Route path="/accounting/treasury-transfers/:id" element={<TreasuryTransferEditPage />} />
    <Route path="/accounting/opening-balances" element={<AccountOpeningBalancesListPage />} />
    <Route path="/accounting/opening-balances/:id" element={<AccountOpeningBalanceBatchEditPage />} />
    <Route path="/inventory/items" element={<ItemsListPage />} />
    <Route path="/inventory/items/:id" element={<ItemEditPage />} />
    <Route path="/inventory/item-groups" element={<ItemGroupsListPage />} />
    <Route path="/inventory/item-groups/:id" element={<ItemGroupEditPage />} />
    <Route path="/inventory/units-of-measure" element={<UnitsOfMeasureListPage />} />
    <Route path="/inventory/units-of-measure/:id" element={<UnitOfMeasureEditPage />} />
    <Route path="/inventory/pos-categories" element={<POSCategoriesListPage />} />
    <Route path="/inventory/pos-categories/:id" element={<POSCategoryEditPage />} />
    <Route path="/inventory/warehouses" element={<WarehousesListPage />} />
    <Route path="/sales/returns" element={<SalesReturnsListPage />} />
    <Route path="/sales/returns/:id" element={<SalesReturnEditPage />} />
    <Route path="/sales/delivery-orders" element={<DeliveryOrdersListPage />} />
    <Route path="/sales/delivery-orders/:id" element={<DeliveryOrderEditPage />} />
    <Route path="/sales/invoices" element={<SalesInvoicesListPage />} />
    <Route path="/sales/invoices/:id" element={<SalesInvoiceEditPage />} />
    <Route path="/sales/quotes" element={<SalesQuotesListPage />} />
    <Route path="/sales/quotes/:id" element={<SalesQuoteEditPage />} />
    <Route path="/sales/sales-orders" element={<SalesOrdersListPage />} />
    <Route path="/sales/sales-orders/:id" element={<SalesOrderEditPage />} />
    <Route path="/sales/price-lists" element={<PriceListsListPage />} />
    <Route path="/sales/price-lists/:id" element={<PriceListEditPage />} />
    <Route path="/sales/discounts" element={<DiscountsListPage />} />
    <Route path="/sales/discounts/:id" element={<DiscountEditPage />} />
    <Route path="/sales/customers" element={<CustomersListPage />} />
    <Route path="/sales/customers/:id" element={<CustomerEditPage />} />
    <Route path="/sales/loyalty-tiers" element={<LoyaltyTiersListPage />} />
    <Route path="/sales/loyalty-tiers/:id" element={<LoyaltyTierEditPage />} />
    <Route path="/sales/settings/sales-cycle" element={<SalesCycleSettingsPage />} />
    <Route path="/sales/settings/loyalty-program" element={<LoyaltyProgramSettingsPage />} />
    <Route path="/purchasing/suppliers" element={<SuppliersListPage />} />
    <Route path="/purchasing/suppliers/:id" element={<SupplierEditPage />} />
    <Route path="/purchasing/settings/purchase-cycle" element={<PurchaseCycleSettingsPage />} />
    <Route path="/accounting/settings/account-mappings" element={<AccountMappingsPage />} />
    <Route path="/accounting/posting-reports" element={<PostingReportsPage />} />
    <Route path="/purchasing/purchase-requests" element={<PurchaseRequestsListPage />} />
    <Route path="/purchasing/purchase-requests/:id" element={<PurchaseRequestEditPage />} />
    <Route path="/purchasing/purchase-orders" element={<PurchaseOrdersListPage />} />
    <Route path="/purchasing/purchase-orders/:id" element={<PurchaseOrderEditPage />} />
    <Route path="/purchasing/goods-receipts" element={<GoodsReceiptsListPage />} />
    <Route path="/purchasing/goods-receipts/:id" element={<GoodsReceiptEditPage />} />
    <Route path="/purchasing/purchase-invoices" element={<PurchaseInvoicesListPage />} />
    <Route path="/purchasing/purchase-invoices/:id" element={<PurchaseInvoiceEditPage />} />
    <Route path="/purchasing/supplier-payments" element={<SupplierPaymentsListPage />} />
    <Route path="/purchasing/supplier-payments/:id" element={<SupplierPaymentEditPage />} />
    <Route path="/purchasing/supplier-price-history" element={<SupplierPriceHistoryPage />} />
    <Route path="/purchasing/purchase-returns" element={<PurchaseReturnsListPage />} />
    <Route path="/purchasing/purchase-returns/:id" element={<PurchaseReturnEditPage />} />
    <Route path="/purchasing/supplier-contracts" element={<SupplierContractsListPage />} />
    <Route path="/purchasing/supplier-contracts/:id" element={<SupplierContractEditPage />} />
    <Route path="/purchasing/supplier-evaluations" element={<SupplierEvaluationsListPage />} />
    <Route path="/purchasing/supplier-evaluations/:id" element={<SupplierEvaluationEditPage />} />
    <Route path="/purchasing/rfqs" element={<RFQsListPage />} />
    <Route path="/purchasing/rfqs/:id" element={<RFQEditPage />} />
    <Route path="/purchasing/reports" element={<PurchasingReportsPage />} />
    <Route path="/purchasing/purchase-expenses" element={<PurchaseExpensesListPage />} />
    <Route path="/purchasing/purchase-expenses/:id" element={<PurchaseExpenseEditPage />} />
    <Route path="/inventory/warehouses/:id" element={<WarehouseEditPage />} />
    <Route path="/inventory/reports" element={<InventoryReportsPage />} />
    <Route path="/inventory/opening-balances" element={<WarehouseDocumentsListPage kind="opening-balances" />} />
    <Route path="/inventory/opening-balances/:id" element={<WarehouseDocumentEditPage kind="opening-balances" />} />
    <Route path="/inventory/stock-in" element={<WarehouseDocumentsListPage kind="stock-in" />} />
    <Route path="/inventory/stock-in/:id" element={<WarehouseDocumentEditPage kind="stock-in" />} />
    <Route path="/inventory/stock-out" element={<WarehouseDocumentsListPage kind="stock-out" />} />
    <Route path="/inventory/stock-out/:id" element={<WarehouseDocumentEditPage kind="stock-out" />} />
    <Route path="/inventory/transfer-order" element={<WarehouseDocumentsListPage kind="transfer-order" />} />
    <Route path="/inventory/transfer-order/:id" element={<WarehouseDocumentEditPage kind="transfer-order" />} />
    <Route path="/inventory/inventory-adjustments" element={<WarehouseDocumentsListPage kind="inventory-adjustments" />} />
    <Route path="/inventory/inventory-adjustments/:id" element={<WarehouseDocumentEditPage kind="inventory-adjustments" />} />
    <Route path="/inventory/inventory-counts" element={<InventoryCountsListPage />} />
    <Route path="/inventory/inventory-counts/:id" element={<InventoryCountEditPage />} />
    <Route path="/inventory/settings/shortage-policy" element={<ShortagePolicySettingsPage />} />
    <Route path="/inventory/settings/inventory-settings" element={<GeneralInventorySettingsPage />} />
    <Route path="/inventory/settings/production-sales-mode" element={<ProductionSalesModeSettingsPage />} />
    <Route path="/inventory/transfer-receipt" element={<TransferReceiptsListPage />} />
    <Route path="/inventory/transfer-receipt/:id" element={<TransferReceiptEditPage />} />
    <Route path="/inventory/custody-officers" element={<CustodyOfficersListPage />} />
    <Route path="/inventory/custody-officers/:id" element={<CustodyOfficerEditPage />} />
    <Route path="/inventory/branch-requests" element={<BranchRequestsListPage />} />
    <Route path="/inventory/branch-requests/:id/approve" element={<BranchRequestApprovalPage />} />
    <Route path="/inventory/branch-requests/:id" element={<BranchRequestEditPage />} />
    <Route path="/inventory/recipes" element={<RecipesListPage />} />
    <Route path="/inventory/recipes/:id" element={<RecipeEditPage />} />
    <Route path="/inventory/production-orders" element={<ProductionOrdersListPage />} />
    <Route path="/inventory/production-orders/:id" element={<ProductionOrderEditPage />} />
    <Route path="/inventory/waste-records" element={<WasteRecordsListPage />} />
    <Route path="/inventory/waste-records/:id" element={<WasteRecordEditPage />} />
    <Route path="/inventory/production-issues" element={<ProductionDocumentsListPage kind="production-issues" />} />
    <Route path="/inventory/production-issues/:id" element={<ProductionDocumentDetailPage kind="production-issues" />} />
    <Route path="/inventory/production-receipts" element={<ProductionDocumentsListPage kind="production-receipts" />} />
    <Route path="/inventory/production-receipts/:id" element={<ProductionDocumentDetailPage kind="production-receipts" />} />
    <Route path="/pos/terminals" element={<POSTerminalsListPage />} />
    <Route path="/pos/terminals/:id" element={<POSTerminalEditPage />} />
    <Route path="/pos/branch-settings" element={<BranchPOSSettingsPage />} />
    <Route path="/pos/shift-assignments" element={<ShiftAssignmentsListPage />} />
    <Route path="/pos/shifts" element={<ShiftsListPage />} />
    <Route path="/pos/shift-console" element={<ShiftConsolePage />} />
    <Route path="/pos/shifts/:id" element={<ShiftDetailPage />} />
    <Route path="/pos/table-board" element={<TableBoardPage />} />
    <Route path="/pos/checks/open" element={<ChecksListPage kind="open" />} />
    <Route path="/pos/checks/held" element={<ChecksListPage kind="held" />} />
    <Route path="/pos/checks/:id" element={<CheckEditPage />} />
    <Route path="/pos/checks/:id/payment" element={<PaymentPage />} />
    <Route path="/pos/payment-method-configs" element={<PaymentMethodConfigsPage />} />
    <Route path="/pos/invoices/:id" element={<POSInvoiceReceiptPage />} />
    <Route path="/pos/drawer-movements" element={<DrawerMovementsPage />} />
    <Route path="/pos/drawer-expenses" element={<DrawerExpensesPage />} />
    <Route path="/pos/returns" element={<POSReturnsListPage />} />
    <Route path="/pos/returns/:id" element={<POSReturnEditPage />} />
    <Route path="/pos/delivery-orders" element={<POSDeliveryOrdersListPage />} />
    <Route path="/pos/qr-tickets" element={<QRTicketsPage />} />
    <Route path="/pos/blend-types" element={<BlendTypesListPage />} />
    <Route path="/pos/blend-types/:id" element={<BlendTypeEditPage />} />
    <Route path="/pos/blend-consultation" element={<BlendConsultationPage />} />
    {/* Assets & maintenance (08-Module-Maintenance-FixedAssets): 13 screens. */}
    <Route path="/fixed-assets/categories" element={<AssetCategoriesListPage />} />
    <Route path="/fixed-assets/categories/:id" element={<AssetCategoryEditPage />} />
    <Route path="/fixed-assets/assets" element={<FixedAssetsListPage />} />
    <Route path="/fixed-assets/assets/:id" element={<FixedAssetEditPage />} />
    <Route path="/fixed-assets/depreciation-schedule" element={<DepreciationSchedulePage />} />
    <Route path="/fixed-assets/depreciation-runs" element={<DepreciationRunsListPage />} />
    <Route path="/fixed-assets/depreciation-runs/:id" element={<DepreciationRunDetailPage />} />
    <Route path="/fixed-assets/transfers" element={<AssetTransfersListPage />} />
    <Route path="/fixed-assets/transfers/:id" element={<AssetTransferEditPage />} />
    <Route path="/fixed-assets/disposals" element={<AssetDisposalsListPage />} />
    <Route path="/fixed-assets/disposals/:id" element={<AssetDisposalEditPage />} />
    <Route path="/fixed-assets/physical-counts" element={<AssetPhysicalCountsListPage />} />
    <Route path="/fixed-assets/physical-counts/:id" element={<AssetPhysicalCountEditPage />} />
    <Route path="/fixed-assets/settings" element={<AssetSettingsPage />} />
    <Route path="/maintenance/categories" element={<MaintenanceCategoriesListPage />} />
    <Route path="/maintenance/categories/:id" element={<MaintenanceCategoryEditPage />} />
    <Route path="/maintenance/issues" element={<MaintenanceIssuesListPage />} />
    <Route path="/maintenance/issues/:id" element={<MaintenanceIssueEditPage />} />
    <Route path="/maintenance/requests" element={<MaintenanceRequestsListPage />} />
    <Route path="/maintenance/requests/:id" element={<MaintenanceRequestEditPage />} />
    <Route path="/maintenance/schedules" element={<MaintenanceSchedulesListPage />} />
    <Route path="/maintenance/schedules/:id" element={<MaintenanceScheduleEditPage />} />
    <Route path="/maintenance/board" element={<MaintenanceBoardPage />} />

    <Route path="/hr/job-grades" element={<JobGradesListPage />} />
    <Route path="/hr/job-grades/:id" element={<JobGradeEditPage />} />
    <Route path="/hr/job-positions" element={<JobPositionsListPage />} />
    <Route path="/hr/job-positions/:id" element={<JobPositionEditPage />} />
    <Route path="/hr/org-units" element={<OrgUnitsPage />} />
    <Route path="/hr/document-types" element={<DocumentTypesListPage />} />
    <Route path="/hr/document-types/:id" element={<DocumentTypeEditPage />} />
    <Route path="/hr/insurance-offices" element={<InsuranceOfficesListPage />} />
    <Route path="/hr/insurance-offices/:id" element={<InsuranceOfficeEditPage />} />
    <Route path="/hr/employees" element={<EmployeesListPage />} />
    <Route path="/hr/employees/:id" element={<EmployeeEditPage />} />
    <Route path="/hr/hiring/new" element={<HiringWizardPage />} />
    <Route path="/hr/settings" element={<HrSettingsPage />} />

    <Route path="/hr/work-shifts" element={<WorkShiftsListPage />} />
    <Route path="/hr/work-shifts/:id" element={<WorkShiftEditPage />} />
    <Route path="/hr/shift-schedules" element={<ShiftSchedulesListPage />} />
    <Route path="/hr/shift-schedules/:id" element={<ShiftScheduleEditPage />} />
    <Route path="/hr/shift-schedule-generator" element={<BulkShiftScheduleGeneratorPage />} />
    <Route path="/hr/time-entries" element={<TimeEntriesListPage />} />
    <Route path="/hr/attendance" element={<AttendanceListPage />} />
    <Route path="/hr/attendance-conflicts" element={<AttendanceConflictsPage />} />
    <Route path="/hr/leave-types" element={<LeaveTypesListPage />} />
    <Route path="/hr/leave-types/:id" element={<LeaveTypeEditPage />} />
    <Route path="/hr/holidays" element={<HolidaysListPage />} />
    <Route path="/hr/holidays/:id" element={<HolidayEditPage />} />
    <Route path="/hr/leave-balances" element={<LeaveBalancesListPage />} />
    <Route path="/hr/leave-requests" element={<LeaveRequestsListPage />} />
    <Route path="/hr/leave-requests/new" element={<LeaveRequestEditPage />} />
    <Route path="/hr/overtime-requests" element={<OvertimeRequestsListPage />} />
    <Route path="/hr/overtime-requests/new" element={<OvertimeRequestEditPage />} />

    <Route path="/hr/attendance-devices" element={<AttendanceDevicesListPage />} />
    <Route path="/hr/attendance-devices/:id" element={<AttendanceDeviceEditPage />} />
    <Route path="/hr/employee-device-mappings" element={<EmployeeDeviceMappingsPage />} />
    <Route path="/hr/attendance-device-logs" element={<AttendanceDeviceLogsPage />} />
    <Route path="/hr/attendance-reconciliation" element={<AttendanceReconciliationPage />} />

    <Route path="/settings/approval-workflows" element={<ApprovalWorkflowsListPage />} />
    <Route path="/settings/approval-workflows/:id" element={<ApprovalWorkflowEditPage />} />
    <Route path="/approvals/pending" element={<MyPendingApprovalsPage />} />
    <Route path="/notifications" element={<NotificationsPage />} />

    <Route path="/settings/coding-rules" element={<CodingRulesSettingsPage />} />
    <Route path="/settings/companies" element={<CompaniesListPage />} />
    <Route path="/settings/companies/:id" element={<CompanyEditPage />} />
    <Route path="/settings/currencies" element={<CurrenciesListPage />} />
    <Route path="/settings/currencies/:id" element={<CurrencyEditPage />} />
    <Route path="/settings/users" element={<UsersListPage />} />
    <Route path="/settings/users/:id" element={<UserEditPage />} />
    <Route path="/settings/roles" element={<RolesListPage />} />
    <Route path="/settings/roles/:id" element={<RoleEditPage />} />
    <Route path="/settings/sessions" element={<SessionsPage />} />
    <Route path="/settings/login-attempts" element={<LoginAttemptsPage />} />
    <Route path="/settings/audit-log" element={<AuditLogPage />} />
    <Route path="/settings/security" element={<SecuritySettingsPage />} />
    <Route path="/settings/countries" element={<CountriesListPage />} />
    <Route path="/settings/countries/:id" element={<CountryEditPage />} />
    <Route path="/settings/cities" element={<CitiesListPage />} />
    <Route path="/settings/cities/:id" element={<CityEditPage />} />
    <Route path="/settings/banks" element={<BanksListPage />} />
    <Route path="/settings/banks/:id" element={<BankEditPage />} />

    <Route path="*" element={<Navigate to={DEFAULT_PATH} replace />} />
  </>
);
