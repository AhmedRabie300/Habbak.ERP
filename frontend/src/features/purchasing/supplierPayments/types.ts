export interface SupplierPaymentListItem {
  id: number;
  voucherNumber: string;
  voucherDate: string;
  description?: string;
  amount: number;
  status: string;
}

/** Shape matches the generic VoucherDetailDto the API actually returns (GetVoucherByIdQuery is
 * reused as-is) — counterpartyId/relatedInvoiceId rather than supplierId/purchaseInvoiceId. */
export interface SupplierPaymentDetail {
  id: number;
  branchId?: number;
  voucherNumber: string;
  voucherDate: string;
  treasuryAccountId: number;
  description?: string;
  counterpartyId?: number;
  relatedInvoiceId?: number;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  baseCurrencyAmount: number;
  status: string;
  journalEntryId?: number;
  rowVersion: string;
}

export interface SupplierPaymentFormValues {
  branchId?: number;
  voucherDate: string;
  treasuryAccountId: number;
  description?: string;
  supplierId: number;
  purchaseInvoiceId?: number;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  baseCurrencyAmount: number;
}

export interface PayableInvoice {
  id: number;
  invoiceNumber: string;
  totalAmount: number;
  amountPaid: number;
  remainingAmount: number;
  currencyCode: string;
}
