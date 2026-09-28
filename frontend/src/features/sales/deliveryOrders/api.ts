import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { DeliveryOrderDetail, DeliveryOrderFormValues, DeliveryOrderListItem } from './types';

const BASE = '/sales/delivery-orders';

export function useDeliveryOrdersList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['delivery-orders', params],
    queryFn: async () => (await api.get<PagedResult<DeliveryOrderListItem>>(BASE, { params })).data
  });
}

export function useDeliveryOrder(id: number | undefined) {
  return useQuery({
    queryKey: ['delivery-orders', id],
    queryFn: async () => (await api.get<DeliveryOrderDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateDeliveryOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: DeliveryOrderFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['delivery-orders'] });
      queryClient.invalidateQueries({ queryKey: ['sales-orders'] });
    }
  });
}

export function useUpdateDeliveryOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<DeliveryOrderFormValues, 'customerId' | 'sourceOrderId' | 'sourceInvoiceId'> & { rowVersion: string }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['delivery-orders'] })
  });
}

export function usePostDeliveryOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['delivery-orders'] });
      queryClient.invalidateQueries({ queryKey: ['sales-orders'] });
    }
  });
}

export function useRejectDeliveryOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['delivery-orders'] })
  });
}
