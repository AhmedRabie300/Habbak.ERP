import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';

/**
 * The purchase order's link to its request (Remarks7): which of the company's requests still have
 * something left to convert, and what each one's lines still owe.
 */
export interface OpenPurchaseRequest {
  id: number;
  requestNumber: string;
  requestDate: string;
  status: string;
  remainingLineCount: number;
  completionPercentage: number;
}

export interface PurchaseRequestLineForOrder {
  purchaseRequestLineId: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  unitId: number;
  unitCode: string;
  unitFactor: number;
  requestedQuantity: number;
  orderedQuantity: number;
  remainingQuantity: number;
  unitPrice: number;
}

const BASE = '/purchasing/purchase-requests';

/** A request carries no SupplierId (Remarks7), unlike orders-by-supplier (Remarks6) — no filter needed. */
export function useOpenPurchaseRequests() {
  return useQuery({
    queryKey: ['purchase-requests', 'open-for-order'],
    queryFn: async () => (await api.get<OpenPurchaseRequest[]>(`${BASE}/open-for-order`)).data
  });
}

/** `excludeOrderId` gives the order being edited its own quantities back before counting what is left. */
export function usePurchaseRequestLinesForOrder(purchaseRequestId: number | undefined, excludeOrderId?: number) {
  return useQuery({
    queryKey: ['purchase-requests', purchaseRequestId, 'order-lines', excludeOrderId],
    queryFn: async () =>
      (await api.get<PurchaseRequestLineForOrder[]>(`${BASE}/${purchaseRequestId}/order-lines`, { params: { excludeOrderId } })).data,
    enabled: purchaseRequestId !== undefined && purchaseRequestId > 0
  });
}
