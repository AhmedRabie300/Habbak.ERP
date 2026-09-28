import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useBranchesList } from '../../organization/branches/api';
import { useAttendanceConflicts, type AttendanceConflict } from './api';

/** /hr/attendance-conflicts — screen HR_ATTENDANCE_CONFLICTS (قاعدة 12، كاشف احتيال). */
export function AttendanceConflictsPage() {
  const { t } = useTranslation();
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [branchId, setBranchId] = useState<number | ''>('');
  const { data: branches } = useBranchesList();
  const { data: conflicts, isLoading } = useAttendanceConflicts(date, branchId === '' ? undefined : branchId);
  // DataGrid<T extends { id: number }> — التقرير ده Read-only بلا Id خاص بيه، الـ posShiftId فريد
  // ضمن نتيجة اليوم الواحد فبيتستخدم كـ Id بديل هنا بس.
  const rows = (conflicts ?? []).map((c) => ({ ...c, id: c.posShiftId }));

  const columns: DataGridColumn<AttendanceConflict & { id: number }>[] = [
    { key: 'employeeCode', label: t('hr.attendance.conflicts.employeeCode'), render: (r) => r.employeeCode, exportValue: (r) => r.employeeCode },
    { key: 'employeeNameAr', label: t('hr.attendance.conflicts.employeeName'), render: (r) => r.employeeNameAr, exportValue: (r) => r.employeeNameAr },
    { key: 'posShiftId', label: t('hr.attendance.conflicts.posShift'), render: (r) => `#${r.posShiftId}`, exportValue: (r) => r.posShiftId },
    { key: 'description', label: t('hr.attendance.conflicts.description'), render: (r) => r.description, exportValue: (r) => r.description }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.conflicts.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
            <FieldWrapper label={t('hr.attendance.conflicts.date')}>
              <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.conflicts.branch')}>
              <SearchableSelect
                value={branchId}
                onChange={(v) => setBranchId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('hr.attendance.conflicts.allBranches') }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))]}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      {!isLoading && rows.length === 0 ? (
        <Card><CardBody>{t('hr.attendance.conflicts.empty')}</CardBody></Card>
      ) : (
        <DataGrid
          columns={columns}
          data={{ items: rows, totalCount: rows.length, page: 1, pageSize: Math.max(rows.length, 1) }}
          isLoading={isLoading}
          search=""
          onSearchChange={() => undefined}
          page={1}
          onPageChange={() => undefined}
          exportFileName={t('hr.attendance.conflicts.title')}
        />
      )}
    </div>
  );
}
