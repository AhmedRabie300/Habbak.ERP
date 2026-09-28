export interface SalesReturnListItem {
  id: number;
  returnNumber: string;
  returnDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  warehouseId: number;
  warehouseNameAr: string;
  sourceInvoiceId?: number;
  reason: string;
  lineCount: number;
  status: string;
}

export interface SalesReturnLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  batchNumber?: string;
}

export interface SalesReturnLineInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
  batchNumber?: string;
}

export interface SalesReturnDetail {
  id: number;
  returnNumber: string;
  returnDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  warehouseId: number;
  warehouseNameAr: string;
  sourceInvoiceId?: number;
  sourceInvoiceNumber?: string;
  reason: string;
  status: string;
  rowVersion: string;
  lines: SalesReturnLine[];
}

export interface SalesReturnFormValues {
  branchId?: number;
  customerId: number;
  warehouseId: number;
  returnDate: string;
  sourceInvoiceId?: number;
  reason: string;
  lines: SalesReturnLineInput[];
}
