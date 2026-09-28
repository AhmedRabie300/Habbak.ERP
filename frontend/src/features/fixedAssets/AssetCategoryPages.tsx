import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../ui-kit/DataGrid';
import { Button } from '../../ui-kit/Button';
import { ActionBar } from '../../ui-kit/ActionBar';
import { Card, CardBody } from '../../ui-kit/Card';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { SearchableSelect } from '../../ui-kit/SearchableSelect';
import { usePermission } from '../../ui-kit/usePermission';
import { useToastStore } from '../../store/toastStore';
import { getFieldErrorMessage } from '../../app/api';
import { useCodingRule } from '../settings/codingRules/api';
import {
  useAssetCategories,
  useDeleteAssetCategory,
  useSaveAssetCategory,
  type DepreciationMethod,
  type FixedAssetCategory
} from './api';
import { toPaged, usePostableAccountOptions } from './listing';

const methods: DepreciationMethod[] = ['StraightLine', 'DecliningBalance', 'NoDepreciation'];

/** /fixed-assets/categories — screen #1: the accounts and depreciation defaults of each kind of asset. */
export function AssetCategoriesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useAssetCategories();

  const rows = toPaged(data, search, (c, q) => `${c.code} ${c.nameAr} ${c.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<FixedAssetCategory>[] = [
    { key: 'code', label: t('fixedAssets.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('fixedAssets.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    {
      key: 'method',
      label: t('fixedAssets.depreciationMethod'),
      render: (r) => t(`fixedAssets.methods.${r.depreciationMethod}`),
      exportValue: (r) => t(`fixedAssets.methods.${r.depreciationMethod}`)
    },
    {
      key: 'life',
      label: t('fixedAssets.usefulLifeYears'),
      render: (r) => r.defaultUsefulLifeYears ?? '—',
      exportValue: (r) => String(r.defaultUsefulLifeYears ?? '')
    },
    {
      key: 'rate',
      label: t('fixedAssets.depreciationRate'),
      render: (r) => (r.defaultDepreciationRate == null ? '—' : `${r.defaultDepreciationRate}%`),
      exportValue: (r) => String(r.defaultDepreciationRate ?? '')
    },
    {
      key: 'isActive',
      label: t('fixedAssets.isActive'),
      render: (r) => (r.isActive ? t('common.yes') : t('common.no')),
      exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no'))
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.categoriesTitle')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/fixed-assets/categories/new')}>
            {t('fixedAssets.addCategory')}
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
        onRowClick={(row) => navigate(`/fixed-assets/categories/${row.id}`)}
        exportFileName={t('fixedAssets.categoriesTitle')}
      />
    </div>
  );
}

/** /fixed-assets/categories/:id — the category's accounts are the ones every entry of its assets uses. */
export function AssetCategoryEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const categoryId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: categories, isLoading } = useAssetCategories();
  const category = categories?.find((c) => c.id === categoryId);
  const accountOptions = usePostableAccountOptions();
  const { data: codingRule } = useCodingRule('FIXED_ASSETS_CATEGORIES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [method, setMethod] = useState<DepreciationMethod>('StraightLine');
  const [rate, setRate] = useState<string>('');
  const [life, setLife] = useState<string>('');
  const [salvagePercentage, setSalvagePercentage] = useState<string>('');
  const [assetAccountId, setAssetAccountId] = useState<number | ''>('');
  const [accumulatedAccountId, setAccumulatedAccountId] = useState<number | ''>('');
  const [expenseAccountId, setExpenseAccountId] = useState<number | ''>('');
  const [gainAccountId, setGainAccountId] = useState<number | ''>('');
  const [lossAccountId, setLossAccountId] = useState<number | ''>('');
  const [maintenanceAccountId, setMaintenanceAccountId] = useState<number | ''>('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!category) return;
    setNameAr(category.nameAr);
    setNameEn(category.nameEn);
    setMethod(category.depreciationMethod);
    setRate(category.defaultDepreciationRate?.toString() ?? '');
    setLife(category.defaultUsefulLifeYears?.toString() ?? '');
    setSalvagePercentage(category.defaultSalvagePercentage?.toString() ?? '');
    setAssetAccountId(category.assetAccountId);
    setAccumulatedAccountId(category.accumulatedDepreciationAccountId);
    setExpenseAccountId(category.depreciationExpenseAccountId);
    setGainAccountId(category.disposalGainAccountId ?? '');
    setLossAccountId(category.disposalLossAccountId ?? '');
    setMaintenanceAccountId(category.maintenanceExpenseAccountId);
    setIsActive(category.isActive);
  }, [category]);

  const save = useSaveAssetCategory(categoryId);
  const remove = useDeleteAssetCategory();

  const handleSave = async () => {
    try {
      const data = {
        nameAr,
        nameEn,
        depreciationMethod: method,
        defaultDepreciationRate: rate === '' ? null : Number(rate),
        defaultUsefulLifeYears: life === '' ? null : Number(life),
        defaultSalvagePercentage: salvagePercentage === '' ? null : Number(salvagePercentage),
        assetAccountId: Number(assetAccountId),
        accumulatedDepreciationAccountId: Number(accumulatedAccountId),
        depreciationExpenseAccountId: Number(expenseAccountId),
        disposalGainAccountId: gainAccountId === '' ? null : Number(gainAccountId),
        disposalLossAccountId: lossAccountId === '' ? null : Number(lossAccountId),
        maintenanceExpenseAccountId: Number(maintenanceAccountId),
        isActive
      };
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        rowVersion: category?.rowVersion,
        data
      });
      showToast(t('fixedAssets.saveSuccess'), 'success');
      if (isNew) navigate(`/fixed-assets/categories/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!categoryId) return;
    try {
      await remove.mutateAsync(categoryId);
      showToast(t('fixedAssets.deleteSuccess'), 'success');
      navigate('/fixed-assets/categories');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  const accountField = (label: string, value: number | '', onChange: (v: number | '') => void, optional = false) => (
    <FieldWrapper label={label}>
      <SearchableSelect
        value={value}
        onChange={(v) => onChange(v === '' ? '' : Number(v))}
        options={optional ? [{ value: '', label: t('fixedAssets.none') }, ...accountOptions] : accountOptions}
        style={{ minWidth: 280 }}
      />
    </FieldWrapper>
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 900 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('fixedAssets.addCategory') : `${t('fixedAssets.categoriesTitle')} — ${category?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/categories') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('fixedAssets.deleteCategoryConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (category?.code ?? '')}
                onChange={(e) => setCode(e.target.value)}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.depreciationMethod')}>
              <SearchableSelect
                value={method}
                onChange={(v) => setMethod(v as DepreciationMethod)}
                options={methods.map((m) => ({ value: m, label: t(`fixedAssets.methods.${m}`) }))}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.usefulLifeYears')}>
              <Input type="number" value={life} onChange={(e) => setLife(e.target.value)} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.depreciationRate')}>
              <Input
                type="number"
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                disabled={method !== 'DecliningBalance'}
                style={{ width: 120 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.salvagePercentage')}>
              <Input type="number" value={salvagePercentage} onChange={(e) => setSalvagePercentage(e.target.value)} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.isActive')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <h3 style={{ marginTop: 0 }}>{t('fixedAssets.categoryAccounts')}</h3>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            {accountField(t('fixedAssets.assetAccount'), assetAccountId, setAssetAccountId)}
            {accountField(t('fixedAssets.accumulatedAccount'), accumulatedAccountId, setAccumulatedAccountId)}
            {accountField(t('fixedAssets.expenseAccount'), expenseAccountId, setExpenseAccountId)}
            {accountField(t('fixedAssets.gainAccount'), gainAccountId, setGainAccountId, true)}
            {accountField(t('fixedAssets.lossAccount'), lossAccountId, setLossAccountId, true)}
            {accountField(t('fixedAssets.maintenanceAccount'), maintenanceAccountId, setMaintenanceAccountId)}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
