import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../ui-kit/DataGrid';
import { Button } from '../../ui-kit/Button';
import { Badge } from '../../ui-kit/Badge';
import { ActionBar } from '../../ui-kit/ActionBar';
import { Card, CardBody } from '../../ui-kit/Card';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { SearchableSelect } from '../../ui-kit/SearchableSelect';
import { usePermission } from '../../ui-kit/usePermission';
import { useToastStore } from '../../store/toastStore';
import { getFieldErrorMessage } from '../../app/api';
import { useBranchesList } from '../organization/branches/api';
import { useCustodyOfficersList } from '../inventory/custodyOfficers/api';
import { useSuppliersList } from '../purchasing/suppliers/api';
import { useCodingRule } from '../settings/codingRules/api';
import {
  useActivateFixedAsset,
  useAssetCategories,
  useDeleteFixedAsset,
  useFixedAsset,
  useFixedAssets,
  useSaveFixedAsset,
  type DepreciationMethod,
  type FixedAssetListItem,
  type FixedAssetStatus
} from './api';
import { money, toPaged, today, usePostableAccountOptions } from './listing';

export const assetStatusTone: Record<FixedAssetStatus, 'neutral' | 'success' | 'warning' | 'error' | 'info'> = {
  Draft: 'neutral',
  Active: 'success',
  InMaintenance: 'warning',
  Transferred: 'info',
  Disposed: 'neutral',
  WrittenOff: 'error'
};

/** /fixed-assets/assets — screen #2: the register, with what each asset still carries on the books. */
export function FixedAssetsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<FixedAssetStatus | ''>('');
  const { data, isLoading } = useFixedAssets(status === '' ? undefined : { status });

  const rows = toPaged(data, search, (a, q) =>
    `${a.assetNumber} ${a.nameAr} ${a.nameEn} ${a.categoryNameAr} ${a.location ?? ''}`.toLowerCase().includes(q)
  );

  const columns: DataGridColumn<FixedAssetListItem>[] = [
    { key: 'assetNumber', label: t('fixedAssets.assetNumber'), render: (r) => r.assetNumber, exportValue: (r) => r.assetNumber },
    { key: 'nameAr', label: t('fixedAssets.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'category', label: t('fixedAssets.category'), render: (r) => r.categoryNameAr, exportValue: (r) => r.categoryNameAr },
    { key: 'branch', label: t('fixedAssets.branch'), render: (r) => r.branchNameAr ?? '—', exportValue: (r) => r.branchNameAr ?? '' },
    { key: 'acquisitionDate', label: t('fixedAssets.acquisitionDate'), render: (r) => r.acquisitionDate, exportValue: (r) => r.acquisitionDate },
    { key: 'cost', label: t('fixedAssets.cost'), render: (r) => money(r.baseCurrencyAmount), exportValue: (r) => String(r.baseCurrencyAmount) },
    {
      key: 'accumulated',
      label: t('fixedAssets.accumulatedDepreciation'),
      render: (r) => money(r.accumulatedDepreciation),
      exportValue: (r) => String(r.accumulatedDepreciation)
    },
    { key: 'netBookValue', label: t('fixedAssets.netBookValue'), render: (r) => money(r.netBookValue), exportValue: (r) => String(r.netBookValue) },
    {
      key: 'status',
      label: t('fixedAssets.status'),
      render: (r) => <Badge label={t(`fixedAssets.statuses.${r.status}`)} tone={assetStatusTone[r.status]} />,
      exportValue: (r) => t(`fixedAssets.statuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.title')}</h2>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <SearchableSelect
            value={status}
            onChange={(v) => setStatus(v === '' ? '' : (v as FixedAssetStatus))}
            options={[
              { value: '', label: t('fixedAssets.allStatuses') },
              ...(['Draft', 'Active', 'InMaintenance', 'Transferred', 'Disposed', 'WrittenOff'] as FixedAssetStatus[]).map((s) => ({
                value: s,
                label: t(`fixedAssets.statuses.${s}`)
              }))
            ]}
            style={{ minWidth: 180 }}
          />
          {canAdd && (
            <Button variant="primary" onClick={() => navigate('/fixed-assets/assets/new')}>
              {t('fixedAssets.addAsset')}
            </Button>
          )}
        </div>
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/fixed-assets/assets/${row.id}`)}
        exportFileName={t('fixedAssets.title')}
      />
    </div>
  );
}

/**
 * /fixed-assets/assets/:id — the asset's card. While it is a Draft everything can still change;
 * approving it posts the acquisition and lays out the depreciation, after which only the
 * descriptive fields (name, location, custody, notes) stay open.
 */
export function FixedAssetEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const assetId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: asset, isLoading } = useFixedAsset(assetId);
  const { data: categories } = useAssetCategories();
  const { data: branches } = useBranchesList();
  const { data: officers } = useCustodyOfficersList();
  const { data: suppliers } = useSuppliersList();
  const accountOptions = usePostableAccountOptions();
  const { data: codingRule } = useCodingRule('FIXED_ASSETS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [assetNumber, setAssetNumber] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [branchId, setBranchId] = useState<number | ''>('');
  const [categoryId, setCategoryId] = useState<number | ''>('');
  const [serialNumber, setSerialNumber] = useState('');
  const [barcode, setBarcode] = useState('');
  const [location, setLocation] = useState('');
  const [acquisitionDate, setAcquisitionDate] = useState(today());
  const [acquisitionCost, setAcquisitionCost] = useState('');
  const [currencyCode, setCurrencyCode] = useState('');
  const [exchangeRate, setExchangeRate] = useState('1');
  const [supplierId, setSupplierId] = useState<number | ''>('');
  const [fundingAccountId, setFundingAccountId] = useState<number | ''>('');
  const [usefulLifeYears, setUsefulLifeYears] = useState('');
  const [salvageValue, setSalvageValue] = useState('');
  const [method, setMethod] = useState<DepreciationMethod | ''>('');
  const [rate, setRate] = useState('');
  const [depreciationStartDate, setDepreciationStartDate] = useState('');
  const [firstMonthProrated, setFirstMonthProrated] = useState<boolean | null>(null);
  const [custodyOfficerId, setCustodyOfficerId] = useState<number | ''>('');
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (!asset) return;
    setNameAr(asset.nameAr);
    setNameEn(asset.nameEn);
    setBranchId(asset.branchId ?? '');
    setCategoryId(asset.categoryId);
    setSerialNumber(asset.serialNumber ?? '');
    setBarcode(asset.barcode ?? '');
    setLocation(asset.location ?? '');
    setAcquisitionDate(asset.acquisitionDate);
    setAcquisitionCost(String(asset.acquisitionCost));
    setCurrencyCode(asset.currencyCode);
    setExchangeRate(String(asset.exchangeRate));
    setSupplierId(asset.supplierId ?? '');
    setFundingAccountId(asset.fundingAccountId ?? '');
    setUsefulLifeYears(asset.usefulLifeYears?.toString() ?? '');
    setSalvageValue(String(asset.salvageValue));
    setMethod(asset.depreciationMethod);
    setRate(asset.depreciationRate?.toString() ?? '');
    setDepreciationStartDate(asset.depreciationStartDate);
    setFirstMonthProrated(asset.firstMonthProrated);
    setCustodyOfficerId(asset.custodyOfficerId ?? '');
    setNotes(asset.notes ?? '');
  }, [asset]);

  const save = useSaveFixedAsset(assetId);
  const activate = useActivateFixedAsset();
  const remove = useDeleteFixedAsset();

  const isDraft = isNew || asset?.status === 'Draft';
  const financialsLocked = !isDraft;

  const handleSave = async () => {
    try {
      const data = {
        nameAr,
        nameEn,
        branchId: branchId === '' ? null : Number(branchId),
        categoryId: Number(categoryId),
        serialNumber: serialNumber || null,
        barcode: barcode || null,
        location: location || null,
        acquisitionDate,
        acquisitionCost: Number(acquisitionCost || 0),
        currencyCode: currencyCode || null,
        exchangeRate: Number(exchangeRate || 1),
        supplierId: supplierId === '' ? null : Number(supplierId),
        purchaseInvoiceId: asset?.purchaseInvoiceId ?? null,
        fundingAccountId: fundingAccountId === '' ? null : Number(fundingAccountId),
        usefulLifeYears: usefulLifeYears === '' ? null : Number(usefulLifeYears),
        salvageValue: salvageValue === '' ? null : Number(salvageValue),
        depreciationMethod: method === '' ? null : method,
        depreciationRate: rate === '' ? null : Number(rate),
        depreciationStartDate: depreciationStartDate || null,
        firstMonthProrated,
        custodyOfficerId: custodyOfficerId === '' ? null : Number(custodyOfficerId),
        costCenterValueId: asset?.costCenterValueId ?? null,
        notes: notes || null
      };
      const result = await save.mutateAsync({
        assetNumber: codeIsAutomatic || !isNew ? undefined : assetNumber,
        rowVersion: asset?.rowVersion,
        data
      });
      showToast(t('fixedAssets.saveSuccess'), 'success');
      if (isNew) navigate(`/fixed-assets/assets/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleActivate = async () => {
    if (!assetId) return;
    try {
      await activate.mutateAsync(assetId);
      showToast(t('fixedAssets.activateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!assetId) return;
    try {
      await remove.mutateAsync(assetId);
      showToast(t('fixedAssets.deleteSuccess'), 'success');
      navigate('/fixed-assets/assets');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  const categoryOptions = (categories ?? []).filter((c) => c.isActive).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));
  const branchOptions = [{ value: '', label: t('fixedAssets.none') }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))];
  const officerOptions = [{ value: '', label: t('fixedAssets.none') }, ...(officers ?? []).map((o) => ({ value: o.id, label: o.nameAr }))];
  const supplierOptions = [{ value: '', label: t('fixedAssets.none') }, ...(suppliers ?? []).map((s) => ({ value: s.id, label: s.nameAr }))];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('fixedAssets.addAsset') : `${asset?.assetNumber} — ${asset?.nameAr}`}</h2>
        {asset && <Badge label={t(`fixedAssets.statuses.${asset.status}`)} tone={assetStatusTone[asset.status]} />}
      </div>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[
          ...(asset?.status === 'Draft'
            ? [{ key: 'approve', label: t('fixedAssets.activate'), onClick: handleActivate }]
            : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/assets') }
        ]}
        destructive={
          asset?.status === 'Draft'
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('fixedAssets.deleteAssetConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.assetNumber')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : assetNumber) : (asset?.assetNumber ?? '')}
                onChange={(e) => setAssetNumber(e.target.value)}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.category')}>
              <SearchableSelect
                value={categoryId}
                onChange={(v) => setCategoryId(v === '' ? '' : Number(v))}
                options={categoryOptions}
                disabled={financialsLocked}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.branch')}>
              <SearchableSelect
                value={branchId}
                onChange={(v) => setBranchId(v === '' ? '' : Number(v))}
                options={branchOptions}
                disabled={financialsLocked}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.serialNumber')}>
              <Input value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.barcode')}>
              <Input value={barcode} onChange={(e) => setBarcode(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.location')}>
              <Input value={location} onChange={(e) => setLocation(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.custodyOfficer')}>
              <SearchableSelect
                value={custodyOfficerId}
                onChange={(v) => setCustodyOfficerId(v === '' ? '' : Number(v))}
                options={officerOptions}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <h3 style={{ marginTop: 0 }}>{t('fixedAssets.acquisition')}</h3>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.acquisitionDate')}>
              <Input type="date" value={acquisitionDate} onChange={(e) => setAcquisitionDate(e.target.value)} disabled={financialsLocked} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.acquisitionCost')}>
              <Input type="number" value={acquisitionCost} onChange={(e) => setAcquisitionCost(e.target.value)} disabled={financialsLocked} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.currency')}>
              <Input value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value.toUpperCase())} disabled={financialsLocked} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.exchangeRate')}>
              <Input type="number" value={exchangeRate} onChange={(e) => setExchangeRate(e.target.value)} disabled={financialsLocked} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.baseAmount')}>
              <Input value={money(asset?.baseCurrencyAmount ?? Number(acquisitionCost || 0) * Number(exchangeRate || 1))} disabled />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.supplier')}>
              <SearchableSelect
                value={supplierId}
                onChange={(v) => setSupplierId(v === '' ? '' : Number(v))}
                options={supplierOptions}
                disabled={financialsLocked}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.fundingAccount')}>
              <SearchableSelect
                value={fundingAccountId}
                onChange={(v) => setFundingAccountId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('fixedAssets.none') }, ...accountOptions]}
                disabled={financialsLocked}
                style={{ minWidth: 280 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <h3 style={{ marginTop: 0 }}>{t('fixedAssets.depreciation')}</h3>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.depreciationMethod')}>
              <SearchableSelect
                value={method}
                onChange={(v) => setMethod(v === '' ? '' : (v as DepreciationMethod))}
                options={[
                  { value: '', label: t('fixedAssets.fromCategory') },
                  ...(['StraightLine', 'DecliningBalance', 'NoDepreciation'] as DepreciationMethod[]).map((m) => ({
                    value: m,
                    label: t(`fixedAssets.methods.${m}`)
                  }))
                ]}
                disabled={financialsLocked}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.usefulLifeYears')}>
              <Input type="number" value={usefulLifeYears} onChange={(e) => setUsefulLifeYears(e.target.value)} disabled={financialsLocked} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.depreciationRate')}>
              <Input
                type="number"
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                disabled={financialsLocked || method === 'StraightLine' || method === 'NoDepreciation'}
                style={{ width: 120 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.salvageValue')}>
              <Input type="number" value={salvageValue} onChange={(e) => setSalvageValue(e.target.value)} disabled={financialsLocked} style={{ width: 140 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.depreciationStartDate')}>
              <Input type="date" value={depreciationStartDate} onChange={(e) => setDepreciationStartDate(e.target.value)} disabled={financialsLocked} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.firstMonthProrated')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input
                  type="checkbox"
                  checked={firstMonthProrated ?? true}
                  onChange={(e) => setFirstMonthProrated(e.target.checked)}
                  disabled={financialsLocked}
                />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.accumulatedDepreciation')}>
              <Input value={money(asset?.accumulatedDepreciation)} disabled />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.netBookValue')}>
              <Input value={money(asset?.netBookValue)} disabled />
            </FieldWrapper>
          </div>
          <FieldWrapper label={t('fixedAssets.notes')}>
            <Input value={notes} onChange={(e) => setNotes(e.target.value)} style={{ minWidth: 420 }} />
          </FieldWrapper>
        </CardBody>
      </Card>

      {asset && asset.schedule.length > 0 && (
        <Card>
          <CardBody>
            <h3 style={{ marginTop: 0 }}>{t('fixedAssets.scheduleTitle')}</h3>
            <div style={{ maxHeight: 320, overflow: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 6 }}>#</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.periodEnd')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.amount')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.bookValueAfter')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.status')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.run')}</th>
                  </tr>
                </thead>
                <tbody>
                  {asset.schedule.map((period) => (
                    <tr key={period.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                      <td style={{ padding: 6 }}>{period.periodNumber}</td>
                      <td style={{ padding: 6 }}>{period.periodEnd}</td>
                      <td style={{ padding: 6 }}>{money(period.amount)}</td>
                      <td style={{ padding: 6 }}>{money(period.bookValueAfter)}</td>
                      <td style={{ padding: 6 }}>{t(`fixedAssets.periodStatuses.${period.status}`)}</td>
                      <td style={{ padding: 6 }}>{period.runNumber ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
