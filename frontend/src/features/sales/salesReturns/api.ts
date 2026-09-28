import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { SalesReturnDetail, SalesReturnFormValues, SalesReturnListItem } from './types';

const BASE = '/sales/returns';

export function useSalesReturnsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['sales-returns', params],
    queryFn: async () => (await api.get<PagedResult<SalesReturnListItem>>(BASE, { params })).data
  });
}

export function useSalesReturn(id: number | undefined) {
  return useQuery({
    queryKey: ['sales-returns', id],
    queryFn: async () => (await api.get<SalesReturnDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateSalesReturn() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SalesReturnFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-returns'] })
  });
}

export function useUpdateSalesReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<SalesReturnFormValues, 'sourceInvoiceId'> & { rowVersion: string }) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-returns'] })
  });
}

export function usePostSalesReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-returns'] })
  });
}

export function useRejectSalesReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-returns'] })
  });
}

export function useCancelSalesReturn(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-returns'] })
  });
}
