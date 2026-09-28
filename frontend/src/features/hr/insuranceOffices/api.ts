import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — HR_INSURANCE_OFFICES. */

export interface InsuranceOffice {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  officialCode: string | null;
  address: string | null;
}

export type InsuranceOfficeInput = Omit<InsuranceOffice, 'id' | 'code'>;

const BASE = '/hr/insurance-offices';

export function useInsuranceOffices() {
  return useQuery({
    queryKey: ['hr-insurance-offices'],
    queryFn: async () => (await api.get<InsuranceOffice[]>(BASE)).data
  });
}

export function useSaveInsuranceOffice(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: InsuranceOfficeInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-insurance-offices'] })
  });
}

export function useDeleteInsuranceOffice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-insurance-offices'] })
  });
}
