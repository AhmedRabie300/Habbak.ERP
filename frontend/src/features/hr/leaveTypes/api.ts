import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_LEAVE_TYPES. */

export type LeaveAccrualMethod = 'Monthly' | 'Annual' | 'Hourly' | 'None';
export type Gender = 'Male' | 'Female';

export interface LeaveType {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  accrualMethod: LeaveAccrualMethod;
  annualDays: number;
  maxCarryOver: number;
  isPaid: boolean;
  paidPercentage: number;
  deductFromLeaveTypeId: number | null;
  requiresDocument: boolean;
  maxDaysPerRequest: number | null;
  genderRestriction: Gender | null;
  maxTimesInService: number | null;
  isCashableOnTermination: boolean;
}

export type LeaveTypeInput = Omit<LeaveType, 'id' | 'code'>;

const BASE = '/hr/leave-types';

export function useLeaveTypes() {
  return useQuery({
    queryKey: ['hr-leave-types'],
    queryFn: async () => (await api.get<LeaveType[]>(BASE)).data
  });
}

export function useSaveLeaveType(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: LeaveTypeInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-leave-types'] })
  });
}

export function useDeleteLeaveType() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-leave-types'] })
  });
}
