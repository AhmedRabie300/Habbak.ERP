import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — SETTINGS_COUNTRIES. */

export interface Country {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  isoCode: string | null;
}

export type CountryInput = Omit<Country, 'id' | 'code'>;

const BASE = '/organization/countries';

export function useCountries() {
  return useQuery({
    queryKey: ['settings-countries'],
    queryFn: async () => (await api.get<Country[]>(BASE)).data
  });
}

export function useSaveCountry(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: CountryInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings-countries'] })
  });
}

export function useDeleteCountry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings-countries'] })
  });
}
