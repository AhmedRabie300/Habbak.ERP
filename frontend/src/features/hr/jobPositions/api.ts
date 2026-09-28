import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — HR_JOB_POSITIONS. */

export interface JobPosition {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  orgUnitId: number | null;
  defaultJobGradeId: number | null;
}

export type JobPositionInput = Omit<JobPosition, 'id' | 'code'>;

const BASE = '/hr/job-positions';

export function useJobPositions() {
  return useQuery({
    queryKey: ['hr-job-positions'],
    queryFn: async () => (await api.get<JobPosition[]>(BASE)).data
  });
}

export function useSaveJobPosition(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: JobPositionInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-job-positions'] })
  });
}

export function useDeleteJobPosition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-job-positions'] })
  });
}
