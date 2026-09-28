import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { InvoiceLineForReturn, PurchaseReturnDetail, PurchaseReturnFormValues, PurchaseReturnListItem } from './types';

const BASE = '/purchasing/purchase-returns';

export function usePurchaseReturnsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['purchase-returns', params],
    queryFn: async () => (await api.get<PagedResult<PurchaseReturnListItem>>(BASE, { params })).data
  });
}

export function usePurchaseReturn(id: number | undefined) {
  return useQuery({
    queryKey: ['purchase-returns', id],
    queryFn: async () => (await api.get<PurchaseReturnDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

/** The chosen invoice's lines, with how much of each can still go back (Remarks4, item 8). */
export function useInvoiceLinesForReturn(invoiceId: number | undefined, excludeReturnId?: number) {
  return useQuery({
    queryKey: ['purchase-returns', 'invoice-lines', invoiceId, excludeReturnId],
    queryFn: async () =>
      (await api.get<InvoiceLineForReturn[]>(`${BASE}/invoice-lines`, { params: { invoiceId, excludeReturnId } })).data,
    enabled: invoiceId !== undefined
  });
}

export function useCreatePurchaseReturn() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseReturnFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-returns'] })
  });
}

export function useUpdatePurchaseReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseReturnFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-returns'] })
  });
}

export function usePostPurchaseReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchase-returns'] });
      queryClient.invalidateQueries({ queryKey: ['stock-balances'] });
    }
  });
}

export function useCancelPurchaseReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-returns'] })
  });
}
