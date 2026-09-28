import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { PostedSalesInvoice, SalesInvoiceDetail, SalesInvoiceFormValues, SalesInvoiceListItem } from './types';

const BASE = '/sales/invoices';

export function usePostedSalesInvoicesList() {
  return useQuery({
    queryKey: ['sales-invoices', 'posted'],
    queryFn: async () => (await api.get<PostedSalesInvoice[]>(`${BASE}/posted`)).data
  });
}

export function useSalesInvoicesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['sales-invoices', params],
    queryFn: async () => (await api.get<PagedResult<SalesInvoiceListItem>>(BASE, { params })).data
  });
}

export function useSalesInvoice(id: number | undefined) {
  return useQuery({
    queryKey: ['sales-invoices', id],
    queryFn: async () => (await api.get<SalesInvoiceDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateSalesInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SalesInvoiceFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sales-invoices'] });
      queryClient.invalidateQueries({ queryKey: ['sales-orders'] });
    }
  });
}

export function useUpdateSalesInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<SalesInvoiceFormValues, 'sourceOrderId'> & { rowVersion: string }) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
  });
}

export function usePostSalesInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
  });
}

export function useRejectSalesInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
  });
}

export function useCancelSalesInvoice(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-invoices'] })
  });
}
