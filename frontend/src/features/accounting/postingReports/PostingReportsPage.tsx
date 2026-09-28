import { useState, type CSSProperties, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Badge } from '../../../ui-kit/Badge';
import { Input } from '../../../ui-kit/Field';

const BASE = '/accounting/posting-reports';

type ReportKey = 'failures' | 'unposted' | 'auto' | 'bySource' | 'pos' | 'variances' | 'templates' | 'periods' | 'integrity';

const REPORTS: ReportKey[] = ['failures', 'unposted', 'auto', 'bySource', 'pos', 'variances', 'templates', 'periods', 'integrity'];

const firstOfMonth = () => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1).toLocaleDateString('en-CA'); };
const today = () => new Date().toLocaleDateString('en-CA');

/**
 * /accounting/posting-reports — the posting engine's reports (00-Posting-Engine-Architecture.md
 * section 10). Failed postings come first: a document that could not post is still a draft, and
 * this is the only place anyone other than the person who hit the error will see it.
 */
export function PostingReportsPage() {
  const { t } = useTranslation();
  const [report, setReport] = useState<ReportKey>('failures');
  const [from, setFrom] = useState(firstOfMonth());
  const [to, setTo] = useState(today());
  const dated = ['auto', 'bySource', 'pos', 'variances'].includes(report);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('postingReports.title')}</h2>

      <div role="tablist" style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        {REPORTS.map((key) => (
          <button
            key={key}
            role="tab"
            aria-selected={report === key}
            onClick={() => setReport(key)}
            style={{
              padding: '7px 14px', borderRadius: 999, fontSize: 13, fontFamily: 'inherit', cursor: 'pointer',
              border: `1px solid ${report === key ? 'var(--color-primary)' : 'var(--color-border)'}`,
              background: report === key ? 'var(--color-primary)' : 'var(--color-surface)',
              color: report === key ? 'var(--color-on-primary, #fff)' : 'var(--color-text)'
            }}
          >
            {t(`postingReports.tab.${key}`)}
          </button>
        ))}
      </div>

      {dated && (
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', fontSize: 13 }}>
          <span>{t('postingReports.from')}</span>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ width: 170 }} />
          <span>{t('postingReports.to')}</span>
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ width: 170 }} />
        </div>
      )}

      <Card>
        <CardBody>
          <p style={{ margin: '0 0 12px', fontSize: 12.5, color: 'var(--color-text-muted)', lineHeight: 1.8 }}>{t(`postingReports.about.${report}`)}</p>
          {report === 'failures' && <FailuresReport />}
          {report === 'unposted' && <UnpostedReport />}
          {report === 'auto' && <AutoEntriesReport from={from} to={to} />}
          {report === 'bySource' && <BySourceReport from={from} to={to} />}
          {report === 'pos' && <POSEntriesReport from={from} to={to} />}
          {report === 'variances' && <VariancesReport from={from} to={to} />}
          {report === 'templates' && <TemplatesReport />}
          {report === 'periods' && <PeriodsReport />}
          {report === 'integrity' && <IntegrityReport />}
        </CardBody>
      </Card>
    </div>
  );
}

// ---------------------------------------------------------------------------- shared table

const cell: CSSProperties = { padding: '8px 10px', borderBottom: '1px solid var(--color-border)', fontSize: 13, textAlign: 'start', verticalAlign: 'top' };
const num: CSSProperties = { ...cell, textAlign: 'end', fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' };

function Table<T>({ rows, columns, empty }: {
  rows: T[] | undefined;
  columns: { label: string; render: (row: T) => ReactNode; numeric?: boolean }[];
  empty: string;
}) {
  const { t } = useTranslation();
  if (!rows) return <p>{t('common.loading')}</p>;
  if (rows.length === 0) return <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{empty}</p>;
  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ background: 'var(--color-surface-2)' }}>
            {columns.map((c) => <th key={c.label} style={c.numeric ? num : cell}>{c.label}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i}>{columns.map((c) => <td key={c.label} style={c.numeric ? num : cell}>{c.render(row)}</td>)}</tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function useReport<T>(path: string, params?: Record<string, unknown>) {
  return useQuery({
    queryKey: ['posting-report', path, params],
    queryFn: async () => (await api.get<T>(`${BASE}/${path}`, { params })).data
  });
}

const money = (n: number) => n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const entryLink = (id: number, number: string) => <Link to={`/accounting/journal-entries/${id}`}>{number}</Link>;

// ---------------------------------------------------------------------------- reports

interface FailureRow {
  id: number; occurredAtUtc: string; screenNameAr: string; sourceDocumentId: number; description: string;
  errorCode: string; errorMessage: string; isResolved: boolean; resolvedByEntryNumber: string | null;
}

function FailuresReport() {
  const { t } = useTranslation();
  const [includeResolved, setIncludeResolved] = useState(false);
  const { data } = useReport<FailureRow[]>('failures', { includeResolved });
  return (
    <>
      <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 13, marginBottom: 10 }}>
        <input type="checkbox" checked={includeResolved} onChange={(e) => setIncludeResolved(e.target.checked)} />
        {t('postingReports.includeResolved')}
      </label>
      <Table rows={data} empty={t('postingReports.noFailures')} columns={[
        { label: t('postingReports.col.when'), render: (r) => new Date(r.occurredAtUtc).toLocaleString() },
        { label: t('postingReports.col.screen'), render: (r) => r.screenNameAr },
        { label: t('postingReports.col.document'), render: (r) => `${r.description} (#${r.sourceDocumentId})` },
        { label: t('postingReports.col.error'), render: (r) => <><code style={{ fontSize: 11.5 }}>{r.errorCode}</code><div>{r.errorMessage}</div></> },
        { label: t('postingReports.col.status'), render: (r) => r.isResolved
          ? <Badge label={t('postingReports.resolvedBy', { entry: r.resolvedByEntryNumber })} tone="success" />
          : <Badge label={t('postingReports.open')} tone="error" /> }
      ]} />
    </>
  );
}

interface UnpostedRow { screenCode: string; screenNameAr: string; isConfigured: boolean; withoutEntry: number; withoutEntrySinceActivation: number }

function UnpostedReport() {
  const { t } = useTranslation();
  const { data } = useReport<UnpostedRow[]>('unposted-documents');
  return (
    <Table rows={data} empty="—" columns={[
      { label: t('postingReports.col.screen'), render: (r) => r.screenNameAr },
      { label: t('postingReports.col.posting'), render: (r) => r.isConfigured
        ? <Badge label={t('postingReports.configured')} tone="success" />
        : <Badge label={t('postingReports.notConfigured')} tone="neutral" /> },
      { label: t('postingReports.col.withoutEntry'), render: (r) => r.withoutEntry, numeric: true },
      { label: t('postingReports.col.sinceActivation'), render: (r) => r.isConfigured
        ? <span style={{ color: r.withoutEntrySinceActivation > 0 ? 'var(--color-danger, #B3261E)' : undefined, fontWeight: 700 }}>{r.withoutEntrySinceActivation}</span>
        : '—', numeric: true }
    ]} />
  );
}

interface AutoEntryRow {
  id: number; entryNumber: string; entryDate: string; sourceModule: string; sourceDocumentType: string | null; sourceDocumentId: number | null;
  description: string; total: number; status: string; isReversal: boolean; screenCode: string | null; templateVersion: number | null;
}

function AutoEntriesReport({ from, to }: { from: string; to: string }) {
  const { t } = useTranslation();
  const { data } = useReport<AutoEntryRow[]>('auto-entries', { from, to });
  return (
    <Table rows={data} empty={t('postingReports.noEntries')} columns={[
      { label: t('postingReports.col.entry'), render: (r) => entryLink(r.id, r.entryNumber) },
      { label: t('postingReports.col.date'), render: (r) => r.entryDate },
      { label: t('postingReports.col.source'), render: (r) => t(`postingReports.module.${r.sourceModule}`, { defaultValue: r.sourceModule }) },
      { label: t('postingReports.col.description'), render: (r) => <>{r.description}{r.isReversal && <> <Badge label={t('postingReports.reversal')} tone="warning" /></>}</> },
      { label: t('postingReports.col.template'), render: (r) => (r.screenCode ? `${r.screenCode} · v${r.templateVersion}` : '—') },
      { label: t('postingReports.col.total'), render: (r) => money(r.total), numeric: true }
    ]} />
  );
}

interface BySourceRow { sourceModule: string; sourceDocumentType: string | null; entryCount: number; total: number; reversalCount: number }

function BySourceReport({ from, to }: { from: string; to: string }) {
  const { t } = useTranslation();
  const { data } = useReport<BySourceRow[]>('by-source', { from, to });
  return (
    <Table rows={data} empty={t('postingReports.noEntries')} columns={[
      { label: t('postingReports.col.source'), render: (r) => t(`postingReports.module.${r.sourceModule}`, { defaultValue: r.sourceModule }) },
      { label: t('postingReports.col.documentType'), render: (r) => (r.sourceDocumentType ? t(`postingReports.docType.${r.sourceDocumentType}`, { defaultValue: r.sourceDocumentType }) : '—') },
      { label: t('postingReports.col.count'), render: (r) => r.entryCount, numeric: true },
      { label: t('postingReports.col.reversals'), render: (r) => r.reversalCount, numeric: true },
      { label: t('postingReports.col.total'), render: (r) => money(r.total), numeric: true }
    ]} />
  );
}

interface POSEntryRow { entryId: number; entryNumber: string; entryDate: string; branchId: number | null; scope: string; invoiceCount: number; total: number; description: string }

function POSEntriesReport({ from, to }: { from: string; to: string }) {
  const { t } = useTranslation();
  const { data } = useReport<POSEntryRow[]>('pos-entries', { from, to });
  return (
    <Table rows={data} empty={t('postingReports.noEntries')} columns={[
      { label: t('postingReports.col.entry'), render: (r) => entryLink(r.entryId, r.entryNumber) },
      { label: t('postingReports.col.date'), render: (r) => r.entryDate },
      { label: t('postingReports.col.scope'), render: (r) => t(`postingReports.docType.${r.scope}`, { defaultValue: r.scope }) },
      { label: t('postingReports.col.description'), render: (r) => r.description },
      { label: t('postingReports.col.invoices'), render: (r) => r.invoiceCount || '—', numeric: true },
      { label: t('postingReports.col.total'), render: (r) => money(r.total), numeric: true }
    ]} />
  );
}

interface VarianceRow {
  shiftId: number; closedAtUtc: string | null; status: string; expected: number; actual: number; difference: number;
  threshold: number; classification: 'Surplus' | 'CompanyShortage' | 'CashierShortage'; varianceEntryNumber: string | null;
}

function VariancesReport({ from, to }: { from: string; to: string }) {
  const { t } = useTranslation();
  const { data } = useReport<VarianceRow[]>('shift-variances', { from, to });
  const tone = { Surplus: 'info', CompanyShortage: 'warning', CashierShortage: 'error' } as const;
  return (
    <Table rows={data} empty={t('postingReports.noVariances')} columns={[
      { label: t('postingReports.col.shift'), render: (r) => <Link to={`/pos/shifts/${r.shiftId}`}>#{r.shiftId}</Link> },
      { label: t('postingReports.col.closedAt'), render: (r) => (r.closedAtUtc ? new Date(r.closedAtUtc).toLocaleString() : '—') },
      { label: t('postingReports.col.expected'), render: (r) => money(r.expected), numeric: true },
      { label: t('postingReports.col.actual'), render: (r) => money(r.actual), numeric: true },
      { label: t('postingReports.col.difference'), render: (r) => money(r.difference), numeric: true },
      { label: t('postingReports.col.classification'), render: (r) => <Badge label={t(`postingReports.variance.${r.classification}`, { threshold: r.threshold })} tone={tone[r.classification]} /> },
      { label: t('postingReports.col.entry'), render: (r) => r.varianceEntryNumber ?? '—' }
    ]} />
  );
}

interface TemplateUsageRow { templateId: number; screenNameAr: string; templateNameAr: string; versionNumber: number; isCurrentVersion: boolean; isActive: boolean; entryCount: number; lastUsedAtUtc: string | null }

function TemplatesReport() {
  const { t } = useTranslation();
  const { data } = useReport<TemplateUsageRow[]>('template-usage');
  return (
    <Table rows={data} empty={t('postingReports.noTemplates')} columns={[
      { label: t('postingReports.col.screen'), render: (r) => `${r.screenNameAr} — ${r.templateNameAr}` },
      { label: t('postingReports.col.version'), render: (r) => <>v{r.versionNumber} {r.isCurrentVersion ? <Badge label={t('postingReports.current')} tone="info" /> : null}</> },
      { label: t('postingReports.col.status'), render: (r) => (r.isActive ? <Badge label={t('postingReports.active')} tone="success" /> : <Badge label={t('postingReports.inactive')} tone="neutral" />) },
      { label: t('postingReports.col.entries'), render: (r) => r.entryCount, numeric: true },
      { label: t('postingReports.col.lastUsed'), render: (r) => (r.lastUsedAtUtc ? new Date(r.lastUsedAtUtc).toLocaleString() : '—') }
    ]} />
  );
}

interface PeriodRow { id: number; periodStart: string; periodEnd: string; status: string; closedAtUtc: string | null; postedEntries: number; autoEntries: number }

function PeriodsReport() {
  const { t } = useTranslation();
  const { data } = useReport<PeriodRow[]>('periods');
  return (
    <Table rows={data} empty={t('postingReports.noPeriods')} columns={[
      { label: t('postingReports.col.period'), render: (r) => `${r.periodStart} → ${r.periodEnd}` },
      { label: t('postingReports.col.status'), render: (r) => (r.status === 'Closed' ? <Badge label={t('postingReports.closed')} tone="neutral" /> : <Badge label={t('postingReports.openPeriod')} tone="success" />) },
      { label: t('postingReports.col.entries'), render: (r) => r.postedEntries, numeric: true },
      { label: t('postingReports.col.autoEntries'), render: (r) => r.autoEntries, numeric: true }
    ]} />
  );
}

interface IntegrityRow { entryId: number; entryNumber: string; entryDate: string; issue: string; headerDebit: number; headerCredit: number; linesDebit: number; linesCredit: number }

function IntegrityReport() {
  const { t } = useTranslation();
  const { data } = useReport<IntegrityRow[]>('integrity');
  return (
    <Table rows={data} empty={t('postingReports.integrityOk')} columns={[
      { label: t('postingReports.col.entry'), render: (r) => entryLink(r.entryId, r.entryNumber) },
      { label: t('postingReports.col.date'), render: (r) => r.entryDate },
      { label: t('postingReports.col.issue'), render: (r) => t(`postingReports.issue.${r.issue}`) },
      { label: t('postingReports.col.linesDebit'), render: (r) => money(r.linesDebit), numeric: true },
      { label: t('postingReports.col.linesCredit'), render: (r) => money(r.linesCredit), numeric: true }
    ]} />
  );
}
