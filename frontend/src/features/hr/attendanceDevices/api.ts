import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/Phase-3B-Research.md §Phase 3B — HR_ATTENDANCE_DEVICES. */

export interface AttendanceDevice {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  model: string | null;
  serialNumber: string | null;
  branchId: number | null;
  isActive: boolean;
  lastSeenAtUtc: string | null;
}

export type AttendanceDeviceInput = Omit<AttendanceDevice, 'id' | 'code' | 'lastSeenAtUtc'>;

export interface AttendanceDeviceSecret {
  id: number;
  deviceSecret: string;
}

export interface IngestResult {
  received: number;
  accepted: number;
  duplicates: number;
}

const BASE = '/hr/attendance-devices';

export function useAttendanceDevices() {
  return useQuery({
    queryKey: ['hr-attendance-devices'],
    queryFn: async () => (await api.get<AttendanceDevice[]>(BASE)).data
  });
}

export function useSaveAttendanceDevice(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: AttendanceDeviceInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<AttendanceDeviceSecret>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-attendance-devices'] })
  });
}

export function useDeleteAttendanceDevice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-attendance-devices'] })
  });
}

export function useRegenerateDeviceSecret() {
  return useMutation({
    mutationFn: async (id: number) => (await api.post<AttendanceDeviceSecret>(`${BASE}/${id}/regenerate-secret`)).data
  });
}

/** Sub-Batch 3B.5 — رفع يدوي (csv/txt/dat/xlsx)، نفس مسار المعالجة بتاع الـPush. */
export function useImportAttendanceDeviceFile(deviceId: number) {
  return useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData();
      formData.append('file', file);
      return (await api.post<IngestResult>(`${BASE}/${deviceId}/import`, formData, { headers: { 'Content-Type': 'multipart/form-data' } })).data;
    }
  });
}
