import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type {
  CountedQuantityInput, CreateInventoryCountInput, InventoryCountDetail, InventoryCountListItem, SettleLineInput
} from './types';

const BASE = '/inventory/inventory-counts';

export function useInventoryCountsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['inventory-counts', params],
    queryFn: async () => (await api.get<PagedResult<InventoryCountListItem>>(BASE, { params })).data
  });
}

export function useInventoryCount(id: number | undefined) {
  return useQuery({
    queryKey: ['inventory-counts', id],
    queryFn: async () => (await api.get<InventoryCountDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateInventoryCount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateInventoryCountInput) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useStartInventoryCount(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/start`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useRecordCountedQuantities(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (lines: CountedQuantityInput[]) => api.post(`${BASE}/${id}/counted-quantities`, { lines }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useSubmitInventoryCountForSettlement(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/submit-for-settlement`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useSettleInventoryCountLine(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ lineId, input }: { lineId: number; input: SettleLineInput }) =>
      api.post(`${BASE}/${id}/lines/${lineId}/settle`, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useCompleteInventoryCountSettlement(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/complete-settlement`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useCloseInventoryCount(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<{ status: string; inventoryAdjustmentDocumentId?: number }>(`${BASE}/${id}/close`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useRejectInventoryCount(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}

export function useCancelInventoryCount(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-counts'] })
  });
}
