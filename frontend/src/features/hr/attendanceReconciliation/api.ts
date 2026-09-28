import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/Phase-3B-Research.md §5 — HR_ATTENDANCE_RECONCILIATION. */

export interface RawPunchReconciliationSummary {
  totalReceived: number;
  processed: number;
  pending: number;
  skippedNoEmployeeMapping: number;
  skippedOther: number;
}

export interface RawPunchException {
  id: number;
  attendanceDeviceId: number;
  deviceCode: string;
  deviceUserId: string;
  punchTimestampUtc: string;
  processingStatus: string;
  skipReason: string | null;
}

export interface RawPunchReconciliationResult {
  summary: RawPunchReconciliationSummary;
  exceptions: RawPunchException[];
}

export function useRawPunchReconciliation(params: { attendanceDeviceId?: number; fromDate: string; toDate: string }) {
  return useQuery({
    queryKey: ['hr-attendance-reconciliation', params.attendanceDeviceId, params.fromDate, params.toDate],
    queryFn: async () => (await api.get<RawPunchReconciliationResult>('/hr/attendance-reconciliation', { params })).data
  });
}
