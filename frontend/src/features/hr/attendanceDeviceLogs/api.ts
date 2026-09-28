import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/Phase-3B-Research.md §Phase 3B — HR_ATTENDANCE_DEVICE_LOGS. */

export interface AttendanceDeviceLog {
  id: number;
  attendanceDeviceId: number;
  deviceCode: string;
  syncType: number; // 1=Push, 2=FileImport
  startedAtUtc: string;
  finishedAtUtc: string | null;
  status: number; // 1=Success, 2=PartialFailure, 3=Failed
  punchesReceived: number;
  punchesProcessed: number;
  errorMessage: string | null;
}

export function useAttendanceDeviceLogs(filter: { attendanceDeviceId?: number; fromDate?: string; toDate?: string } = {}) {
  return useQuery({
    queryKey: ['hr-attendance-device-logs', filter.attendanceDeviceId, filter.fromDate, filter.toDate],
    queryFn: async () => (await api.get<AttendanceDeviceLog[]>('/hr/attendance-device-logs', { params: filter })).data
  });
}
