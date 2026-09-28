import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { PurchaseRequestDetail, PurchaseRequestFormValues, PurchaseRequestListItem } from './types';

const BASE = '/purchasing/purchase-requests';

export function usePurchaseRequestsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['purchase-requests', params],
    queryFn: async () => (await api.get<PagedResult<PurchaseRequestListItem>>(BASE, { params })).data
  });
}

export function usePurchaseRequest(id: number | undefined) {
  return useQuery({
    queryKey: ['purchase-requests', id],
    queryFn: async () => (await api.get<PurchaseRequestDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePurchaseRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseRequestFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-requests'] })
  });
}

export function useUpdatePurchaseRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<PurchaseRequestFormValues, 'branchId'> & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-requests'] })
  });
}

export function useSubmitPurchaseRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/submit`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-requests'] })
  });
}

export function useApprovePurchaseRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/approve`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-requests'] })
  });
}

export function useRejectPurchaseRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-requests'] })
  });
}

export function useCancelPurchaseRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-requests'] })
  });
}
