import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';

/**
 * The purchase invoice's link to its order (Remarks6): which of the supplier's orders still have
 * something left to bill, and what each one's lines still owe.
 */
export interface OpenPurchaseOrder {
  id: number;
  orderNumber: string;
  orderDate: string;
  status: string;
  currencyCode: string;
  totalAmount: number;
  remainingLineCount: number;
  completionPercentage: number;
}

export interface PurchaseOrderLineForInvoice {
  purchaseOrderLineId: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  unitId: number;
  unitCode: string;
  unitFactor: number;
  orderedQuantity: number;
  invoicedQuantity: number;
  remainingQuantity: number;
  receivedQuantity: number;
  unitPrice: number;
  discountAmount?: number;
}

const BASE = '/purchasing/purchase-orders';

/** Only asked once a supplier is chosen — an invoice with no supplier has no orders to offer. */
export function useOpenPurchaseOrders(supplierId: number | undefined) {
  return useQuery({
    queryKey: ['purchase-orders', 'open-for-invoice', supplierId],
    queryFn: async () => (await api.get<OpenPurchaseOrder[]>(`${BASE}/open-for-invoice`, { params: { supplierId } })).data,
    enabled: supplierId !== undefined && supplierId > 0
  });
}

/** `excludeInvoiceId` gives the invoice being edited its own quantities back before counting what is left. */
export function usePurchaseOrderLinesForInvoice(purchaseOrderId: number | undefined, excludeInvoiceId?: number) {
  return useQuery({
    queryKey: ['purchase-orders', purchaseOrderId, 'invoice-lines', excludeInvoiceId],
    queryFn: async () =>
      (await api.get<PurchaseOrderLineForInvoice[]>(`${BASE}/${purchaseOrderId}/invoice-lines`, { params: { excludeInvoiceId } })).data,
    enabled: purchaseOrderId !== undefined && purchaseOrderId > 0
  });
}
