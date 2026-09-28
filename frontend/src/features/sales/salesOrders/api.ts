import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { ConfirmedSalesOrder, SalesOrderDetail, SalesOrderFormValues, SalesOrderListItem } from './types';

const BASE = '/sales/sales-orders';

export function useConfirmedSalesOrdersList() {
  return useQuery({
    queryKey: ['sales-orders', 'confirmed'],
    queryFn: async () => (await api.get<ConfirmedSalesOrder[]>(`${BASE}/confirmed`)).data
  });
}

export function useSalesOrdersList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['sales-orders', params],
    queryFn: async () => (await api.get<PagedResult<SalesOrderListItem>>(BASE, { params })).data
  });
}

export function useSalesOrder(id: number | undefined) {
  return useQuery({
    queryKey: ['sales-orders', id],
    queryFn: async () => (await api.get<SalesOrderDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateSalesOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SalesOrderFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sales-orders'] });
      queryClient.invalidateQueries({ queryKey: ['sales-quotes'] });
    }
  });
}

export function useUpdateSalesOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<SalesOrderFormValues, 'sourceQuoteId'> & { rowVersion: string }) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-orders'] })
  });
}

export function useConfirmSalesOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/confirm`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-orders'] })
  });
}

export function useRejectSalesOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-orders'] })
  });
}

export function useCancelSalesOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-orders'] })
  });
}
