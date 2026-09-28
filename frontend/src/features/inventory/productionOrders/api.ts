import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type {
  CompleteProductionOrderInput, CompleteProductionOrderResult, ProductionOrderDetail, ProductionOrderFormValues, ProductionOrderListItem
} from './types';

const BASE = '/inventory/production-orders';

export function useProductionOrdersList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['production-orders', params],
    queryFn: async () => (await api.get<PagedResult<ProductionOrderListItem>>(BASE, { params })).data
  });
}

export function useProductionOrder(id: number | undefined) {
  return useQuery({
    queryKey: ['production-orders', id],
    queryFn: async () => (await api.get<ProductionOrderDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateProductionOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: ProductionOrderFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['production-orders'] })
  });
}

export function useUpdateProductionOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: ProductionOrderFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['production-orders'] })
  });
}

export function useStartProductionOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/start`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['production-orders'] })
  });
}

export function useCompleteProductionOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CompleteProductionOrderInput) =>
      (await api.post<CompleteProductionOrderResult>(`${BASE}/${id}/complete`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['production-orders'] })
  });
}

export function useCancelProductionOrder(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['production-orders'] })
  });
}
