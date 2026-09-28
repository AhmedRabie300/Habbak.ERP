import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { LoyaltyTierDetail, LoyaltyTierFormValues, LoyaltyTierListItem } from './types';

const BASE = '/sales/loyalty-tiers';

export function useLoyaltyTiersList() {
  return useQuery({
    queryKey: ['loyalty-tiers'],
    queryFn: async () => (await api.get<LoyaltyTierListItem[]>(BASE)).data
  });
}

export function useLoyaltyTier(id: number | undefined) {
  return useQuery({
    queryKey: ['loyalty-tiers', id],
    queryFn: async () => (await api.get<LoyaltyTierDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateLoyaltyTier() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: LoyaltyTierFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['loyalty-tiers'] })
  });
}

export function useUpdateLoyaltyTier(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: LoyaltyTierFormValues) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['loyalty-tiers'] })
  });
}

export function useDeleteLoyaltyTier() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      queryClient.removeQueries({ queryKey: ['loyalty-tiers', id] });
      queryClient.invalidateQueries({ queryKey: ['loyalty-tiers'] });
    }
  });
}
