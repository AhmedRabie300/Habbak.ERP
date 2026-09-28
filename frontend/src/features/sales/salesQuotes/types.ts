export interface SalesQuoteListItem {
  id: number;
  quoteNumber: string;
  quoteDate: string;
  validUntil: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  subtotal: number;
  status: string;
}

export interface SalesQuoteLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  discountAmount?: number;
  lineTotal: number;
}

export interface SalesQuoteLineInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountAmount?: number;
}

export interface SalesQuoteDetail {
  id: number;
  quoteNumber: string;
  quoteDate: string;
  validUntil: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  subtotal: number;
  status: string;
  rowVersion: string;
  lines: SalesQuoteLine[];
}

export interface SalesQuoteFormValues {
  branchId?: number;
  customerId: number;
  quoteDate: string;
  validUntil: string;
  lines: SalesQuoteLineInput[];
}

export interface AcceptedSalesQuote {
  id: number;
  quoteNumber: string;
  customerId: number;
  customerNameAr: string;
  validUntil: string;
  subtotal: number;
}
