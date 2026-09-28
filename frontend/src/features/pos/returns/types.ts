export interface POSReturnLineInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
}

export interface POSReturnLine {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface POSReturnListItem {
  id: number;
  returnNumber: string;
  returnDate: string;
  sourceInvoiceNumber: string;
  reason: string;
  total: number;
  status: string;
}

export interface POSReturnDetail {
  id: number;
  returnNumber: string;
  returnDate: string;
  sourceInvoiceId: number;
  sourceInvoiceNumber: string;
  reason: string;
  status: string;
  total: number;
  lines: POSReturnLine[];
}
