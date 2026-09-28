import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_OVERTIME. */

export type OvertimeType = 'Day' | 'Night' | 'RestDay' | 'PublicHoliday';
export type HrRequestStatus = 'Draft' | 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';

export interface OvertimeRequest {
  id: number;
  employeeId: number;
  workDate: string;
  plannedMinutes: number;
  overtimeType: OvertimeType;
  reason: string | null;
  status: HrRequestStatus;
  approvalInstanceId: number | null;
  actualMinutes: number | null;
}

const BASE = '/hr/overtime-requests';

export function useOvertimeRequests(employeeId?: number) {
  return useQuery({
    queryKey: ['hr-overtime-requests', employeeId],
    queryFn: async () => (await api.get<OvertimeRequest[]>(BASE, { params: employeeId ? { employeeId } : undefined })).data
  });
}

export function useCreateOvertimeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; workDate: string; plannedMinutes: number; overtimeType: OvertimeType; reason: string | null }) =>
      (await api.post<{ id: number }>(BASE, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-overtime-requests'] })
  });
}

export function useSubmitOvertimeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/submit`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-overtime-requests'] })
  });
}

export function useCancelOvertimeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/cancel`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-overtime-requests'] })
  });
}
