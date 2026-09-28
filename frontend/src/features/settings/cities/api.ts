import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — SETTINGS_CITIES. */

export interface City {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  countryId: number;
}

export type CityInput = Omit<City, 'id' | 'code'>;

const BASE = '/organization/cities';

export function useCities() {
  return useQuery({
    queryKey: ['settings-cities'],
    queryFn: async () => (await api.get<City[]>(BASE)).data
  });
}

export function useSaveCity(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: CityInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings-cities'] })
  });
}

export function useDeleteCity() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings-cities'] })
  });
}
