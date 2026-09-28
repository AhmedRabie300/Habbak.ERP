import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { PurchaseOrderDetail, PurchaseOrderFormValues, PurchaseOrderListItem } from './types';

const BASE = '/purchasing/purchase-orders';

export function usePurchaseOrdersList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['purchase-orders', params],
    queryFn: async () => (await api.get<PagedResult<PurchaseOrderListItem>>(BASE, { params })).data
  });
}

export function usePurchaseOrder(id: number | undefined) {
  return useQuery({
    queryKey: ['purchase-orders', id],
    queryFn: async () => (await api.get<PurchaseOrderDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePurchaseOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseOrderFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchase-orders'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-requests'] });
    }
  });
}

export function useUpdatePurchaseOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<PurchaseOrderFormValues, 'purchaseRequestId'> & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-orders'] })
  });
}

export function useSendPurchaseOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/send`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-orders'] })
  });
}

export function useConfirmPurchaseOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/confirm`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-orders'] })
  });
}

export function useRejectPurchaseOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-orders'] })
  });
}

export function useCancelPurchaseOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-orders'] })
  });
}
