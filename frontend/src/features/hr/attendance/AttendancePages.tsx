import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { SectionTabs } from '../../../ui-kit/SectionTabs';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useEmployeesLookup } from '../employees/api';
import { useApproveAttendance, useAttendanceList, useRecomputeAttendance, type Attendance, type AttendanceStatus } from './api';

const STATUS_COLORS: Record<AttendanceStatus, string> = {
  Present: 'var(--color-success, #2e7d32)',
  Absent: 'var(--color-error, #c62828)',
  Leave: 'var(--color-info, #1565c0)',
  Holiday: 'var(--color-gold-500, #b8860b)',
  RestDay: 'var(--color-text-muted, #888)'
};

function monthRange(month: string): { from: string; to: string } {
  const [y, m] = month.split('-').map(Number);
  const from = `${month}-01`;
  const lastDay = new Date(y, m, 0).getDate();
  const to = `${month}-${String(lastDay).padStart(2, '0')}`;
  return { from, to };
}

/** /hr/attendance — screen HR_ATTENDANCE. تقويم الحضور Toggle جوه نفس الصفحة (Phase-3-Research.md نمط
 * — مفيش MenuItem/Screen منفصل للتقويم). */
export function AttendanceListPage() {
  const { t } = useTranslation();
  const canEdit = usePermission('edit');
  const showToast = useToastStore((s) => s.show);

  const [view, setView] = useState<'list' | 'calendar'>('list');
  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7));
  const { data: employees } = useEmployeesLookup();
  const employeeName = (id: number) => employees?.find((e) => e.id === id)?.nameAr ?? id;

  const { from, to } = monthRange(month);
  const { data: rows, isLoading } = useAttendanceList({ employeeId: employeeId === '' ? undefined : employeeId, fromDate: from, toDate: to });

  const recompute = useRecomputeAttendance();
  const approve = useApproveAttendance();

  const handleRecompute = async () => {
    if (employeeId === '') return;
    try {
      await recompute.mutateAsync({ employeeId, workDate: new Date().toISOString().slice(0, 10) });
      showToast(t('hr.attendance.daily.recomputeSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleApprove = async (id: number) => {
    try {
      await approve.mutateAsync(id);
      showToast(t('hr.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<Attendance>[] = useMemo(
    () => [
      { key: 'employeeId', label: t('hr.attendance.daily.employee'), render: (r) => employeeName(r.employeeId), exportValue: (r) => employeeName(r.employeeId) },
      { key: 'workDate', label: t('hr.attendance.daily.workDate'), render: (r) => r.workDate, exportValue: (r) => r.workDate },
      { key: 'status', label: t('hr.attendance.daily.status'), render: (r) => t(`hr.attendance.daily.statuses.${r.status}`), exportValue: (r) => r.status },
      { key: 'workedMinutes', label: t('hr.attendance.daily.workedMinutes'), render: (r) => r.workedMinutes, exportValue: (r) => r.workedMinutes },
      { key: 'lateMinutes', label: t('hr.attendance.daily.lateMinutes'), render: (r) => r.lateMinutes, exportValue: (r) => r.lateMinutes },
      { key: 'overtimeMinutes', label: t('hr.attendance.daily.overtimeMinutes'), render: (r) => r.overtimeMinutes, exportValue: (r) => r.overtimeMinutes },
      { key: 'isApproved', label: t('hr.attendance.daily.isApproved'), render: (r) => (r.isApproved ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isApproved ? t('common.yes') : t('common.no')) },
      {
        key: 'actions',
        label: '',
        render: (r) =>
          canEdit && !r.isApproved ? (
            <Button variant="secondary" onClick={(e) => { e.stopPropagation(); handleApprove(r.id); }}>{t('hr.attendance.daily.approve')}</Button>
          ) : null
      }
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, employees, canEdit]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.daily.title')}</h2>

      <SectionTabs
        items={[{ id: 'list', label: t('hr.attendance.daily.title') }, { id: 'calendar', label: t('hr.attendance.calendar.title') }]}
        activeId={view}
        onChange={(id) => setView(id as 'list' | 'calendar')}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
            <FieldWrapper label={t('hr.attendance.daily.employee')}>
              <SearchableSelect
                value={employeeId}
                onChange={(v) => setEmployeeId(v === '' ? '' : Number(v))}
                options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.daily.workDate')}>
              <Input type="month" value={month} onChange={(e) => setMonth(e.target.value)} />
            </FieldWrapper>
            {canEdit && (
              <Button variant="secondary" onClick={handleRecompute} disabled={employeeId === ''}>{t('hr.attendance.daily.recompute')}</Button>
            )}
          </div>
        </CardBody>
      </Card>

      {view === 'list' ? (
        <DataGrid
          columns={columns}
          data={{ items: rows ?? [], totalCount: (rows ?? []).length, page: 1, pageSize: Math.max((rows ?? []).length, 1) }}
          isLoading={isLoading}
          search=""
          onSearchChange={() => undefined}
          page={1}
          onPageChange={() => undefined}
          exportFileName={t('hr.attendance.daily.title')}
        />
      ) : (
        <AttendanceCalendarGrid month={month} rows={rows ?? []} />
      )}
    </div>
  );
}

function AttendanceCalendarGrid({ month, rows }: { month: string; rows: Attendance[] }) {
  const { t } = useTranslation();
  const [y, m] = month.split('-').map(Number);
  const daysInMonth = new Date(y, m, 0).getDate();
  const firstWeekday = new Date(y, m - 1, 1).getDay();
  const byDate = new Map(rows.map((r) => [r.workDate.slice(0, 10), r]));

  const cells: (Attendance | null)[] = [...Array(firstWeekday).fill(null), ...Array.from({ length: daysInMonth }, (_, i) => {
    const date = `${month}-${String(i + 1).padStart(2, '0')}`;
    return byDate.get(date) ?? null;
  })];

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 8 }}>
          {cells.map((cell, i) => (
            <div
              key={i}
              style={{
                minHeight: 64, borderRadius: 8, padding: 6, fontSize: 12,
                background: cell ? STATUS_COLORS[cell.status] + '22' : 'transparent',
                border: cell ? `1px solid ${STATUS_COLORS[cell.status]}` : '1px solid transparent'
              }}
            >
              {cell && (
                <>
                  <div style={{ fontWeight: 700 }}>{Number(cell.workDate.slice(8, 10))}</div>
                  <div>{t(`hr.attendance.daily.statuses.${cell.status}`)}</div>
                </>
              )}
            </div>
          ))}
        </div>
      </CardBody>
    </Card>
  );
}
