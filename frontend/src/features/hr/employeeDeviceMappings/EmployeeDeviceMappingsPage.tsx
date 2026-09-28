import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Button } from '../../../ui-kit/Button';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useEmployeesLookup } from '../employees/api';
import { useAttendanceDevices } from '../attendanceDevices/api';
import { useEmployeeDeviceMappings, useCreateEmployeeDeviceMapping, useDeleteEmployeeDeviceMapping } from './api';

/** /hr/employee-device-mappings — screen HR_EMPLOYEE_DEVICE_MAPPINGS. */
export function EmployeeDeviceMappingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const canAdd = usePermission('add');

  const [deviceFilter, setDeviceFilter] = useState<number | undefined>(undefined);
  const { data: mappings, isLoading } = useEmployeeDeviceMappings({ attendanceDeviceId: deviceFilter });
  const { data: employees } = useEmployeesLookup();
  const { data: devices } = useAttendanceDevices();

  const [creating, setCreating] = useState(false);
  const [employeeId, setEmployeeId] = useState<number | undefined>(undefined);
  const [attendanceDeviceId, setAttendanceDeviceId] = useState<number | undefined>(undefined);
  const [deviceUserId, setDeviceUserId] = useState('');

  const create = useCreateEmployeeDeviceMapping();
  const remove = useDeleteEmployeeDeviceMapping();

  const startCreate = () => { setCreating(true); setEmployeeId(undefined); setAttendanceDeviceId(undefined); setDeviceUserId(''); };

  const handleCreate = async () => {
    if (!employeeId || !attendanceDeviceId || !deviceUserId) {
      showToast(t('hr.attendance.deviceMappings.requiredFields'), 'error');
      return;
    }
    try {
      await create.mutateAsync({ employeeId, attendanceDeviceId, deviceUserId });
      showToast(t('hr.saveSuccess'), 'success');
      setCreating(false);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await remove.mutateAsync(id);
      showToast(t('hr.deleteSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.deviceMappings.title')}</h2>
        {canAdd && !creating && <Button variant="primary" onClick={startCreate}>{t('hr.attendance.deviceMappings.add')}</Button>}
      </div>

      <FieldWrapper label={t('hr.attendance.devices.title')}>
        <SearchableSelect
          value={deviceFilter}
          onChange={(v) => setDeviceFilter(v ? Number(v) : undefined)}
          options={[{ value: '', label: t('common.all') }, ...(devices ?? []).map((d) => ({ value: d.id, label: `${d.code} — ${d.nameAr}` }))]}
          style={{ maxWidth: 320 }}
        />
      </FieldWrapper>

      {creating && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <FieldWrapper label={t('hr.employees.title')}>
                <SearchableSelect
                  value={employeeId}
                  onChange={(v) => setEmployeeId(Number(v))}
                  options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                  style={{ minWidth: 220 }}
                />
              </FieldWrapper>
              <FieldWrapper label={t('hr.attendance.devices.title')}>
                <SearchableSelect
                  value={attendanceDeviceId}
                  onChange={(v) => setAttendanceDeviceId(Number(v))}
                  options={(devices ?? []).map((d) => ({ value: d.id, label: `${d.code} — ${d.nameAr}` }))}
                  style={{ minWidth: 220 }}
                />
              </FieldWrapper>
              <FieldWrapper label={t('hr.attendance.deviceMappings.deviceUserId')}>
                <Input value={deviceUserId} onChange={(e) => setDeviceUserId(e.target.value)} style={{ width: 140 }} />
              </FieldWrapper>
              <Button variant="primary" onClick={handleCreate}>{t('common.save')}</Button>
              <Button variant="secondary" onClick={() => setCreating(false)}>{t('common.cancel')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      {isLoading && <div>{t('common.loading')}</div>}

      {!isLoading && (!mappings || mappings.length === 0) && <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>}

      {mappings && mappings.length > 0 && (
        <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.title')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.devices.title')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.deviceMappings.deviceUserId')}</th>
              <th style={{ textAlign: 'start', padding: 6 }} />
            </tr>
          </thead>
          <tbody>
            {mappings.map((m) => (
              <tr key={m.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                <td style={{ padding: 6 }}>{m.employeeCode} — {m.employeeNameAr}</td>
                <td style={{ padding: 6 }}>{m.attendanceDeviceCode}</td>
                <td style={{ padding: 6 }}>{m.deviceUserId}</td>
                <td style={{ padding: 6 }}>
                  <Button variant="secondary" size="sm" onClick={() => handleDelete(m.id)}>{t('common.remove')}</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
