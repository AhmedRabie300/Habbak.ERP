import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { PurchaseInvoiceDetail, PurchaseInvoiceFormValues, PurchaseInvoiceListItem } from './types';

const BASE = '/purchasing/purchase-invoices';

export function usePurchaseInvoicesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['purchase-invoices', params],
    queryFn: async () => (await api.get<PagedResult<PurchaseInvoiceListItem>>(BASE, { params })).data
  });
}

export function usePurchaseInvoice(id: number | undefined) {
  return useQuery({
    queryKey: ['purchase-invoices', id],
    queryFn: async () => (await api.get<PurchaseInvoiceDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePurchaseInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseInvoiceFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] })
  });
}

export function useUpdatePurchaseInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseInvoiceFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] })
  });
}

export function useSubmitPurchaseInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/submit`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] })
  });
}

export function usePostPurchaseInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] })
  });
}

export function useRejectPurchaseInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] })
  });
}

export function useCancelPurchaseInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] })
  });
}
