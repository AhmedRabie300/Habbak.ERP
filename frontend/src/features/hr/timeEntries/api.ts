import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_TIME_ENTRIES. */

export type TimeEntryType = 'In' | 'Out';
export type TimeEntrySource = 'Manual' | 'SelfService' | 'Kiosk' | 'POSShift' | 'Device';
export type TimeEntryStatus = 'Suggested' | 'Accepted' | 'Dismissed';

export interface TimeEntry {
  id: number;
  employeeId: number;
  entryType: TimeEntryType;
  timestampUtc: string;
  source: TimeEntrySource;
  posShiftId: number | null;
  deviceId: number | null;
  status: TimeEntryStatus;
  isCorrection: boolean;
  correctsTimeEntryId: number | null;
}

const BASE = '/hr/time-entries';

export function useTimeEntries(params: { employeeId?: number; fromDate?: string; toDate?: string; status?: TimeEntryStatus }) {
  return useQuery({
    queryKey: ['hr-time-entries', params],
    queryFn: async () => (await api.get<TimeEntry[]>(BASE, { params })).data
  });
}

export function useCreateTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; entryType: TimeEntryType; timestampUtc: string }) =>
      (await api.post<{ id: number }>(BASE, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-time-entries'] })
  });
}

export function useCorrectTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...data }: { id: number; entryType: TimeEntryType; timestampUtc: string; reason: string }) =>
      (await api.post<{ id: number }>(`${BASE}/${id}/correct`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-time-entries'] })
  });
}

export function useAcceptTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/accept`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-time-entries'] })
  });
}

export function useDismissTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/dismiss`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-time-entries'] })
  });
}
