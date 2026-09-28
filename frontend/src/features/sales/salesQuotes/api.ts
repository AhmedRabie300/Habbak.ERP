import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { AcceptedSalesQuote, SalesQuoteDetail, SalesQuoteFormValues, SalesQuoteListItem } from './types';

const BASE = '/sales/quotes';

export function useSalesQuotesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['sales-quotes', params],
    queryFn: async () => (await api.get<PagedResult<SalesQuoteListItem>>(BASE, { params })).data
  });
}

export function useSalesQuote(id: number | undefined) {
  return useQuery({
    queryKey: ['sales-quotes', id],
    queryFn: async () => (await api.get<SalesQuoteDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useAcceptedSalesQuotesList() {
  return useQuery({
    queryKey: ['sales-quotes', 'accepted'],
    queryFn: async () => (await api.get<AcceptedSalesQuote[]>(`${BASE}/accepted`)).data
  });
}

export function useCreateSalesQuote() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SalesQuoteFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-quotes'] })
  });
}

export function useUpdateSalesQuote(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SalesQuoteFormValues & { rowVersion: string }) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-quotes'] })
  });
}

export function useSubmitSalesQuote(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/submit`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-quotes'] })
  });
}

export function useAcceptSalesQuote(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/accept`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-quotes'] })
  });
}

export function useRejectSalesQuote(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-quotes'] })
  });
}
