export interface PurchaseInvoiceListItem {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  totalAmount: number;
  currencyCode: string;
  lineCount: number;
  status: string;
}

export interface PurchaseInvoiceLine {
  id?: number;
  itemId: number;
  purchaseOrderLineId?: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  receivedQuantity: number;
  unitPrice: number;
  totalPrice: number;
  discountAmount?: number;
  unitId: number;
  unitCode: string;
  allocatedAdditionalCost: number;
  allocationPercentage?: number;
  weight?: number;
}

export interface PurchaseInvoiceDetail {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string;
  branchId?: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  supplierInvoiceNumber?: string;
  purchaseOrderId?: number;
  purchaseOrderNumber?: string;
  goodsReceiptId?: number;
  goodsReceiptNumber?: string;
  currencyCode: string;
  exchangeRate: number;
  paymentTerms: string;
  status: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  discountAmount?: number;
  discountReason?: string;
  amountPaid: number;
  additionalCosts: number;
  additionalCostAllocationMethod?: string;
  commissionRate?: number;
  commissionAmount?: number;
  commissionAccountId?: number;
  notes?: string;
  journalEntryId?: number;
  journalEntryNumber?: string;
  reversalJournalEntryId?: number;
  reversalJournalEntryNumber?: string;
  rowVersion: string;
  lines: PurchaseInvoiceLine[];
}

export interface PurchaseInvoiceLineInput {
  itemId: number;
  /** The order line this bills (Remarks6) — set on lines loaded from a purchase order. */
  purchaseOrderLineId?: number;
  quantity: number;
  receivedQuantity: number;
  unitPrice: number;
  discountAmount?: number;
  /** The item's base unit when left out. */
  unitId?: number;
  allocationPercentage?: number;
  weight?: number;
}

export interface PurchaseInvoiceFormValues {
  branchId?: number;
  invoiceDate: string;
  dueDate: string;
  supplierId: number;
  supplierInvoiceNumber?: string;
  purchaseOrderId?: number;
  goodsReceiptId?: number;
  currencyCode: string;
  exchangeRate: number;
  paymentTerms: string;
  taxAmount: number;
  discountAmount?: number;
  discountReason?: string;
  additionalCosts: number;
  additionalCostAllocationMethod?: string;
  commissionRate?: number;
  commissionAmount?: number;
  commissionAccountId?: number;
  notes?: string;
  lines: PurchaseInvoiceLineInput[];
}
