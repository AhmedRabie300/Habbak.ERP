import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useAttendanceDevices } from '../attendanceDevices/api';
import { useRawPunchReconciliation } from './api';

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function daysAgoIso(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

/** /hr/attendance-reconciliation — screen HR_ATTENDANCE_RECONCILIATION. شاشة عرض بس (§5). */
export function AttendanceReconciliationPage() {
  const { t } = useTranslation();
  const [deviceFilter, setDeviceFilter] = useState<number | undefined>(undefined);
  const [fromDate, setFromDate] = useState(daysAgoIso(7));
  const [toDate, setToDate] = useState(todayIso());
  const { data: devices } = useAttendanceDevices();
  const { data, isLoading } = useRawPunchReconciliation({ attendanceDeviceId: deviceFilter, fromDate, toDate });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.reconciliation.title')}</h2>

      <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
        <FieldWrapper label={t('hr.attendance.devices.title')}>
          <SearchableSelect
            value={deviceFilter}
            onChange={(v) => setDeviceFilter(v ? Number(v) : undefined)}
            options={[{ value: '', label: t('common.all') }, ...(devices ?? []).map((d) => ({ value: d.id, label: `${d.code} — ${d.nameAr}` }))]}
            style={{ minWidth: 260 }}
          />
        </FieldWrapper>
        <FieldWrapper label={t('common.fromDate')}>
          <Input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </FieldWrapper>
        <FieldWrapper label={t('common.toDate')}>
          <Input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </FieldWrapper>
      </div>

      {isLoading && <div>{t('common.loading')}</div>}

      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {[
              { label: t('hr.attendance.reconciliation.totalReceived'), value: data.summary.totalReceived },
              { label: t('hr.attendance.reconciliation.processed'), value: data.summary.processed },
              { label: t('hr.attendance.reconciliation.pending'), value: data.summary.pending },
              { label: t('hr.attendance.reconciliation.skippedNoMapping'), value: data.summary.skippedNoEmployeeMapping },
              { label: t('hr.attendance.reconciliation.skippedOther'), value: data.summary.skippedOther }
            ].map((tile) => (
              <Card key={tile.label} style={{ minWidth: 160 }}>
                <CardBody>
                  <p style={{ margin: 0, fontSize: 12, color: 'var(--color-text-muted)' }}>{tile.label}</p>
                  <p style={{ margin: '4px 0 0', fontSize: 24, fontWeight: 700 }}>{tile.value}</p>
                </CardBody>
              </Card>
            ))}
          </div>

          {data.exceptions.length === 0 ? (
            <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
          ) : (
            <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.devices.title')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.deviceMappings.deviceUserId')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.reconciliation.punchTime')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.reconciliation.status')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.reconciliation.skipReason')}</th>
                </tr>
              </thead>
              <tbody>
                {data.exceptions.map((row) => (
                  <tr key={row.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                    <td style={{ padding: 6 }}>{row.deviceCode}</td>
                    <td style={{ padding: 6 }}>{row.deviceUserId}</td>
                    <td style={{ padding: 6 }}>{new Date(row.punchTimestampUtc).toLocaleString()}</td>
                    <td style={{ padding: 6 }}>{row.processingStatus}</td>
                    <td style={{ padding: 6 }}>{row.skipReason ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </div>
  );
}
