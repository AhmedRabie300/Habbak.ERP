export type CheckOrderType = 'DineIn' | 'Takeaway' | 'Delivery';
export type CheckStatus = 'Open' | 'Held' | 'Completed' | 'Rejected' | 'Cancelled' | 'Merged';
export type ManualDiscountType = 'Percentage' | 'Fixed';

export interface CheckLine {
  id: number;
  lineNumber: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  lineTotal: number;
  isPriceManuallyOverridden: boolean;
  note?: string;
  sentToKitchenAt?: string;
}

export interface CheckListItem {
  id: number;
  checkCode: string;
  tableId?: number;
  tableCode?: string;
  orderType: CheckOrderType;
  status: CheckStatus;
  lineCount: number;
  total: number;
  openedAtUtc: string;
}

export interface CheckDetail {
  id: number;
  rowVersion: string;
  posTerminalId: number;
  shiftId: number;
  checkCode: string;
  tableId?: number;
  tableCode?: string;
  orderType: CheckOrderType;
  status: CheckStatus;
  customerId?: number;
  customerNameAr?: string;
  customerLoyaltyPointsBalance?: number;
  mergedIntoCheckId?: number;
  subtotal: number;
  discountTotal: number;
  manualDiscountType?: ManualDiscountType;
  manualDiscountValue?: number;
  manualDiscountReason?: string;
  manualDiscountAmount: number;
  loyaltyPointsToRedeem?: number;
  loyaltyDiscountAmount: number;
  /** Net of the lines after every discount — before service charge and VAT. */
  total: number;
  serviceChargeAmount: number;
  taxAmount: number;
  /** total + service charge + VAT, excluding any tip. What the cashier collects. */
  payableTotal: number;
  lines: CheckLine[];
}
