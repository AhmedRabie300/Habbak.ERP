import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_WORK_SHIFTS. */

export interface WorkShiftDefinition {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  isNightShift: boolean;
}

export type WorkShiftDefinitionInput = Omit<WorkShiftDefinition, 'id' | 'code'>;

const BASE = '/hr/work-shifts';

export function useWorkShiftDefinitions() {
  return useQuery({
    queryKey: ['hr-work-shifts'],
    queryFn: async () => (await api.get<WorkShiftDefinition[]>(BASE)).data
  });
}

export function useSaveWorkShiftDefinition(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: WorkShiftDefinitionInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-work-shifts'] })
  });
}

export function useDeleteWorkShiftDefinition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-work-shifts'] })
  });
}
