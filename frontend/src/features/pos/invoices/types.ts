export interface PaymentInput {
  paymentMethodId: number;
  amount: number;
  cardTransactionReference?: string;
  amountTendered?: number;
}

export interface POSPayment {
  id: number;
  paymentMethodId: number;
  paymentMethodNameAr: string;
  amount: number;
  cardTransactionReference?: string;
  amountTendered?: number;
  changeGiven?: number;
  cashRoundingAdjustment: number;
  status: string;
}

export interface POSInvoiceLine {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  lineTotal: number;
}

export interface POSInvoiceListItem {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  orderType: string;
  total: number;
  status: string;
}

export interface POSInvoiceDetail {
  id: number;
  invoiceNumber: string;
  invoiceDate: string;
  checkId: number;
  checkCode: string;
  customerId?: number;
  orderType: string;
  subtotal: number;
  discountAmount: number;
  manualDiscountAmount: number;
  loyaltyPointsRedeemed: number;
  loyaltyDiscountAmount: number;
  serviceChargeAmount: number;
  tipAmount: number;
  taxAmount: number;
  total: number;
  status: string;
  etaReceiptStatus: string;
  lines: POSInvoiceLine[];
  payments: POSPayment[];
}
