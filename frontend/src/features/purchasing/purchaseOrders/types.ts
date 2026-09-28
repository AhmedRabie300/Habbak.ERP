export interface PurchaseOrderListItem {
  id: number;
  orderNumber: string;
  orderDate: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  totalAmount: number;
  currencyCode: string;
  lineCount: number;
  status: string;
}

export interface PurchaseOrderLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  receivedQuantity: number;
  unitPrice: number;
  totalPrice: number;
  discountAmount?: number;
  unitId: number;
  unitCode: string;
  expectedDeliveryDate?: string;
  weight?: number;
  /** The request line this line came from, if any (Remarks7). */
  purchaseRequestLineId?: number;
}

export interface PurchaseOrderDetail {
  id: number;
  orderNumber: string;
  orderDate: string;
  branchId?: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  purchaseRequestId?: number;
  purchaseRequestNumber?: string;
  currencyCode: string;
  exchangeRate: number;
  paymentTerms: string;
  deliveryTerms?: string;
  expectedDeliveryDate?: string;
  deliveryAddress?: string;
  status: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  discountAmount?: number;
  discountReason?: string;
  notes?: string;
  rowVersion: string;
  lines: PurchaseOrderLine[];
}

export interface PurchaseOrderLineInput {
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountAmount?: number;
  /** The item's base unit when left out. */
  unitId?: number;
  expectedDeliveryDate?: string;
  weight?: number;
  /** The request line this line came from, if any (Remarks7). */
  purchaseRequestLineId?: number;
}

export interface PurchaseOrderFormValues {
  branchId?: number;
  orderDate: string;
  supplierId: number;
  purchaseRequestId?: number;
  currencyCode: string;
  exchangeRate: number;
  paymentTerms: string;
  deliveryTerms?: string;
  expectedDeliveryDate?: string;
  deliveryAddress?: string;
  taxAmount: number;
  discountAmount?: number;
  discountReason?: string;
  notes?: string;
  lines: PurchaseOrderLineInput[];
}
