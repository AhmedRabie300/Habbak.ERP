import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { UserName, useUserNameOf, useUsersLookup } from '../../settings/security/UserName';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useCreateShiftAssignment, useDeleteShiftAssignment, useShiftAssignmentsList } from './api';
import type { ShiftAssignment } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { todayLocal } from '../../../lib/date';

/** /pos/shift-assignments — قاعدة 31 (الكاشير المعيَّن على جهاز بعينه في يوم بعينه). لا يوجد
 * تعديل بعد الإنشاء — إضافة أو حذف فقط. */
export function ShiftAssignmentsListPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: assignments, isLoading } = useShiftAssignmentsList();
  const { data: terminals } = usePOSTerminalsList();
  const createMutation = useCreateShiftAssignment();
  const deleteMutation = useDeleteShiftAssignment();

  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');
  const [userId, setUserId] = useState<number | ''>('');
  const { data: users } = useUsersLookup();
  const nameOf = useUserNameOf();
  const [assignedDate, setAssignedDate] = useState(todayLocal());

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));

  const data: PagedResult<ShiftAssignment> = {
    items: assignments ?? [],
    totalCount: assignments?.length ?? 0,
    page: 1,
    pageSize: Math.max(assignments?.length ?? 1, 1)
  };

  const columns: DataGridColumn<ShiftAssignment>[] = [
    { key: 'posTerminalNameAr', label: t('shiftAssignments.terminal'), render: (r) => r.posTerminalNameAr, exportValue: (r) => r.posTerminalNameAr },
    { key: 'userId', label: t('shiftAssignments.userId'), render: (r) => <UserName id={r.userId} />, exportValue: (r) => nameOf(r.userId) },
    { key: 'assignedDate', label: t('shiftAssignments.assignedDate'), render: (r) => r.assignedDate, exportValue: (r) => r.assignedDate },
    {
      key: 'actions', label: '', render: (r) => (
        <Button
          variant="ghost"
          onClick={async () => {
            try {
              await deleteMutation.mutateAsync(r.id);
              showToast(t('shiftAssignments.deleteSuccess'), 'success');
            } catch (error) {
              const message = getFieldErrorMessage(error);
              if (message) showToast(message, 'error');
            }
          }}
        >
          {t('common.remove')}
        </Button>
      ),
      exportValue: () => ''
    }
  ];

  const handleAdd = async () => {
    if (posTerminalId === '' || !userId) return;
    try {
      await createMutation.mutateAsync({ posTerminalId: Number(posTerminalId), userId: Number(userId), assignedDate });
      showToast(t('shiftAssignments.createSuccess'), 'success');
      setUserId('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('shiftAssignments.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('shiftAssignments.terminal')}>
              <SearchableSelect style={{ minWidth: 220 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('shiftAssignments.userId')}>
              <SearchableSelect
                id="assignment-user"
                style={{ minWidth: 200 }}
                value={userId}
                onChange={(v) => setUserId(v === '' ? '' : Number(v))}
                options={(users ?? []).filter((u) => u.isActive).map((u) => ({ value: u.id, label: u.fullName }))}
              />
            </FieldWrapper>
            <FieldWrapper label={t('shiftAssignments.assignedDate')}>
              <Input type="date" style={{ width: 160 }} value={assignedDate} onChange={(e) => setAssignedDate(e.target.value)} />
            </FieldWrapper>
            <Button variant="primary" onClick={handleAdd}>{t('shiftAssignments.addAssignment')}</Button>
          </div>
        </CardBody>
      </Card>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search=""
        onSearchChange={() => {}}
        page={1}
        onPageChange={() => {}}
        exportFileName={t('shiftAssignments.title')}
      />
    </div>
  );
}
