import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../ui-kit/DataGrid';
import { Badge } from '../../ui-kit/Badge';
import { Button } from '../../ui-kit/Button';
import { ActionBar } from '../../ui-kit/ActionBar';
import { Card, CardBody } from '../../ui-kit/Card';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { SearchableSelect } from '../../ui-kit/SearchableSelect';
import { usePermission } from '../../ui-kit/usePermission';
import { useToastStore } from '../../store/toastStore';
import { getFieldErrorMessage } from '../../app/api';
import { useBranchesList } from '../organization/branches/api';
import { useCustodyOfficersList } from '../inventory/custodyOfficers/api';
import {
  useAssetCountSchedule,
  useAssetDisposalAction,
  useAssetDisposals,
  useAssetPhysicalCount,
  useAssetPhysicalCountAction,
  useAssetPhysicalCounts,
  useAssetSettings,
  useAssetTransferAction,
  useAssetTransfers,
  useCreateAssetDisposal,
  useCreateAssetPhysicalCount,
  useCreateAssetTransfer,
  useFixedAssets,
  useUpdateAssetSettings,
  type AssetCondition,
  type AssetDisposal,
  type AssetPhysicalCount,
  type AssetPhysicalCountFrequency,
  type AssetTransfer,
  type DisposalType
} from './api';
import { money, toPaged, today, usePostableAccountOptions } from './listing';

const openTone = { Draft: 'neutral', Posted: 'success', Rejected: 'error', Cancelled: 'neutral' } as const;

// ================================================================== transfers (screen #5)

/** /fixed-assets/transfers — moving an asset to another branch; the schedule is untouched. */
export function AssetTransfersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useAssetTransfers();

  const rows = toPaged(data, search, (r, q) => `${r.transferNumber} ${r.assetNumber} ${r.assetNameAr} ${r.toBranchNameAr}`.toLowerCase().includes(q));

  const columns: DataGridColumn<AssetTransfer>[] = [
    { key: 'number', label: t('fixedAssets.transferNumber'), render: (r) => r.transferNumber, exportValue: (r) => r.transferNumber },
    { key: 'asset', label: t('fixedAssets.asset'), render: (r) => `${r.assetNumber} — ${r.assetNameAr}`, exportValue: (r) => r.assetNumber },
    { key: 'from', label: t('fixedAssets.fromBranch'), render: (r) => r.fromBranchNameAr ?? '—', exportValue: (r) => r.fromBranchNameAr ?? '' },
    { key: 'to', label: t('fixedAssets.toBranch'), render: (r) => r.toBranchNameAr, exportValue: (r) => r.toBranchNameAr },
    { key: 'date', label: t('fixedAssets.transferDate'), render: (r) => r.transferDate, exportValue: (r) => r.transferDate },
    { key: 'officer', label: t('fixedAssets.custodyOfficer'), render: (r) => r.custodyOfficerName, exportValue: (r) => r.custodyOfficerName },
    {
      key: 'status',
      label: t('fixedAssets.status'),
      render: (r) => <Badge label={t(`fixedAssets.transferStatuses.${r.status}`)} tone={openTone[r.status]} />,
      exportValue: (r) => t(`fixedAssets.transferStatuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.transfersTitle')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/fixed-assets/transfers/new')}>
            {t('fixedAssets.addTransfer')}
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
        onRowClick={(row) => navigate(`/fixed-assets/transfers/${row.id}`)}
        exportFileName={t('fixedAssets.transfersTitle')}
      />
    </div>
  );
}

export function AssetTransferEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: transfers } = useAssetTransfers();
  const transfer = isNew ? undefined : transfers?.find((x) => x.id === Number(id));
  const { data: assets } = useFixedAssets();
  const { data: branches } = useBranchesList();
  const { data: officers } = useCustodyOfficersList();

  const [fixedAssetId, setFixedAssetId] = useState<number | ''>('');
  const [toBranchId, setToBranchId] = useState<number | ''>('');
  const [transferDate, setTransferDate] = useState(today());
  const [reason, setReason] = useState('');
  const [custodyOfficerId, setCustodyOfficerId] = useState<number | ''>('');
  const [notes, setNotes] = useState('');

  const create = useCreateAssetTransfer();
  const act = useAssetTransferAction();

  const handleCreate = async () => {
    try {
      const result = await create.mutateAsync({
        fixedAssetId: Number(fixedAssetId),
        toBranchId: Number(toBranchId),
        transferDate,
        reason: reason || null,
        custodyOfficerId: Number(custodyOfficerId),
        notes: notes || null
      });
      showToast(t('fixedAssets.saveSuccess'), 'success');
      navigate(`/fixed-assets/transfers/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const perform = async (action: 'post' | 'reject' | 'cancel') => {
    if (!transfer) return;
    try {
      await act.mutateAsync({ id: transfer.id, action });
      showToast(t('fixedAssets.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const activeAssets = (assets ?? []).filter((a) => a.status === 'Active');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 760 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('fixedAssets.addTransfer') : `${t('fixedAssets.transfersTitle')} — ${transfer?.transferNumber ?? ''}`}</h2>
        {transfer && <Badge label={t(`fixedAssets.transferStatuses.${transfer.status}`)} tone={openTone[transfer.status]} />}
      </div>

      <ActionBar
        primary={isNew ? { key: 'save', label: t('common.saveDraft'), onClick: handleCreate } : undefined}
        secondary={[
          ...(transfer?.status === 'Draft' ? [{ key: 'post', label: t('fixedAssets.postTransfer'), onClick: () => perform('post') }] : []),
          ...(transfer?.status === 'Draft' ? [{ key: 'reject', label: t('common.reject'), onClick: () => perform('reject') }] : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/transfers') }
        ]}
        destructive={
          transfer?.status === 'Draft'
            ? [{ key: 'cancel', label: t('common.cancel'), onClick: () => perform('cancel'), confirmMessage: t('fixedAssets.cancelConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.asset')}>
              {isNew ? (
                <SearchableSelect
                  value={fixedAssetId}
                  onChange={(v) => setFixedAssetId(v === '' ? '' : Number(v))}
                  options={activeAssets.map((a) => ({ value: a.id, label: `${a.assetNumber} — ${a.nameAr}` }))}
                  style={{ minWidth: 260 }}
                />
              ) : (
                <Input value={`${transfer?.assetNumber ?? ''} — ${transfer?.assetNameAr ?? ''}`} disabled style={{ minWidth: 260 }} />
              )}
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.toBranch')}>
              {isNew ? (
                <SearchableSelect
                  value={toBranchId}
                  onChange={(v) => setToBranchId(v === '' ? '' : Number(v))}
                  options={(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))}
                  style={{ minWidth: 200 }}
                />
              ) : (
                <Input value={transfer?.toBranchNameAr ?? ''} disabled />
              )}
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.transferDate')}>
              <Input type="date" value={isNew ? transferDate : (transfer?.transferDate ?? '')} onChange={(e) => setTransferDate(e.target.value)} disabled={!isNew} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.custodyOfficer')}>
              {isNew ? (
                <SearchableSelect
                  value={custodyOfficerId}
                  onChange={(v) => setCustodyOfficerId(v === '' ? '' : Number(v))}
                  options={(officers ?? []).map((o) => ({ value: o.id, label: o.nameAr }))}
                  style={{ minWidth: 200 }}
                />
              ) : (
                <Input value={transfer?.custodyOfficerName ?? ''} disabled />
              )}
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.reason')}>
              <Input value={isNew ? reason : (transfer?.reason ?? '')} onChange={(e) => setReason(e.target.value)} disabled={!isNew} style={{ minWidth: 260 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.notes')}>
              <Input value={isNew ? notes : (transfer?.notes ?? '')} onChange={(e) => setNotes(e.target.value)} disabled={!isNew} style={{ minWidth: 260 }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

// ================================================================== disposals (screen #6)

/** /fixed-assets/disposals — selling, scrapping or writing off an asset. */
export function AssetDisposalsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useAssetDisposals();

  const rows = toPaged(data, search, (r, q) => `${r.disposalNumber} ${r.assetNumber} ${r.assetNameAr}`.toLowerCase().includes(q));

  const columns: DataGridColumn<AssetDisposal>[] = [
    { key: 'number', label: t('fixedAssets.disposalNumber'), render: (r) => r.disposalNumber, exportValue: (r) => r.disposalNumber },
    { key: 'asset', label: t('fixedAssets.asset'), render: (r) => `${r.assetNumber} — ${r.assetNameAr}`, exportValue: (r) => r.assetNumber },
    { key: 'date', label: t('fixedAssets.disposalDate'), render: (r) => r.disposalDate, exportValue: (r) => r.disposalDate },
    {
      key: 'type',
      label: t('fixedAssets.disposalType'),
      render: (r) => t(`fixedAssets.disposalTypes.${r.disposalType}`),
      exportValue: (r) => t(`fixedAssets.disposalTypes.${r.disposalType}`)
    },
    { key: 'bookValue', label: t('fixedAssets.bookValue'), render: (r) => money(r.bookValueAtDisposal), exportValue: (r) => String(r.bookValueAtDisposal) },
    { key: 'proceeds', label: t('fixedAssets.proceeds'), render: (r) => money(r.proceeds ?? 0), exportValue: (r) => String(r.proceeds ?? 0) },
    { key: 'gainOrLoss', label: t('fixedAssets.gainOrLoss'), render: (r) => money(r.gainOrLoss), exportValue: (r) => String(r.gainOrLoss) },
    {
      key: 'status',
      label: t('fixedAssets.status'),
      render: (r) => <Badge label={t(`fixedAssets.transferStatuses.${r.status}`)} tone={openTone[r.status]} />,
      exportValue: (r) => t(`fixedAssets.transferStatuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.disposalsTitle')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/fixed-assets/disposals/new')}>
            {t('fixedAssets.addDisposal')}
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
        onRowClick={(row) => navigate(`/fixed-assets/disposals/${row.id}`)}
        exportFileName={t('fixedAssets.disposalsTitle')}
      />
    </div>
  );
}

export function AssetDisposalEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: disposals } = useAssetDisposals();
  const disposal = isNew ? undefined : disposals?.find((d) => d.id === Number(id));
  const { data: assets } = useFixedAssets();
  const accountOptions = usePostableAccountOptions();

  const [fixedAssetId, setFixedAssetId] = useState<number | ''>('');
  const [disposalDate, setDisposalDate] = useState(today());
  const [disposalType, setDisposalType] = useState<DisposalType>('Sale');
  const [proceeds, setProceeds] = useState('');
  const [proceedsAccountId, setProceedsAccountId] = useState<number | ''>('');
  const [buyerName, setBuyerName] = useState('');
  const [notes, setNotes] = useState('');

  const create = useCreateAssetDisposal();
  const act = useAssetDisposalAction();

  const handleCreate = async () => {
    try {
      const result = await create.mutateAsync({
        fixedAssetId: Number(fixedAssetId),
        disposalDate,
        disposalType,
        proceeds: proceeds === '' ? null : Number(proceeds),
        proceedsAccountId: proceedsAccountId === '' ? null : Number(proceedsAccountId),
        buyerName: buyerName || null,
        notes: notes || null
      });
      showToast(t('fixedAssets.saveSuccess'), 'success');
      navigate(`/fixed-assets/disposals/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const perform = async (action: 'post' | 'reject' | 'cancel') => {
    if (!disposal) return;
    try {
      await act.mutateAsync({ id: disposal.id, action });
      showToast(t('fixedAssets.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const disposableAssets = (assets ?? []).filter((a) => a.status === 'Active' || a.status === 'InMaintenance');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 760 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('fixedAssets.addDisposal') : `${t('fixedAssets.disposalsTitle')} — ${disposal?.disposalNumber ?? ''}`}</h2>
        {disposal && <Badge label={t(`fixedAssets.transferStatuses.${disposal.status}`)} tone={openTone[disposal.status]} />}
      </div>

      <ActionBar
        primary={isNew ? { key: 'save', label: t('common.saveDraft'), onClick: handleCreate } : undefined}
        secondary={[
          ...(disposal?.status === 'Draft' ? [{ key: 'post', label: t('fixedAssets.postDisposal'), onClick: () => perform('post') }] : []),
          ...(disposal?.status === 'Draft' ? [{ key: 'reject', label: t('common.reject'), onClick: () => perform('reject') }] : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/disposals') }
        ]}
        destructive={
          disposal?.status === 'Draft'
            ? [{ key: 'cancel', label: t('common.cancel'), onClick: () => perform('cancel'), confirmMessage: t('fixedAssets.cancelConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.asset')}>
              {isNew ? (
                <SearchableSelect
                  value={fixedAssetId}
                  onChange={(v) => setFixedAssetId(v === '' ? '' : Number(v))}
                  options={disposableAssets.map((a) => ({ value: a.id, label: `${a.assetNumber} — ${a.nameAr}` }))}
                  style={{ minWidth: 260 }}
                />
              ) : (
                <Input value={`${disposal?.assetNumber ?? ''} — ${disposal?.assetNameAr ?? ''}`} disabled style={{ minWidth: 260 }} />
              )}
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.disposalDate')}>
              <Input type="date" value={isNew ? disposalDate : (disposal?.disposalDate ?? '')} onChange={(e) => setDisposalDate(e.target.value)} disabled={!isNew} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.disposalType')}>
              <SearchableSelect
                value={isNew ? disposalType : (disposal?.disposalType ?? '')}
                onChange={(v) => setDisposalType(v as DisposalType)}
                options={(['Sale', 'Scrap', 'Loss'] as DisposalType[]).map((d) => ({ value: d, label: t(`fixedAssets.disposalTypes.${d}`) }))}
                disabled={!isNew}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.proceeds')}>
              <Input
                type="number"
                value={isNew ? proceeds : String(disposal?.proceeds ?? '')}
                onChange={(e) => setProceeds(e.target.value)}
                disabled={!isNew || disposalType === 'Loss'}
                style={{ width: 160 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.proceedsAccount')}>
              <SearchableSelect
                value={isNew ? proceedsAccountId : (disposal?.proceedsAccountId ?? '')}
                onChange={(v) => setProceedsAccountId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('fixedAssets.none') }, ...accountOptions]}
                disabled={!isNew || disposalType === 'Loss'}
                style={{ minWidth: 280 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.buyerName')}>
              <Input value={isNew ? buyerName : (disposal?.buyerName ?? '')} onChange={(e) => setBuyerName(e.target.value)} disabled={!isNew} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.notes')}>
              <Input value={isNew ? notes : (disposal?.notes ?? '')} onChange={(e) => setNotes(e.target.value)} disabled={!isNew} style={{ minWidth: 260 }} />
            </FieldWrapper>
          </div>

          {disposal && (
            <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginTop: 12 }}>
              <div>
                <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.cost')}</div>
                <div>{money(disposal.costAtDisposal)}</div>
              </div>
              <div>
                <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.accumulatedDepreciation')}</div>
                <div>{money(disposal.accumulatedAtDisposal)}</div>
              </div>
              <div>
                <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.bookValue')}</div>
                <div>{money(disposal.bookValueAtDisposal)}</div>
              </div>
              <div>
                <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.gainOrLoss')}</div>
                <div>{money(disposal.gainOrLoss)}</div>
              </div>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}

// ================================================================== physical counts (screen #7)

const countTone = { Draft: 'neutral', InProgress: 'info', Completed: 'success', Rejected: 'error' } as const;

/** /fixed-assets/physical-counts — a branch's assets checked one by one, and when the next check is due. */
export function AssetPhysicalCountsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useAssetPhysicalCounts();
  const { data: schedule } = useAssetCountSchedule();

  const rows = toPaged(data, search, (r, q) => `${r.countNumber} ${r.branchNameAr ?? ''}`.toLowerCase().includes(q));

  const columns: DataGridColumn<AssetPhysicalCount>[] = [
    { key: 'number', label: t('fixedAssets.countNumber'), render: (r) => r.countNumber, exportValue: (r) => r.countNumber },
    { key: 'branch', label: t('fixedAssets.branch'), render: (r) => r.branchNameAr ?? '—', exportValue: (r) => r.branchNameAr ?? '' },
    { key: 'date', label: t('fixedAssets.countDate'), render: (r) => r.countDate, exportValue: (r) => r.countDate },
    { key: 'lines', label: t('fixedAssets.countLines'), render: (r) => r.lineCount, exportValue: (r) => String(r.lineCount) },
    { key: 'found', label: t('fixedAssets.found'), render: (r) => r.foundCount, exportValue: (r) => String(r.foundCount) },
    { key: 'missing', label: t('fixedAssets.missing'), render: (r) => r.missingCount, exportValue: (r) => String(r.missingCount) },
    { key: 'damaged', label: t('fixedAssets.damaged'), render: (r) => r.damagedCount, exportValue: (r) => String(r.damagedCount) },
    {
      key: 'status',
      label: t('fixedAssets.status'),
      render: (r) => <Badge label={t(`fixedAssets.countStatuses.${r.status}`)} tone={countTone[r.status]} />,
      exportValue: (r) => t(`fixedAssets.countStatuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.countsTitle')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/fixed-assets/physical-counts/new')}>
            {t('fixedAssets.addCount')}
          </Button>
        )}
      </div>

      {schedule?.nextDueDate && (
        <Card>
          <CardBody>
            <span style={{ color: schedule.isOverdue ? 'var(--color-error)' : 'var(--color-text-muted)' }}>
              {t('fixedAssets.nextCountDue', { date: schedule.nextDueDate })}
            </span>
          </CardBody>
        </Card>
      )}

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/fixed-assets/physical-counts/${row.id}`)}
        exportFileName={t('fixedAssets.countsTitle')}
      />
    </div>
  );
}

interface CountLineState {
  isFound: boolean | null;
  actualLocation: string;
  condition: AssetCondition | '';
  notes: string;
}

export function AssetPhysicalCountEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const countId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data, isLoading } = useAssetPhysicalCount(countId);
  const { data: branches } = useBranchesList();
  const create = useCreateAssetPhysicalCount();
  const act = useAssetPhysicalCountAction();

  const [branchId, setBranchId] = useState<number | ''>('');
  const [countDate, setCountDate] = useState(today());
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<Record<number, CountLineState>>({});

  useEffect(() => {
    if (!data) return;
    setLines(
      Object.fromEntries(
        data.lines.map((line) => [
          line.id,
          {
            isFound: line.isFound,
            actualLocation: line.actualLocation ?? line.expectedLocation ?? '',
            condition: line.condition ?? '',
            notes: line.notes ?? ''
          }
        ])
      )
    );
  }, [data]);

  const handleCreate = async () => {
    try {
      const result = await create.mutateAsync({ branchId: Number(branchId), countDate, notes: notes || null });
      showToast(t('fixedAssets.saveSuccess'), 'success');
      navigate(`/fixed-assets/physical-counts/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const perform = async (action: 'start' | 'record' | 'complete' | 'reject') => {
    if (!countId) return;
    try {
      await act.mutateAsync({
        id: countId,
        action,
        lines:
          action === 'record'
            ? Object.entries(lines).map(([lineId, state]) => ({
                lineId: Number(lineId),
                isFound: state.isFound,
                actualLocation: state.actualLocation || null,
                condition: state.condition === '' ? null : state.condition,
                notes: state.notes || null
              }))
            : undefined
      });
      showToast(t('fixedAssets.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  if (isNew) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.addCount')}</h2>
        <ActionBar
          primary={{ key: 'save', label: t('common.saveDraft'), onClick: handleCreate }}
          secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/physical-counts') }]}
        />
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={t('fixedAssets.branch')}>
                <SearchableSelect
                  value={branchId}
                  onChange={(v) => setBranchId(v === '' ? '' : Number(v))}
                  options={(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))}
                  style={{ minWidth: 220 }}
                />
              </FieldWrapper>
              <FieldWrapper label={t('fixedAssets.countDate')}>
                <Input type="date" value={countDate} onChange={(e) => setCountDate(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('fixedAssets.notes')}>
                <Input value={notes} onChange={(e) => setNotes(e.target.value)} style={{ minWidth: 260 }} />
              </FieldWrapper>
            </div>
          </CardBody>
        </Card>
      </div>
    );
  }

  const count = data!.count;
  const open = count.status === 'Draft' || count.status === 'InProgress';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{`${t('fixedAssets.countsTitle')} — ${count.countNumber}`}</h2>
        <Badge label={t(`fixedAssets.countStatuses.${count.status}`)} tone={countTone[count.status]} />
      </div>

      <ActionBar
        primary={open ? { key: 'save', label: t('fixedAssets.recordCount'), onClick: () => perform('record') } : undefined}
        secondary={[
          ...(count.status === 'Draft' ? [{ key: 'start', label: t('fixedAssets.startCount'), onClick: () => perform('start') }] : []),
          ...(open ? [{ key: 'complete', label: t('fixedAssets.completeCount'), onClick: () => perform('complete') }] : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/physical-counts') }
        ]}
        destructive={
          open ? [{ key: 'reject', label: t('common.reject'), onClick: () => perform('reject'), confirmMessage: t('fixedAssets.rejectCountConfirm') }] : []
        }
      />

      <Card>
        <CardBody>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.asset')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.expectedLocation')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.found')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.actualLocation')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.condition')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.notes')}</th>
              </tr>
            </thead>
            <tbody>
              {data!.lines.map((line) => {
                const state = lines[line.id] ?? { isFound: null, actualLocation: '', condition: '' as const, notes: '' };
                const update = (patch: Partial<CountLineState>) => setLines((prev) => ({ ...prev, [line.id]: { ...state, ...patch } }));
                return (
                  <tr key={line.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                    <td style={{ padding: 6 }}>{`${line.assetNumber} — ${line.assetNameAr}`}</td>
                    <td style={{ padding: 6 }}>{line.expectedLocation ?? '—'}</td>
                    <td style={{ padding: 6 }}>
                      <SearchableSelect
                        value={state.isFound === null ? '' : state.isFound ? 'yes' : 'no'}
                        onChange={(v) => update({ isFound: v === '' ? null : v === 'yes' })}
                        options={[
                          { value: '', label: '—' },
                          { value: 'yes', label: t('common.yes') },
                          { value: 'no', label: t('common.no') }
                        ]}
                        disabled={!open}
                        style={{ minWidth: 110 }}
                      />
                    </td>
                    <td style={{ padding: 6 }}>
                      <Input value={state.actualLocation} onChange={(e) => update({ actualLocation: e.target.value })} disabled={!open || state.isFound !== true} />
                    </td>
                    <td style={{ padding: 6 }}>
                      <SearchableSelect
                        value={state.condition}
                        onChange={(v) => update({ condition: v === '' ? '' : (v as AssetCondition) })}
                        options={[
                          { value: '', label: '—' },
                          ...(['Good', 'Fair', 'Damaged'] as AssetCondition[]).map((c) => ({ value: c, label: t(`fixedAssets.conditions.${c}`) }))
                        ]}
                        disabled={!open || state.isFound !== true}
                        style={{ minWidth: 130 }}
                      />
                    </td>
                    <td style={{ padding: 6 }}>
                      <Input value={state.notes} onChange={(e) => update({ notes: e.target.value })} disabled={!open} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}

// ================================================================== settings (screen #13)

/** /fixed-assets/settings — when depreciation runs by itself, what needs approving, how often assets are counted. */
export function AssetSettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data, isLoading } = useAssetSettings();
  const update = useUpdateAssetSettings();

  const [autoDepreciationEnabled, setAutoDepreciationEnabled] = useState(false);
  const [depreciationRunDay, setDepreciationRunDay] = useState('28');
  const [requireApprovalForDisposal, setRequireApprovalForDisposal] = useState(true);
  const [requireApprovalForTransfer, setRequireApprovalForTransfer] = useState(false);
  const [threshold, setThreshold] = useState('');
  const [countFrequency, setCountFrequency] = useState<AssetPhysicalCountFrequency | ''>('');
  const [defaultFirstMonthProrated, setDefaultFirstMonthProrated] = useState(true);

  useEffect(() => {
    if (!data) return;
    setAutoDepreciationEnabled(data.autoDepreciationEnabled);
    setDepreciationRunDay(String(data.depreciationRunDay));
    setRequireApprovalForDisposal(data.requireApprovalForDisposal);
    setRequireApprovalForTransfer(data.requireApprovalForTransfer);
    setThreshold(data.maintenanceApprovalThreshold?.toString() ?? '');
    setCountFrequency(data.physicalCountFrequency ?? '');
    setDefaultFirstMonthProrated(data.defaultFirstMonthProrated);
  }, [data]);

  const handleSave = async () => {
    try {
      await update.mutateAsync({
        autoDepreciationEnabled,
        depreciationRunDay: Number(depreciationRunDay || 28),
        requireApprovalForDisposal,
        requireApprovalForTransfer,
        maintenanceApprovalThreshold: threshold === '' ? null : Number(threshold),
        physicalCountFrequency: countFrequency === '' ? null : countFrequency,
        defaultFirstMonthProrated
      });
      showToast(t('fixedAssets.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 760 }}>
      <h2 style={{ margin: 0 }}>{t('fixedAssets.settingsTitle')}</h2>

      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }} />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('fixedAssets.autoDepreciation')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={autoDepreciationEnabled} onChange={(e) => setAutoDepreciationEnabled(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.depreciationRunDay')}>
              <Input type="number" value={depreciationRunDay} onChange={(e) => setDepreciationRunDay(e.target.value)} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.defaultFirstMonthProrated')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={defaultFirstMonthProrated} onChange={(e) => setDefaultFirstMonthProrated(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.requireApprovalForDisposal')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={requireApprovalForDisposal} onChange={(e) => setRequireApprovalForDisposal(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.requireApprovalForTransfer')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={requireApprovalForTransfer} onChange={(e) => setRequireApprovalForTransfer(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.maintenanceApprovalThreshold')}>
              <Input type="number" value={threshold} onChange={(e) => setThreshold(e.target.value)} style={{ width: 160 }} />
            </FieldWrapper>
            <FieldWrapper label={t('fixedAssets.physicalCountFrequency')}>
              <SearchableSelect
                value={countFrequency}
                onChange={(v) => setCountFrequency(v === '' ? '' : (v as AssetPhysicalCountFrequency))}
                options={[
                  { value: '', label: t('fixedAssets.none') },
                  ...(['Annual', 'SemiAnnual', 'Quarterly'] as AssetPhysicalCountFrequency[]).map((f) => ({
                    value: f,
                    label: t(`fixedAssets.countFrequencies.${f}`)
                  }))
                ]}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
