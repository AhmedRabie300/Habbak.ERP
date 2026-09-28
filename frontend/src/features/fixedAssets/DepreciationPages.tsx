import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../ui-kit/DataGrid';
import { Badge } from '../../ui-kit/Badge';
import { Button } from '../../ui-kit/Button';
import { ActionBar } from '../../ui-kit/ActionBar';
import { Card, CardBody } from '../../ui-kit/Card';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { SearchableSelect } from '../../ui-kit/SearchableSelect';
import { useToastStore } from '../../store/toastStore';
import { getFieldErrorMessage } from '../../app/api';
import {
  useCreateDepreciationRun,
  useDepreciationRun,
  useDepreciationRunAction,
  useDepreciationRuns,
  useDepreciationSchedule,
  useFixedAssets,
  type DepreciationRun,
  type DepreciationRunStatus,
  type DepreciationScheduleRow,
  type DepreciationScheduleStatus
} from './api';
import { money, toPaged } from './listing';

const runTone: Record<DepreciationRunStatus, 'neutral' | 'success' | 'error'> = {
  Draft: 'neutral',
  Posted: 'success',
  Reversed: 'error'
};

/** /fixed-assets/depreciation-schedule — screen #3: every asset's months, and where each one stands. */
export function DepreciationSchedulePage() {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const [assetId, setAssetId] = useState<number | ''>('');
  const [status, setStatus] = useState<DepreciationScheduleStatus | ''>('');
  const [month, setMonth] = useState('');

  const [year, monthNumber] = month ? month.split('-').map(Number) : [undefined, undefined];
  const { data, isLoading } = useDepreciationSchedule({
    fixedAssetId: assetId === '' ? undefined : Number(assetId),
    year,
    month: monthNumber,
    status: status === '' ? undefined : status
  });
  const { data: assets } = useFixedAssets();

  const rows = toPaged(data, search, (r, q) => `${r.assetNumber} ${r.assetNameAr}`.toLowerCase().includes(q));

  const columns: DataGridColumn<DepreciationScheduleRow & { id: number }>[] = [
    { key: 'asset', label: t('fixedAssets.asset'), render: (r) => `${r.assetNumber} — ${r.assetNameAr}`, exportValue: (r) => r.assetNumber },
    { key: 'period', label: t('fixedAssets.periodNumber'), render: (r) => r.period.periodNumber, exportValue: (r) => String(r.period.periodNumber) },
    { key: 'periodEnd', label: t('fixedAssets.periodEnd'), render: (r) => r.period.periodEnd, exportValue: (r) => r.period.periodEnd },
    { key: 'amount', label: t('fixedAssets.amount'), render: (r) => money(r.period.amount), exportValue: (r) => String(r.period.amount) },
    {
      key: 'bookValue',
      label: t('fixedAssets.bookValueAfter'),
      render: (r) => money(r.period.bookValueAfter),
      exportValue: (r) => String(r.period.bookValueAfter)
    },
    {
      key: 'status',
      label: t('fixedAssets.status'),
      render: (r) => t(`fixedAssets.periodStatuses.${r.period.status}`),
      exportValue: (r) => t(`fixedAssets.periodStatuses.${r.period.status}`)
    },
    { key: 'run', label: t('fixedAssets.run'), render: (r) => r.period.runNumber ?? '—', exportValue: (r) => r.period.runNumber ?? '' }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('fixedAssets.scheduleTitle')}</h2>

      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
        <FieldWrapper label={t('fixedAssets.asset')}>
          <SearchableSelect
            value={assetId}
            onChange={(v) => setAssetId(v === '' ? '' : Number(v))}
            options={[
              { value: '', label: t('fixedAssets.allAssets') },
              ...(assets ?? []).map((a) => ({ value: a.id, label: `${a.assetNumber} — ${a.nameAr}` }))
            ]}
            style={{ minWidth: 240 }}
          />
        </FieldWrapper>
        <FieldWrapper label={t('fixedAssets.month')}>
          <Input type="month" value={month} onChange={(e) => setMonth(e.target.value)} />
        </FieldWrapper>
        <FieldWrapper label={t('fixedAssets.status')}>
          <SearchableSelect
            value={status}
            onChange={(v) => setStatus(v === '' ? '' : (v as DepreciationScheduleStatus))}
            options={[
              { value: '', label: t('fixedAssets.allStatuses') },
              ...(['Scheduled', 'Posted', 'Cancelled'] as DepreciationScheduleStatus[]).map((s) => ({
                value: s,
                label: t(`fixedAssets.periodStatuses.${s}`)
              }))
            ]}
            style={{ minWidth: 180 }}
          />
        </FieldWrapper>
      </div>

      <DataGrid
        columns={columns}
        data={{ ...rows, items: rows.items.map((r) => ({ ...r, id: r.period.id })) }}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        exportFileName={t('fixedAssets.scheduleTitle')}
      />
    </div>
  );
}

/** /fixed-assets/depreciation-runs — screen #4: one run per month, asked for twice or not. */
export function DepreciationRunsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [search, setSearch] = useState('');
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));

  const { data, isLoading } = useDepreciationRuns();
  const createRun = useCreateDepreciationRun();

  const rows = toPaged(data, search, (r, q) => `${r.runNumber} ${r.year}-${r.month}`.toLowerCase().includes(q));

  const handleCreate = async () => {
    const [year, monthNumber] = month.split('-').map(Number);
    try {
      const result = await createRun.mutateAsync({ year, month: monthNumber });
      showToast(result.alreadyExisted ? t('fixedAssets.runAlreadyExists') : t('fixedAssets.runCreated'), result.alreadyExisted ? 'info' : 'success');
      navigate(`/fixed-assets/depreciation-runs/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<DepreciationRun>[] = [
    { key: 'runNumber', label: t('fixedAssets.runNumber'), render: (r) => r.runNumber, exportValue: (r) => r.runNumber },
    { key: 'month', label: t('fixedAssets.month'), render: (r) => `${r.month}/${r.year}`, exportValue: (r) => `${r.month}/${r.year}` },
    { key: 'runDate', label: t('fixedAssets.runDate'), render: (r) => r.runDate, exportValue: (r) => r.runDate },
    { key: 'assetCount', label: t('fixedAssets.assetCount'), render: (r) => r.assetCount, exportValue: (r) => String(r.assetCount) },
    { key: 'total', label: t('fixedAssets.totalDepreciation'), render: (r) => money(r.totalDepreciation), exportValue: (r) => String(r.totalDepreciation) },
    {
      key: 'status',
      label: t('fixedAssets.status'),
      render: (r) => <Badge label={t(`fixedAssets.runStatuses.${r.status}`)} tone={runTone[r.status]} />,
      exportValue: (r) => t(`fixedAssets.runStatuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'end', gap: 12, flexWrap: 'wrap' }}>
        <h2 style={{ margin: 0 }}>{t('fixedAssets.runsTitle')}</h2>
        <div style={{ display: 'flex', gap: 12, alignItems: 'end' }}>
          <FieldWrapper label={t('fixedAssets.month')}>
            <Input type="month" value={month} onChange={(e) => setMonth(e.target.value)} />
          </FieldWrapper>
          <Button variant="primary" onClick={handleCreate}>
            {t('fixedAssets.prepareRun')}
          </Button>
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
        onRowClick={(row) => navigate(`/fixed-assets/depreciation-runs/${row.id}`)}
        exportFileName={t('fixedAssets.runsTitle')}
      />
    </div>
  );
}

/** /fixed-assets/depreciation-runs/:id — what the month will post, asset by asset. */
export function DepreciationRunDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const runId = Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [reason, setReason] = useState('');

  const { data, isLoading } = useDepreciationRun(runId);
  const action = useDepreciationRunAction();

  const run = data?.run;

  const perform = async (kind: 'post' | 'reverse' | 'delete') => {
    try {
      await action.mutateAsync({ id: runId, action: kind, reason });
      showToast(t(`fixedAssets.run${kind === 'post' ? 'Posted' : kind === 'reverse' ? 'Reversed' : 'Deleted'}`), 'success');
      if (kind === 'delete') navigate('/fixed-assets/depreciation-runs');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading || !run) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{`${t('fixedAssets.runsTitle')} — ${run.runNumber}`}</h2>
        <Badge label={t(`fixedAssets.runStatuses.${run.status}`)} tone={runTone[run.status]} />
      </div>

      <ActionBar
        primary={run.status === 'Draft' ? { key: 'post', label: t('fixedAssets.postRun'), onClick: () => perform('post') } : undefined}
        secondary={[
          ...(run.status === 'Posted' ? [{ key: 'reverse', label: t('fixedAssets.reverseRun'), onClick: () => perform('reverse') }] : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/fixed-assets/depreciation-runs') }
        ]}
        destructive={
          run.status === 'Draft'
            ? [{ key: 'delete', label: t('common.remove'), onClick: () => perform('delete'), confirmMessage: t('fixedAssets.deleteRunConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap' }}>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.month')}</div>
              <div>{`${run.month}/${run.year}`}</div>
            </div>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.assetCount')}</div>
              <div>{run.assetCount}</div>
            </div>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.totalDepreciation')}</div>
              <div>{money(run.totalDepreciation)}</div>
            </div>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('fixedAssets.journalEntry')}</div>
              <div>{run.journalEntryId ?? '—'}</div>
            </div>
          </div>
          {run.status === 'Posted' && (
            <FieldWrapper label={t('fixedAssets.reverseReason')}>
              <Input value={reason} onChange={(e) => setReason(e.target.value)} style={{ minWidth: 320 }} />
            </FieldWrapper>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <h3 style={{ marginTop: 0 }}>{t('fixedAssets.runLines')}</h3>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.asset')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.periodNumber')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.periodEnd')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('fixedAssets.amount')}</th>
              </tr>
            </thead>
            <tbody>
              {(data?.lines ?? []).map((line) => (
                <tr key={line.scheduleId} style={{ borderTop: '1px solid var(--color-border)' }}>
                  <td style={{ padding: 6 }}>{`${line.assetNumber} — ${line.assetNameAr}`}</td>
                  <td style={{ padding: 6 }}>{line.periodNumber}</td>
                  <td style={{ padding: 6 }}>{line.periodEnd}</td>
                  <td style={{ padding: 6 }}>{money(line.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
