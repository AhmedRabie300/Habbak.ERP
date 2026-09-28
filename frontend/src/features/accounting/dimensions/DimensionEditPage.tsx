import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody, CardHeader } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { ConfirmModal } from '../../../ui-kit/Modal';
import { Icon } from '../../../ui-kit/Icon';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import {
  useCreateDimension,
  useCreateDimensionValue,
  useDeleteDimension,
  useDeleteDimensionValue,
  useDimension,
  useDimensionValues,
  useUpdateDimension,
  LINKED_ENTITY_TYPES,
  isMirroredLinkedType,
  type LinkedEntityType
} from './api';

/** /accounting/dimensions/:id — My Remarks/Remarks2.md, remark 2.1: the standard Edit screen for
 * one cost center, replacing the old dual-panel single-page layout. Values management stays
 * embedded here (a dimension's values only make sense in its own context) rather than becoming a
 * separate List/Edit pair of their own — the same nested-collection pattern ItemEditPage uses for
 * its UnitConversions/WarehouseSettings. */
export function DimensionEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const dimensionId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: dimension, isLoading } = useDimension(dimensionId);
  const { data: values } = useDimensionValues(dimensionId);
  const { label } = useFieldLabels('ACCOUNTING_DIMENSIONS');
  const { data: dimensionCodingRule } = useCodingRule('ACCOUNTING_DIMENSIONS');
  const codeIsAutomatic = dimensionCodingRule?.isAutomatic ?? false;
  const { data: valueCodingRule } = useCodingRule('ACCOUNTING_DIMENSION_VALUES');
  const valueCodeIsAutomatic = valueCodingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [linkedEntityType, setLinkedEntityType] = useState<LinkedEntityType>('None');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (dimension) {
      setNameAr(dimension.nameAr);
      setNameEn(dimension.nameEn);
      setLinkedEntityType(dimension.linkedEntityType);
      setIsActive(dimension.isActive);
    }
  }, [dimension]);

  const createMutation = useCreateDimension();
  const updateMutation = useUpdateDimension(dimensionId ?? 0);
  const deleteMutation = useDeleteDimension();

  const [valueCode, setValueCode] = useState('');
  const [valueNameAr, setValueNameAr] = useState('');
  const [valueNameEn, setValueNameEn] = useState('');
  const [valueParentId, setValueParentId] = useState<number | ''>('');
  const createValue = useCreateDimensionValue(dimensionId ?? 0);
  const deleteValue = useDeleteDimensionValue(dimensionId ?? 0);
  const [pendingDeleteValueId, setPendingDeleteValueId] = useState<number | null>(null);

  const handleSave = async () => {
    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          code: codeIsAutomatic ? undefined : code,
          nameAr,
          nameEn,
          linkedEntityType
        });
        showToast(t('dimensions.createDimensionSuccess'), 'success');
        navigate(`/accounting/dimensions/${newId}`);
      } else {
        await updateMutation.mutateAsync({ nameAr, nameEn, isActive });
        showToast(t('dimensions.updateDimensionSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!dimensionId) return;
    try {
      await deleteMutation.mutateAsync(dimensionId);
      showToast(t('dimensions.deleteDimensionSuccess'), 'success');
      navigate('/accounting/dimensions');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleCreateValue = async () => {
    if (!dimensionId) return;
    try {
      await createValue.mutateAsync({
        code: valueCodeIsAutomatic ? undefined : valueCode,
        nameAr: valueNameAr,
        nameEn: valueNameEn,
        parentId: valueParentId === '' ? undefined : Number(valueParentId)
      });
      showToast(t('dimensions.createValueSuccess'), 'success');
      setValueCode('');
      setValueNameAr('');
      setValueNameEn('');
      setValueParentId('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDeleteValue = async () => {
    if (!pendingDeleteValueId) return;
    try {
      await deleteValue.mutateAsync(pendingDeleteValueId);
      showToast(t('dimensions.deleteValueSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    } finally {
      setPendingDeleteValueId(null);
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 720 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('dimensions.addDimension') : `${t('dimensions.title')} — ${dimension?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/accounting/dimensions') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('dimensions.deleteDimensionConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={label('code', t('dimensions.code'))}>
              {codeIsAutomatic ? (
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (dimension?.code ?? '')} disabled />
              ) : (
                <Input value={isNew ? code : (dimension?.code ?? '')} onChange={(e) => setCode(e.target.value)} disabled={!isNew} style={{ width: 100 }} />
              )}
            </FieldWrapper>
            <FieldWrapper label={label('nameAr', t('dimensions.nameAr'))}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={label('nameEn', t('dimensions.nameEn'))}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={label('linkedEntityType', t('dimensions.linkedEntityType'))}>
              <SearchableSelect
                disabled={!isNew}
                value={linkedEntityType}
                onChange={(v) => setLinkedEntityType(v as LinkedEntityType)}
                options={[
                  ...LINKED_ENTITY_TYPES.map((type) => ({ value: type, label: t(`dimensions.linkedType.${type}`) }))
                ]}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>
            {!isNew && (
              <FieldWrapper label={t('dimensions.isActive')}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                </label>
              </FieldWrapper>
            )}
          </div>
        </CardBody>
      </Card>

      {!isNew && (
        <Card>
          <CardHeader>{t('dimensions.valuesHeading')}</CardHeader>
          <CardBody>
            <table style={{ width: '100%', fontSize: 13, marginBottom: 16 }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 6 }}>{label('code', t('dimensions.code'))}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{label('nameAr', t('dimensions.nameAr'))}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{label('nameEn', t('dimensions.nameEn'))}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{label('level', t('dimensions.level'))}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('dimensions.isActive')}</th>
                  {!isMirroredLinkedType(linkedEntityType) && <th />}
                </tr>
              </thead>
              <tbody>
                {values?.map((v) => (
                  <tr key={v.id} style={{ opacity: v.isActive ? 1 : 0.5 }}>
                    <td style={{ padding: 6 }}>{v.code}</td>
                    <td style={{ padding: 6 }}>{'—'.repeat(v.level)} {v.nameAr}</td>
                    <td style={{ padding: 6 }}>{v.nameEn}</td>
                    <td style={{ padding: 6 }}>{v.level}</td>
                    <td style={{ padding: 6 }}>{v.isActive ? t('common.yes') : t('common.no')}</td>
                    {!isMirroredLinkedType(linkedEntityType) && (
                      <td style={{ padding: 6 }}>
                        <span
                          onClick={() => setPendingDeleteValueId(v.id)}
                          title={t('common.remove')}
                          style={{ cursor: 'pointer', opacity: 0.7, display: 'inline-flex' }}
                        >
                          <Icon name="trash" size={14} />
                        </span>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>

            {isMirroredLinkedType(linkedEntityType) ? (
              <p style={{ fontSize: 12.5, color: 'var(--color-text-muted)' }}>
                {t('dimensions.linkedValuesNote', { screen: t(`dimensions.linkedType.${linkedEntityType}`) })}
              </p>
            ) : (
              <>
                {linkedEntityType === 'Cashier' && (
                  <p style={{ fontSize: 12.5, color: 'var(--color-text-muted)', margin: '0 0 8px' }}>{t('dimensions.cashierValuesNote')}</p>
                )}
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
                  {valueCodeIsAutomatic && linkedEntityType !== 'Cashier' ? (
                    <Input value={t('codingRules.autoGeneratedPlaceholder')} disabled style={{ width: 130 }} />
                  ) : (
                    <Input placeholder={label('code', t('dimensions.code'))} value={valueCode} onChange={(e) => setValueCode(e.target.value)} style={{ width: 90 }} />
                  )}
                  <Input placeholder={label('nameAr', t('dimensions.nameAr'))} value={valueNameAr} onChange={(e) => setValueNameAr(e.target.value)} style={{ width: 140 }} />
                  <Input placeholder={label('nameEn', t('dimensions.nameEn'))} value={valueNameEn} onChange={(e) => setValueNameEn(e.target.value)} style={{ width: 140 }} />
                  <SearchableSelect
                    value={valueParentId}
                    onChange={(v) => setValueParentId(v === '' ? '' : Number(v))}
                    options={[
                      { value: '', label: t('dimensions.noParent') },
                      ...(values?.map((v) => ({ value: v.id, label: v.nameAr })) ?? [])
                    ]}
                    style={{ width: 140 }}
                  />
                  <button
                    type="button"
                    onClick={handleCreateValue}
                    style={{
                      padding: '9px 16px', borderRadius: 'var(--radius-chip)', border: 'none',
                      background: 'var(--color-navy-700)', color: '#fff', cursor: 'pointer', fontFamily: 'inherit', fontSize: 13
                    }}
                  >
                    {t('dimensions.addValue')}
                  </button>
                </div>
              </>
            )}
          </CardBody>
        </Card>
      )}

      <ConfirmModal
        open={pendingDeleteValueId !== null}
        title={t('common.delete')}
        message={t('dimensions.deleteValueConfirm')}
        onCancel={() => setPendingDeleteValueId(null)}
        onConfirm={handleDeleteValue}
      />
    </div>
  );
}
