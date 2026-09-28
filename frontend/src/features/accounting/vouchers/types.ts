export type VoucherKind = 'receipt-vouchers' | 'payment-vouchers';

export interface VoucherListItem {
  id: number;
  voucherType: string;
  voucherNumber: string;
  voucherDate: string;
  treasuryAccountId: number;
  description?: string;
  counterpartyType: string;
  amount: number;
  status: string;
}

export interface VoucherDetail {
  id: number;
  publicId?: string;
  branchId?: number;
  voucherType: string;
  voucherNumber: string;
  voucherDate: string;
  treasuryAccountId: number;
  description?: string;
  counterpartyType: string;
  counterpartyId?: number;
  directAccountId?: number;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  baseCurrencyAmount: number;
  relatedInvoiceId?: number;
  status: string;
  journalEntryId?: number;
  rowVersion: string;
}

export interface VoucherFormValues {
  branchId?: number;
  voucherDate: string;
  treasuryAccountId: number;
  description?: string;
  counterpartyType: 'Customer' | 'Supplier' | 'Employee' | 'Other';
  counterpartyId?: number;
  directAccountId?: number;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  baseCurrencyAmount: number;
}
