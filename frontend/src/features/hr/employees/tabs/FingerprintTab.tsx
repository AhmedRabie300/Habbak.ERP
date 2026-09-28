import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useAttendanceDevices } from '../../attendanceDevices/api';
import { useEmployeeDeviceMappings, useCreateEmployeeDeviceMapping, useDeleteEmployeeDeviceMapping } from '../../employeeDeviceMappings/api';

/** تبويب "ماكينة البصمة" (Docs/Implementation/Phase-3B-Research.md §6, Sub-Batch 3B.7) — نفس نمط
 * تبويبات المستندات/الشهادات: قائمة + إضافة بس، مفيش تعديل بعد الإنشاء (حذف وإعادة إضافة).
 * Validation: DeviceUserId غير مكرر لنفس الجهاز — بيترجع من الـBackend كـBusinessRuleException. */
export function FingerprintTab({ employeeId }: { employeeId: number }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: mappings, isLoading } = useEmployeeDeviceMappings({ employeeId });
  const { data: devices } = useAttendanceDevices();

  const [creating, setCreating] = useState(false);
  const [attendanceDeviceId, setAttendanceDeviceId] = useState<number | undefined>(undefined);
  const [deviceUserId, setDeviceUserId] = useState('');

  const create = useCreateEmployeeDeviceMapping();
  const remove = useDeleteEmployeeDeviceMapping();

  const startCreate = () => { setCreating(true); setAttendanceDeviceId(undefined); setDeviceUserId(''); };

  const handleCreate = async () => {
    if (!attendanceDeviceId || !deviceUserId) {
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

  if (isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        {!creating && <Button variant="primary" onClick={startCreate}>{t('hr.attendance.deviceMappings.add')}</Button>}
      </div>

      {creating && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
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

      {(!mappings || mappings.length === 0) && !creating && (
        <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
      )}

      {mappings && mappings.length > 0 && (
        <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.devices.title')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.deviceMappings.deviceUserId')}</th>
              <th style={{ textAlign: 'start', padding: 6 }} />
            </tr>
          </thead>
          <tbody>
            {mappings.map((m) => (
              <tr key={m.id} style={{ borderTop: '1px solid var(--color-border)' }}>
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
