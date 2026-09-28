import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { LoyaltyProgramSettings, SalesCycleSettings } from './types';

const BASE = '/sales/settings';

export function useSalesCycleSettings() {
  return useQuery({
    queryKey: ['sales-settings', 'sales-cycle'],
    queryFn: async () => (await api.get<SalesCycleSettings>(`${BASE}/sales-cycle`)).data
  });
}

export function useUpdateSalesCycleSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SalesCycleSettings) => api.put(`${BASE}/sales-cycle`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-settings', 'sales-cycle'] })
  });
}

export function useLoyaltyProgramSettings() {
  return useQuery({
    queryKey: ['sales-settings', 'loyalty-program'],
    queryFn: async () => (await api.get<LoyaltyProgramSettings>(`${BASE}/loyalty-program`)).data
  });
}

export function useUpdateLoyaltyProgramSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: LoyaltyProgramSettings) => api.put(`${BASE}/loyalty-program`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['sales-settings', 'loyalty-program'] })
  });
}
