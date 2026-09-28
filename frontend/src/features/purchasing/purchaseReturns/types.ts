export interface PurchaseReturnListItem {
  id: number;
  returnNumber: string;
  returnDate: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  reason: string;
  lineCount: number;
  status: string;
}

export interface PurchaseReturnLine {
  id?: number;
  itemId: number;
  purchaseInvoiceLineId?: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitCost: number;
  unitId: number;
  unitCode: string;
  batchNumber?: string;
}

export interface PurchaseReturnDetail {
  id: number;
  returnNumber: string;
  returnDate: string;
  branchId?: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  purchaseInvoiceId?: number;
  purchaseInvoiceNumber?: string;
  warehouseId: number;
  warehouseCode: string;
  reason: string;
  status: string;
  notes?: string;
  rowVersion: string;
  lines: PurchaseReturnLine[];
}

/** One line of an invoice, with what is still returnable on it (Remarks4, item 8). */
export interface InvoiceLineForReturn {
  purchaseInvoiceLineId: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  invoicedQuantity: number;
  unitId: number;
  unitCode: string;
  unitCost: number;
  returnedQuantity: number;
  returnableQuantity: number;
}

export interface PurchaseReturnLineInput {
  itemId: number;
  quantity: number;
  unitCost: number;
  /** The invoice line this goes back on — set for every line of an invoice-linked return. */
  purchaseInvoiceLineId?: number;
  /** The item's base unit when left out. */
  unitId?: number;
  batchNumber?: string;
}

export interface PurchaseReturnFormValues {
  branchId?: number;
  returnDate: string;
  supplierId: number;
  purchaseInvoiceId?: number;
  warehouseId: number;
  reason: string;
  notes?: string;
  lines: PurchaseReturnLineInput[];
}
