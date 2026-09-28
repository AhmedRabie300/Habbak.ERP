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
import { useDeleteDocumentType, useDocumentTypes, useSaveDocumentType, type DocumentType } from './api';

/** /hr/document-types — screen HR_DOCUMENT_TYPES. */
export function DocumentTypesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useDocumentTypes();

  const rows = toPaged(data, search, (d, q) => `${d.code} ${d.nameAr} ${d.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<DocumentType>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'isMandatory', label: t('hr.documentTypes.isMandatory'), render: (r) => (r.isMandatory ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isMandatory ? t('common.yes') : t('common.no')) },
    { key: 'requiresExpiry', label: t('hr.documentTypes.requiresExpiry'), render: (r) => (r.requiresExpiry ? t('common.yes') : t('common.no')), exportValue: (r) => (r.requiresExpiry ? t('common.yes') : t('common.no')) },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.documentTypes.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/hr/document-types/new')}>
            {t('hr.documentTypes.add')}
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
        onRowClick={(row) => navigate(`/hr/document-types/${row.id}`)}
        exportFileName={t('hr.documentTypes.title')}
      />
    </div>
  );
}

/** /hr/document-types/:id */
export function DocumentTypeEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const typeId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: types, isLoading } = useDocumentTypes();
  const type = types?.find((d) => d.id === typeId);
  const { data: codingRule } = useCodingRule('HR_DOCUMENT_TYPES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [requiresExpiry, setRequiresExpiry] = useState(false);
  const [isMandatory, setIsMandatory] = useState(false);
  const [expiryAlertDays, setExpiryAlertDays] = useState('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!type) return;
    setNameAr(type.nameAr);
    setNameEn(type.nameEn);
    setRequiresExpiry(type.requiresExpiry);
    setIsMandatory(type.isMandatory);
    setExpiryAlertDays(type.expiryAlertDays?.toString() ?? '');
    setIsActive(type.isActive);
  }, [type]);

  const save = useSaveDocumentType(typeId);
  const remove = useDeleteDocumentType();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: {
          nameAr,
          nameEn,
          requiresExpiry,
          isMandatory,
          expiryAlertDays: expiryAlertDays === '' ? null : Number(expiryAlertDays),
          isActive
        }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/document-types/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!typeId) return;
    try {
      await remove.mutateAsync(typeId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/document-types');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.documentTypes.add') : `${t('hr.documentTypes.title')} — ${type?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/document-types') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.documentTypes.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (type?.code ?? '')}
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
            <FieldWrapper label={t('hr.documentTypes.isMandatory')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isMandatory} onChange={(e) => setIsMandatory(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.documentTypes.requiresExpiry')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={requiresExpiry} onChange={(e) => setRequiresExpiry(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.documentTypes.expiryAlertDays')}>
              <Input
                type="number"
                value={expiryAlertDays}
                onChange={(e) => setExpiryAlertDays(e.target.value)}
                disabled={!requiresExpiry}
                style={{ width: 120 }}
              />
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
