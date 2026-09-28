import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_ATTENDANCE / HR_ATTENDANCE_CONFLICTS. */

export type AttendanceStatus = 'Present' | 'Absent' | 'Leave' | 'Holiday' | 'RestDay';

export interface Attendance {
  id: number;
  employeeId: number;
  workDate: string;
  shiftScheduleId: number | null;
  firstInUtc: string | null;
  lastOutUtc: string | null;
  workedMinutes: number;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  overtimeMinutes: number;
  status: AttendanceStatus;
  isApproved: boolean;
}

export interface AttendanceConflict {
  employeeId: number;
  employeeCode: string;
  employeeNameAr: string;
  workDate: string;
  posShiftId: number;
  description: string;
}

const BASE = '/hr/attendance';

export function useAttendanceList(params: { employeeId?: number; fromDate?: string; toDate?: string }) {
  return useQuery({
    queryKey: ['hr-attendance', params],
    queryFn: async () => (await api.get<Attendance[]>(BASE, { params })).data
  });
}

export function useRecomputeAttendance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; workDate: string }) => (await api.post<{ id: number }>(`${BASE}/recompute`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-attendance'] })
  });
}

export function useApproveAttendance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/approve`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-attendance'] })
  });
}

export function useAttendanceConflicts(date: string, branchId?: number) {
  return useQuery({
    queryKey: ['hr-attendance-conflicts', date, branchId],
    queryFn: async () => (await api.get<AttendanceConflict[]>(`${BASE}/conflicts`, { params: { date, branchId } })).data,
    enabled: !!date
  });
}
