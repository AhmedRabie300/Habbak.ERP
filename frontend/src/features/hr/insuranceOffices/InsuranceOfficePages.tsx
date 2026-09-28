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
import { useDeleteInsuranceOffice, useInsuranceOffices, useSaveInsuranceOffice, type InsuranceOffice } from './api';

/** /hr/insurance-offices — screen HR_INSURANCE_OFFICES. */
export function InsuranceOfficesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useInsuranceOffices();

  const rows = toPaged(data, search, (o, q) => `${o.code} ${o.nameAr} ${o.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<InsuranceOffice>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'officialCode', label: t('hr.insuranceOffices.officialCode'), render: (r) => r.officialCode ?? '—', exportValue: (r) => r.officialCode ?? '' },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.insuranceOffices.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/hr/insurance-offices/new')}>
            {t('hr.insuranceOffices.add')}
          </Button>
        )}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/insurance-offices/${row.id}`)}
        exportFileName={t('hr.insuranceOffices.title')}
      />
    </div>
  );
}

/** /hr/insurance-offices/:id */
export function InsuranceOfficeEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const officeId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: offices, isLoading } = useInsuranceOffices();
  const office = offices?.find((o) => o.id === officeId);
  const { data: codingRule } = useCodingRule('HR_INSURANCE_OFFICES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [officialCode, setOfficialCode] = useState('');
  const [address, setAddress] = useState('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!office) return;
    setNameAr(office.nameAr);
    setNameEn(office.nameEn);
    setOfficialCode(office.officialCode ?? '');
    setAddress(office.address ?? '');
    setIsActive(office.isActive);
  }, [office]);

  const save = useSaveInsuranceOffice(officeId);
  const remove = useDeleteInsuranceOffice();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: {
          nameAr,
          nameEn,
          officialCode: officialCode === '' ? null : officialCode,
          address: address === '' ? null : address,
          isActive
        }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/insurance-offices/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!officeId) return;
    try {
      await remove.mutateAsync(officeId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/insurance-offices');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.insuranceOffices.add') : `${t('hr.insuranceOffices.title')} — ${office?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/insurance-offices') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.insuranceOffices.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (office?.code ?? '')}
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
            <FieldWrapper label={t('hr.insuranceOffices.officialCode')}>
              <Input value={officialCode} onChange={(e) => setOfficialCode(e.target.value)} style={{ minWidth: 160 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.insuranceOffices.address')}>
              <Input value={address} onChange={(e) => setAddress(e.target.value)} style={{ minWidth: 260 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.isActive')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
