export interface PurchaseExpenseListItem {
  id: number;
  purchaseInvoiceId: number;
  invoiceNumber: string;
  supplierCode: string;
  supplierNameAr: string;
  expenseType: string;
  amount: number;
  allocationMethod: string;
}

export interface PurchaseExpenseDetail {
  id: number;
  purchaseInvoiceId: number;
  invoiceNumber: string;
  expenseType: string;
  amount: number;
  allocationMethod: string;
  notes?: string;
  rowVersion: string;
}

export interface PurchaseExpenseFormValues {
  purchaseInvoiceId: number;
  expenseType: string;
  amount: number;
  allocationMethod: string;
  notes?: string;
}
