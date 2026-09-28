import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — SETTINGS_BANKS. */

export interface Bank {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  swiftCode: string | null;
  address: string | null;
  countryId: number | null;
}

export type BankInput = Omit<Bank, 'id' | 'code'>;

const BASE = '/organization/banks';

export function useBanks() {
  return useQuery({
    queryKey: ['settings-banks'],
    queryFn: async () => (await api.get<Bank[]>(BASE)).data
  });
}

export function useSaveBank(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: BankInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings-banks'] })
  });
}

export function useDeleteBank() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['settings-banks'] })
  });
}
