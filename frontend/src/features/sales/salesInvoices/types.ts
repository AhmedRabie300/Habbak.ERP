export interface SalesInvoiceListItem {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  paymentType: string;
  totalAmount: number;
  amountPaid: number;
  status: string;
}

export interface SalesInvoiceLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  discountAmount?: number;
  lineTotal: number;
}

export interface SalesInvoiceLineInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountAmount?: number;
}

export interface SalesInvoiceDetail {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  branchId?: number;
  customerId: number;
  customerNameAr: string;
  sourceOrderId?: number;
  sourceOrderNumber?: string;
  paymentType: string;
  creditLimitOverrideApproved: boolean;
  subtotal: number;
  discountAmount?: number;
  taxAmount: number;
  totalAmount: number;
  amountPaid: number;
  status: string;
  journalEntryId?: number;
  journalEntryNumber?: string;
  reversalJournalEntryId?: number;
  reversalJournalEntryNumber?: string;
  rowVersion: string;
  lines: SalesInvoiceLine[];
}

export interface PostedSalesInvoice {
  id: number;
  invoiceNumber: string;
  customerId: number;
  customerNameAr: string;
  totalAmount: number;
}

export interface SalesInvoiceFormValues {
  branchId?: number;
  customerId: number;
  invoiceDate: string;
  sourceOrderId?: number;
  paymentType: string;
  creditLimitOverrideApproved: boolean;
  taxAmount: number;
  discountAmount?: number;
  lines: SalesInvoiceLineInput[];
}
