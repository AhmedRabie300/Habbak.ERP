import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_LEAVE_REQUESTS. */

export type HrRequestStatus = 'Draft' | 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export type LeaveDayCountingMode = 'Calendar' | 'WorkingDays';

export interface LeaveRequest {
  id: number;
  employeeId: number;
  leaveTypeId: number;
  startDate: string;
  endDate: string;
  days: number;
  reason: string | null;
  status: HrRequestStatus;
  approvalInstanceId: number | null;
}

export interface CalculateLeaveDaysResult {
  days: number;
  mode: LeaveDayCountingMode;
  usedFallback: boolean;
}

const BASE = '/hr/leave-requests';

export function useLeaveRequests(employeeId?: number) {
  return useQuery({
    queryKey: ['hr-leave-requests', employeeId],
    queryFn: async () => (await api.get<LeaveRequest[]>(BASE, { params: employeeId ? { employeeId } : undefined })).data
  });
}

export function useCalculateLeaveDays(employeeId: number | '', startDate: string, endDate: string) {
  return useQuery({
    queryKey: ['hr-leave-calculate-days', employeeId, startDate, endDate],
    queryFn: async () => (await api.get<CalculateLeaveDaysResult>(`${BASE}/calculate-days`, { params: { employeeId, startDate, endDate } })).data,
    enabled: employeeId !== '' && !!startDate && !!endDate
  });
}

export function useCreateLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; leaveTypeId: number; startDate: string; endDate: string; reason: string | null }) =>
      (await api.post<{ id: number }>(BASE, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-leave-requests'] })
  });
}

export function useSubmitLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/submit`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-leave-requests'] })
  });
}

export function useCancelLeaveRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/cancel`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-leave-requests'] })
  });
}
