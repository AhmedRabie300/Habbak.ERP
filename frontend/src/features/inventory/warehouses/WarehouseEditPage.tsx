import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import { useBranchesList } from '../../organization/branches/api';
import { useCreateWarehouse, useDeleteWarehouse, useWarehousesList, useUpdateWarehouse } from './api';
import type { WarehouseType } from './api';

const warehouseTypeOptions: { value: WarehouseType; labelKey: string }[] = [
  { value: 'Main', labelKey: 'warehouses.typeMain' },
  { value: 'BranchMaterials', labelKey: 'warehouses.typeBranchMaterials' },
  { value: 'Production', labelKey: 'warehouses.typeProduction' },
  { value: 'DamagedReturns', labelKey: 'warehouses.typeDamagedReturns' },
  { value: 'FinishedGoods', labelKey: 'warehouses.typeFinishedGoods' }
];

/** /inventory/warehouses/:id — Edit screen (standard List/Edit pattern, 00-Frontend-Specs.md, section 5-7).
 * Rule 24 (02-Module-Inventory-Manufacturing.md): Main warehouses have no branch; BranchMaterials/
 * Production warehouses must have one — enforced here by only showing the branch field when relevant. */
export function WarehouseEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const warehouseId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: warehouses, isLoading } = useWarehousesList();
  const warehouse = warehouses?.find((w) => w.id === warehouseId);
  const { data: branches } = useBranchesList();
  const { data: codingRule } = useCodingRule('INVENTORY_WAREHOUSES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [warehouseType, setWarehouseType] = useState<WarehouseType>('Main');
  const [branchId, setBranchId] = useState<number | ''>('');
  const [allowNegativeBalance, setAllowNegativeBalance] = useState(false);
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (warehouse) {
      setNameAr(warehouse.nameAr);
      setNameEn(warehouse.nameEn);
      setWarehouseType(warehouse.warehouseType);
      setBranchId(warehouse.branchId ?? '');
      setAllowNegativeBalance(warehouse.allowNegativeBalance);
      setIsActive(warehouse.isActive);
    }
  }, [warehouse]);

  const createMutation = useCreateWarehouse();
  const updateMutation = useUpdateWarehouse(warehouseId ?? 0);
  const deleteMutation = useDeleteWarehouse();

  const requiresBranch = warehouseType === 'BranchMaterials' || warehouseType === 'Production' || warehouseType === 'FinishedGoods';

  const branchOptions = (branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }));

  const handleWarehouseTypeChange = (value: string) => {
    const nextType = value as WarehouseType;
    setWarehouseType(nextType);
    if (nextType === 'Main') setBranchId('');
  };

  const handleSave = async () => {
    try {
      const resolvedBranchId = requiresBranch ? (branchId === '' ? null : Number(branchId)) : null;
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({ code: codeIsAutomatic ? undefined : code, nameAr, nameEn, warehouseType, branchId: resolvedBranchId, allowNegativeBalance });
        showToast(t('warehouses.createSuccess'), 'success');
        navigate(`/inventory/warehouses/${newId}`);
      } else {
        await updateMutation.mutateAsync({ nameAr, nameEn, warehouseType, branchId: resolvedBranchId, allowNegativeBalance, isActive });
        showToast(t('warehouses.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!warehouseId) return;
    try {
      await deleteMutation.mutateAsync(warehouseId);
      showToast(t('warehouses.deleteSuccess'), 'success');
      navigate('/inventory/warehouses');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('warehouses.addWarehouse') : `${t('warehouses.title')} — ${warehouse?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/warehouses') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('warehouses.deleteConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('warehouses.code')}>
              {codeIsAutomatic ? (
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (warehouse?.code ?? '')} disabled />
              ) : (
                <Input value={isNew ? code : (warehouse?.code ?? '')} onChange={(e) => setCode(e.target.value)} disabled={!isNew} />
              )}
            </FieldWrapper>

            <FieldWrapper label={t('warehouses.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('warehouses.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('warehouses.warehouseType')}>
              <SearchableSelect
                value={warehouseType}
                onChange={handleWarehouseTypeChange}
                options={warehouseTypeOptions.map((o) => ({ value: o.value, label: t(o.labelKey) }))}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            {requiresBranch && (
              <FieldWrapper label={t('warehouses.branch')}>
                <SearchableSelect
                  value={branchId}
                  onChange={(v) => setBranchId(v === '' ? '' : Number(v))}
                  options={branchOptions}
                  style={{ minWidth: 200 }}
                />
              </FieldWrapper>
            )}

            <FieldWrapper label={t('warehouses.allowNegativeBalance')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={allowNegativeBalance} onChange={(e) => setAllowNegativeBalance(e.target.checked)} />
              </label>
            </FieldWrapper>

            {!isNew && (
              <FieldWrapper label={t('warehouses.isActive')}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                </label>
              </FieldWrapper>
            )}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
