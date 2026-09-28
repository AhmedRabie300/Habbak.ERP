import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { FieldWrapper } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useAttendanceDevices } from '../attendanceDevices/api';
import { useAttendanceDeviceLogs, type AttendanceDeviceLog } from './api';

const SYNC_TYPE_LABEL: Record<number, string> = { 1: 'Push', 2: 'FileImport' };
const STATUS_LABEL: Record<number, string> = { 1: 'Success', 2: 'PartialFailure', 3: 'Failed' };

/** /hr/attendance-device-logs — screen HR_ATTENDANCE_DEVICE_LOGS. شاشة عرض بس (§6: 3B.6). */
export function AttendanceDeviceLogsPage() {
  const { t } = useTranslation();
  const [deviceFilter, setDeviceFilter] = useState<number | undefined>(undefined);
  const { data: devices } = useAttendanceDevices();
  const { data, isLoading } = useAttendanceDeviceLogs({ attendanceDeviceId: deviceFilter });

  const columns: DataGridColumn<AttendanceDeviceLog>[] = [
    { key: 'deviceCode', label: t('hr.attendance.devices.title'), render: (r) => r.deviceCode, exportValue: (r) => r.deviceCode },
    { key: 'syncType', label: t('hr.attendance.deviceLogs.syncType'), render: (r) => SYNC_TYPE_LABEL[r.syncType] ?? r.syncType, exportValue: (r) => String(r.syncType) },
    { key: 'startedAtUtc', label: t('hr.attendance.deviceLogs.startedAt'), render: (r) => new Date(r.startedAtUtc).toLocaleString(), exportValue: (r) => r.startedAtUtc },
    { key: 'status', label: t('hr.attendance.deviceLogs.status'), render: (r) => STATUS_LABEL[r.status] ?? r.status, exportValue: (r) => String(r.status) },
    { key: 'punchesReceived', label: t('hr.attendance.deviceLogs.received'), render: (r) => r.punchesReceived, exportValue: (r) => String(r.punchesReceived) },
    { key: 'punchesProcessed', label: t('hr.attendance.deviceLogs.processed'), render: (r) => r.punchesProcessed, exportValue: (r) => String(r.punchesProcessed) },
    { key: 'errorMessage', label: t('hr.attendance.deviceLogs.error'), render: (r) => r.errorMessage ?? '—', exportValue: (r) => r.errorMessage ?? '' }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.deviceLogs.title')}</h2>

      <FieldWrapper label={t('hr.attendance.devices.title')}>
        <SearchableSelect
          value={deviceFilter}
          onChange={(v) => setDeviceFilter(v ? Number(v) : undefined)}
          options={[{ value: '', label: t('common.all') }, ...(devices ?? []).map((d) => ({ value: d.id, label: `${d.code} — ${d.nameAr}` }))]}
          style={{ maxWidth: 320 }}
        />
      </FieldWrapper>

      <DataGrid
        columns={columns}
        data={{ items: data ?? [], totalCount: data?.length ?? 0, page: 1, pageSize: Math.max(data?.length ?? 1, 1) }}
        isLoading={isLoading}
        search=""
        onSearchChange={() => undefined}
        page={1}
        onPageChange={() => undefined}
        exportFileName={t('hr.attendance.deviceLogs.title')}
      />
    </div>
  );
}
