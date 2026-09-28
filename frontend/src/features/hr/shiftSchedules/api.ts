import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_SHIFT_SCHEDULES.
 * startDate/endDate بدل workDate (Remarks8 Item 5 — Phase 3 Amendment: فترة، مش يوم واحد بس). */

export interface ShiftSchedule {
  id: number;
  employeeId: number;
  branchId: number | null;
  startDate: string;
  endDate: string | null;
  workShiftDefinitionId: number | null;
  isRestDay: boolean;
}

export interface ShiftScheduleInput {
  employeeId: number;
  startDate: string;
  endDate: string | null;
  workShiftDefinitionId: number | null;
  isRestDay: boolean;
}

const BASE = '/hr/shift-schedules';

export function useShiftSchedules(params: { employeeId?: number; fromDate?: string; toDate?: string }) {
  return useQuery({
    queryKey: ['hr-shift-schedules', params],
    queryFn: async () => (await api.get<ShiftSchedule[]>(BASE, { params })).data
  });
}

export function useCreateShiftSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: ShiftScheduleInput) => (await api.post<{ id: number }>(BASE, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-shift-schedules'] })
  });
}

export function useUpdateShiftSchedule(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { startDate: string; endDate: string | null; workShiftDefinitionId: number | null; isRestDay: boolean }) => {
      await api.put(`${BASE}/${id}`, data);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-shift-schedules'] })
  });
}

export function useDeleteShiftSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-shift-schedules'] })
  });
}

/** DayOfWeek بيتبعت كنص ("Sunday".."Saturday") — الـ API عندها JsonStringEnumConverter عام. */
export function useGenerateWeeklySchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; weekStartDate: string; workShiftDefinitionId: number | null; restDayOfWeek: string | null }) =>
      (await api.post<{ created: number }>(`${BASE}/generate-weekly`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-shift-schedules'] })
  });
}

// ------------------------------------------------------------ Remarks8 Item 7 — Bulk Generator

export interface BulkScheduleException {
  employeeId: number;
  workShiftDefinitionId: number | null;
  weeklyRestDaysMask: number | null;
}

export interface GenerateBulkRequest {
  orgUnitId: number;
  fromDate: string;
  toDate: string;
  workShiftDefinitionId: number | null;
  weeklyRestDaysMask: number;
  exceptions: BulkScheduleException[] | null;
}

export interface BulkSchedulePreviewLine {
  employeeId: number;
  employeeCode: string;
  employeeNameAr: string;
  workingDays: number;
  restDays: number;
  alreadyScheduledDays: number;
}

export function usePreviewBulkShiftSchedules() {
  return useMutation({
    mutationFn: async (data: GenerateBulkRequest) => (await api.post<BulkSchedulePreviewLine[]>(`${BASE}/preview-bulk`, data)).data
  });
}

export function useGenerateBulkShiftSchedules() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: GenerateBulkRequest) => (await api.post<{ employeesProcessed: number; daysCreated: number }>(`${BASE}/generate-bulk`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-shift-schedules'] })
  });
}
