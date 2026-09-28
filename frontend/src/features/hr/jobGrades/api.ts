import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — HR_JOB_GRADES. */

export interface JobGrade {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  level: number;
  minSalary: number | null;
  maxSalary: number | null;
}

export type JobGradeInput = Omit<JobGrade, 'id' | 'code'>;

const BASE = '/hr/job-grades';

export function useJobGrades() {
  return useQuery({
    queryKey: ['hr-job-grades'],
    queryFn: async () => (await api.get<JobGrade[]>(BASE)).data
  });
}

export function useSaveJobGrade(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: JobGradeInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-job-grades'] })
  });
}

export function useDeleteJobGrade() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-job-grades'] })
  });
}
