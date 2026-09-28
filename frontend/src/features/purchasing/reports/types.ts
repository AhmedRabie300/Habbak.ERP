export interface PurchasesBySupplierRow {
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  currencyCode: string;
  invoiceCount: number;
  totalAmount: number;
  totalPaid: number;
}

export interface PurchasesByItemRow {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  totalQuantity: number;
  totalValue: number;
  averageUnitPrice: number;
}

export interface OpenPurchaseOrderRow {
  id: number;
  orderNumber: string;
  orderDate: string;
  supplierCode: string;
  supplierNameAr: string;
  status: string;
  totalAmount: number;
  remainingQuantity: number;
}

export interface UnpaidPurchaseInvoiceRow {
  id: number;
  invoiceNumber: string;
  dueDate: string;
  supplierCode: string;
  supplierNameAr: string;
  status: string;
  totalAmount: number;
  amountPaid: number;
  remainingAmount: number;
  daysOverdue: number;
}

export interface PurchaseReturnReportRow {
  id: number;
  returnNumber: string;
  returnDate: string;
  supplierCode: string;
  supplierNameAr: string;
  reason: string;
  status: string;
  totalValue: number;
}

export interface SupplierEvaluationRankingRow {
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  evaluationCount: number;
  averageQualityScore: number;
  averageDeliveryTimeScore: number;
  averageQuantityComplianceScore: number;
  averageOverallScore: number;
  latestEvaluationDate: string;
}

export interface PurchaseExpenseReportRow {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  supplierCode: string;
  supplierNameAr: string;
  additionalCosts: number;
  allocationMethod?: string;
  totalAmount: number;
}

export interface ExpiringSupplierContractRow {
  id: number;
  contractNumber: string;
  supplierCode: string;
  supplierNameAr: string;
  startDate: string;
  endDate: string;
  daysRemaining: number;
  autoRenew: boolean;
}

export interface AllocationMethodSummary {
  allocationMethod: string;
  invoiceCount: number;
  totalAdditionalCosts: number;
}

export interface AllocationByItemRow {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  totalAllocatedAdditionalCost: number;
}

export interface AdditionalCostAllocationReport {
  byMethod: AllocationMethodSummary[];
  byItem: AllocationByItemRow[];
}

export interface PurchaseExpenseTypeSummary {
  expenseType: string;
  count: number;
  totalAmount: number;
}

export interface ExpiredRFQQuoteRow {
  quoteId: number;
  rfqId: number;
  rfqNumber: string;
  itemCode: string;
  itemNameAr: string;
  supplierCode: string;
  supplierNameAr: string;
  unitPrice: number;
  validUntil: string;
  daysExpired: number;
}
