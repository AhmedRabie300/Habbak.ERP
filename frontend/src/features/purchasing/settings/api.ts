import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { PurchaseCyclePreset, PurchaseCycleSettings } from './types';

const BASE = '/purchasing/settings';

export function usePurchaseCycleSettings() {
  return useQuery({
    queryKey: ['purchasing-settings', 'purchase-cycle'],
    queryFn: async () => (await api.get<PurchaseCycleSettings>(`${BASE}/purchase-cycle`)).data
  });
}

/** What each named cycle requires — the screen applies the matching preset when the type changes. */
export function usePurchaseCyclePresets() {
  return useQuery({
    queryKey: ['purchasing-settings', 'purchase-cycle', 'presets'],
    queryFn: async () => (await api.get<PurchaseCyclePreset[]>(`${BASE}/purchase-cycle/presets`)).data
  });
}

export function useUpdatePurchaseCycleSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseCycleSettings) => api.put(`${BASE}/purchase-cycle`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchasing-settings', 'purchase-cycle'] })
  });
}
