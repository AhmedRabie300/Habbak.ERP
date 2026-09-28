import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import { toPaged } from '../listing';
import {
  useAttendanceDevices, useSaveAttendanceDevice, useDeleteAttendanceDevice, useRegenerateDeviceSecret, useImportAttendanceDeviceFile,
  type AttendanceDevice
} from './api';

/** /hr/attendance-devices — screen HR_ATTENDANCE_DEVICES. */
export function AttendanceDevicesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useAttendanceDevices();

  const rows = toPaged(data, search, (d, q) => `${d.code} ${d.nameAr} ${d.nameEn} ${d.serialNumber ?? ''}`.toLowerCase().includes(q));

  const columns: DataGridColumn<AttendanceDevice>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'model', label: t('hr.attendance.devices.model'), render: (r) => r.model ?? '—', exportValue: (r) => r.model ?? '' },
    { key: 'serialNumber', label: t('hr.attendance.devices.serialNumber'), render: (r) => r.serialNumber ?? '—', exportValue: (r) => r.serialNumber ?? '' },
    {
      key: 'lastSeenAtUtc', label: t('hr.attendance.devices.lastSeen'),
      render: (r) => (r.lastSeenAtUtc ? new Date(r.lastSeenAtUtc).toLocaleString() : '—'),
      exportValue: (r) => r.lastSeenAtUtc ?? ''
    },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.devices.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/hr/attendance-devices/new')}>{t('hr.attendance.devices.add')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/attendance-devices/${row.id}`)}
        exportFileName={t('hr.attendance.devices.title')}
      />
    </div>
  );
}

/** /hr/attendance-devices/:id */
export function AttendanceDeviceEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const deviceId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: devices, isLoading } = useAttendanceDevices();
  const device = devices?.find((d) => d.id === deviceId);
  const { data: codingRule } = useCodingRule('HR_ATTENDANCE_DEVICES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [model, setModel] = useState('');
  const [serialNumber, setSerialNumber] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);

  useEffect(() => {
    if (!device) return;
    setNameAr(device.nameAr);
    setNameEn(device.nameEn);
    setModel(device.model ?? '');
    setSerialNumber(device.serialNumber ?? '');
    setIsActive(device.isActive);
  }, [device]);

  const save = useSaveAttendanceDevice(deviceId);
  const remove = useDeleteAttendanceDevice();
  const regenerateSecret = useRegenerateDeviceSecret();
  const importFile = useImportAttendanceDeviceFile(deviceId ?? 0);

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: { nameAr, nameEn, model: model || null, serialNumber: serialNumber || null, branchId: device?.branchId ?? null, isActive }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) {
        const created = result as { id: number; deviceSecret?: string };
        if (created.deviceSecret) setRevealedSecret(created.deviceSecret);
        navigate(`/hr/attendance-devices/${result.id}`);
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!deviceId) return;
    try {
      await remove.mutateAsync(deviceId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/attendance-devices');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleRegenerateSecret = async () => {
    if (!deviceId) return;
    try {
      const result = await regenerateSecret.mutateAsync(deviceId);
      setRevealedSecret(result.deviceSecret);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleImport = async (file: File) => {
    try {
      const result = await importFile.mutateAsync(file);
      showToast(t('hr.attendance.devices.importResult', { accepted: result.accepted, duplicates: result.duplicates, received: result.received }), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.attendance.devices.add') : `${t('hr.attendance.devices.title')} — ${device?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/attendance-devices') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.attendance.devices.deleteConfirm') }] : []}
      />

      {revealedSecret && (
        <Card>
          <CardBody>
            <p style={{ fontWeight: 600, margin: '0 0 4px' }}>{t('hr.attendance.devices.secretRevealTitle')}</p>
            <p style={{ fontSize: 12, color: 'var(--color-text-muted)', margin: '0 0 8px' }}>{t('hr.attendance.devices.secretRevealHint')}</p>
            <code style={{ display: 'block', padding: 10, background: 'var(--color-surface-2)', borderRadius: 6, wordBreak: 'break-all' }}>{revealedSecret}</code>
          </CardBody>
        </Card>
      )}

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (device?.code ?? '')}
                onChange={(e) => setCode(e.target.value)}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.devices.model')}>
              <Input value={model} onChange={(e) => setModel(e.target.value)} placeholder="ZKTeco K40" style={{ minWidth: 180 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.devices.serialNumber')}>
              <Input value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} style={{ minWidth: 180 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.isActive')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      {!isNew && (
        <Card>
          <CardBody>
            <p style={{ fontWeight: 600, margin: '0 0 12px' }}>{t('hr.attendance.devices.secretSectionTitle')}</p>
            <Button variant="secondary" onClick={handleRegenerateSecret}>{t('hr.attendance.devices.regenerateSecret')}</Button>

            <p style={{ fontWeight: 600, margin: '20px 0 8px' }}>{t('hr.attendance.devices.importSectionTitle')}</p>
            <p style={{ fontSize: 12, color: 'var(--color-text-muted)', margin: '0 0 8px' }}>{t('hr.attendance.devices.importHint')}</p>
            <input type="file" accept=".csv,.txt,.dat,.xlsx" onChange={(e) => e.target.files?.[0] && handleImport(e.target.files[0])} />
          </CardBody>
        </Card>
      )}
    </div>
  );
}
