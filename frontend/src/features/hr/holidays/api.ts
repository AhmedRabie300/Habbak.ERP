import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_HOLIDAYS. */

/** startDate/endDate بدل date (Remarks8 Item 8 — Phase 3 Amendment: عيد الفطر من... إلى...). */
export interface Holiday {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  startDate: string;
  endDate: string | null;
  year: number;
  isNational: boolean;
  branchId: number | null;
}

export type HolidayInput = Omit<Holiday, 'id' | 'code' | 'year'>;

const BASE = '/hr/holidays';

export function useHolidays(year?: number) {
  return useQuery({
    queryKey: ['hr-holidays', year],
    queryFn: async () => (await api.get<Holiday[]>(BASE, { params: year ? { year } : undefined })).data
  });
}

export function useSaveHoliday(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: HolidayInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-holidays'] })
  });
}

export function useDeleteHoliday() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-holidays'] })
  });
}
