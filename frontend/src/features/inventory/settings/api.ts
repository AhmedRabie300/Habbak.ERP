import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { CreateProductionSalesModeSettingInput, InventorySettings, ProductionSalesModeSetting, ShortagePolicy } from './types';

const BASE = '/inventory/settings';

export function useShortagePolicy() {
  return useQuery({
    queryKey: ['inventory-settings', 'shortage-policy'],
    queryFn: async () => (await api.get<ShortagePolicy>(`${BASE}/shortage-policy`)).data
  });
}

export function useUpdateShortagePolicy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: ShortagePolicy) => api.put(`${BASE}/shortage-policy`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-settings', 'shortage-policy'] })
  });
}

export function useInventorySettings() {
  return useQuery({
    queryKey: ['inventory-settings', 'general'],
    queryFn: async () => (await api.get<InventorySettings>(`${BASE}/inventory-settings`)).data
  });
}

export function useUpdateInventorySettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: InventorySettings) => api.put(`${BASE}/inventory-settings`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-settings', 'general'] })
  });
}

export function useProductionSalesModeSettingsList() {
  return useQuery({
    queryKey: ['inventory-settings', 'production-sales-mode'],
    queryFn: async () => (await api.get<ProductionSalesModeSetting[]>(`${BASE}/production-sales-mode`)).data
  });
}

export function useCreateProductionSalesModeSetting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateProductionSalesModeSettingInput) =>
      (await api.post<{ id: number }>(`${BASE}/production-sales-mode`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-settings', 'production-sales-mode'] })
  });
}

export function useUpdateProductionSalesModeSetting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, mode }: { id: number; mode: string }) => api.put(`${BASE}/production-sales-mode/${id}`, { mode }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-settings', 'production-sales-mode'] })
  });
}

export function useDeleteProductionSalesModeSetting() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/production-sales-mode/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory-settings', 'production-sales-mode'] })
  });
}
