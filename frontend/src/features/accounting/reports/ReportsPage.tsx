import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { SearchableMultiSelect } from '../../../ui-kit/SearchableMultiSelect';
import { ExportMenu } from '../../../ui-kit/ExportMenu';
import { Card, CardBody } from '../../../ui-kit/Card';
import { useFieldLabels } from '../../common/useFieldLabels';
import type { ExportColumn } from '../../../lib/export';
import { useAccountsList } from '../accounts/api';
import { useDimensionsList, useDimensionValues } from '../dimensions/api';
import {
  useAccountStatement, useBalanceSheet, useBankReconciliationReport, useCashFlow, useCostCenterStatement,
  useCustodyReport, useExpensesByCostCenter, useIncomeStatement, useJournalBook, useTreasuryBankStatement,
  useTreasuryPosition, useTrialBalance,
  type AccountStatementLine, type BankReconciliationReportLine, type CostCenterExpenseLine,
  type TreasuryPositionLine, type TrialBalanceLine
} from './api';
import { todayLocal } from '../../../lib/date';

type ReportKey =
  | 'trialBalance' | 'journalBook' | 'generalLedger' | 'balanceSheet' | 'incomeStatement'
  | 'treasuryBankReport' | 'custodyReport' | 'bankReconciliationReport'
  | 'treasuryStatement' | 'cashFlow' | 'costCenterStatement' | 'treasuryPosition' | 'expensesByCostCenter';

/** My Remarks/Remarks2.md, remark 3.10 — the eight required Accounting reports, in the order
 * given there; the pre-existing extras (treasuryStatement/cashFlow/costCenterStatement/
 * treasuryPosition/expensesByCostCenter) follow after a divider, kept since they're still useful
 * even though they aren't part of that list. */
const AVAILABLE_REPORTS: ReportKey[] = [
  'trialBalance', 'journalBook', 'generalLedger', 'balanceSheet', 'incomeStatement',
  'treasuryBankReport', 'custodyReport', 'bankReconciliationReport'
];

const EXTRA_REPORTS: ReportKey[] = [
  'treasuryStatement', 'cashFlow', 'costCenterStatement', 'treasuryPosition', 'expensesByCostCenter'
];

const UNAVAILABLE_REPORTS = ['arAging', 'apAging', 'supplierStatement', 'customerStatement'] as const;

const today = todayLocal();
const monthStart = `${today.slice(0, 7)}-01`;

/** /accounting/reports (01-Module-Accounting.md, section 8). */
export function ReportsPage() {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<ReportKey | undefined>();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('reports.title')}</h2>

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ width: 260, height: 'fit-content' }}>
          <CardBody style={{ padding: 12 }}>
          {AVAILABLE_REPORTS.map((key) => (
            <ReportListItem key={key} label={t(`reports.${key}`)} active={selected === key} onClick={() => setSelected(key)} />
          ))}
          <div style={{ height: 1, background: 'var(--color-border)', margin: '8px 0' }} />
          {EXTRA_REPORTS.map((key) => (
            <ReportListItem key={key} label={t(`reports.${key}`)} active={selected === key} onClick={() => setSelected(key)} />
          ))}
          <div style={{ height: 1, background: 'var(--color-border)', margin: '8px 0' }} />
          {UNAVAILABLE_REPORTS.map((key) => (
            <div key={key} title={t('reports.notAvailableYet')} style={{ padding: '8px 10px', fontSize: 13, color: 'var(--color-text-muted)', opacity: 0.6, cursor: 'not-allowed' }}>
              {t(`reports.${key}`)}
            </div>
          ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 1 }}>
          <CardBody style={{ minHeight: 300 }}>
          {!selected && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.reports')}</p>}
          {selected === 'trialBalance' && <TrialBalanceReport />}
          {selected === 'journalBook' && <JournalBookReport />}
          {selected === 'generalLedger' && <AccountStatementReport titleKey="generalLedger" />}
          {selected === 'treasuryStatement' && <AccountStatementReport titleKey="treasuryStatement" />}
          {selected === 'incomeStatement' && <IncomeStatementReport />}
          {selected === 'balanceSheet' && <BalanceSheetReport />}
          {selected === 'treasuryBankReport' && <TreasuryBankReport />}
          {selected === 'custodyReport' && <CustodyReportView />}
          {selected === 'bankReconciliationReport' && <BankReconciliationReportView />}
          {selected === 'cashFlow' && <CashFlowReport />}
          {selected === 'costCenterStatement' && <CostCenterStatementReport />}
          {selected === 'treasuryPosition' && <TreasuryPositionReport />}
          {selected === 'expensesByCostCenter' && <ExpensesByCostCenterReport />}
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

function ReportListItem({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <div
      onClick={onClick}
      style={{
        padding: '8px 10px', borderRadius: 6, cursor: 'pointer', fontSize: 13, marginBottom: 2,
        background: active ? 'var(--color-navy-500)' : 'transparent', color: active ? '#fff' : 'inherit'
      }}
    >
      {label}
    </div>
  );
}

function StatementTable({ lines }: { lines: AccountStatementLine[] }) {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  return (
    <table style={{ width: '100%', fontSize: 13 }}>
      <thead>
        <tr>
          <th style={{ textAlign: 'start', padding: 6 }}>{label('date', t('reports.date'))}</th>
          <th style={{ textAlign: 'start', padding: 6 }}>{label('entryNumber', t('reports.entryNumber'))}</th>
          <th style={{ textAlign: 'start', padding: 6 }}>{label('description', t('reports.description'))}</th>
          <th style={{ textAlign: 'start', padding: 6 }}>{label('debit', t('reports.debit'))}</th>
          <th style={{ textAlign: 'start', padding: 6 }}>{label('credit', t('reports.credit'))}</th>
          <th style={{ textAlign: 'start', padding: 6 }}>{label('runningBalance', t('reports.runningBalance'))}</th>
        </tr>
      </thead>
      <tbody>
        {lines.map((l, i) => (
          <tr key={i}>
            <td style={{ padding: 6 }}>{l.date}</td>
            <td style={{ padding: 6 }}>{l.entryNumber}</td>
            <td style={{ padding: 6 }}>{l.description}</td>
            <td style={{ padding: 6 }}>{l.debit ? l.debit.toLocaleString(i18n.language) : ''}</td>
            <td style={{ padding: 6 }}>{l.credit ? l.credit.toLocaleString(i18n.language) : ''}</td>
            <td style={{ padding: 6, fontWeight: 600 }}>{l.runningBalance.toLocaleString(i18n.language)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function TrialBalanceReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [asOf, setAsOf] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useTrialBalance(asOf, run);

  const totalDebit = data?.reduce((s, l) => s + l.debit, 0) ?? 0;
  const totalCredit = data?.reduce((s, l) => s + l.credit, 0) ?? 0;

  const columns: ExportColumn<TrialBalanceLine>[] = [
    { header: label('account', t('reports.account')), value: (l) => `${l.code} - ${l.nameAr}` },
    { header: label('debit', t('reports.debit')), value: (l) => l.debit },
    { header: label('credit', t('reports.credit')), value: (l) => l.credit }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.trialBalance')}>
        <FieldInline label={label('asOf', t('reports.asOf'))}><Input type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('reports.trialBalance')} title={t('reports.trialBalance')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('account', t('reports.account'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('debit', t('reports.debit'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('credit', t('reports.credit'))}</th>
            </tr>
          </thead>
          <tbody>
            {data.map((l) => (
              <tr key={l.accountId}>
                <td style={{ padding: 6 }}>{l.code} - {l.nameAr}</td>
                <td style={{ padding: 6 }}>{l.debit ? l.debit.toLocaleString(i18n.language) : ''}</td>
                <td style={{ padding: 6 }}>{l.credit ? l.credit.toLocaleString(i18n.language) : ''}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr style={{ fontWeight: 700, borderTop: '2px solid var(--color-border)' }}>
              <td style={{ padding: 6 }}>{t('common.total')}</td>
              <td style={{ padding: 6 }}>{totalDebit.toLocaleString(i18n.language)}</td>
              <td style={{ padding: 6 }}>{totalCredit.toLocaleString(i18n.language)}</td>
            </tr>
          </tfoot>
        </table>
      )}
    </div>
  );
}

function AccountStatementReport({ titleKey }: { titleKey: 'generalLedger' | 'treasuryStatement' }) {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const { data: accounts } = useAccountsList();
  const [accountId, setAccountId] = useState(0);
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useAccountStatement(accountId, from, to, run && accountId > 0);

  const columns: ExportColumn<AccountStatementLine>[] = [
    { header: label('date', t('reports.date')), value: (l) => l.date },
    { header: label('entryNumber', t('reports.entryNumber')), value: (l) => l.entryNumber },
    { header: label('description', t('reports.description')), value: (l) => l.description },
    { header: label('debit', t('reports.debit')), value: (l) => l.debit },
    { header: label('credit', t('reports.credit')), value: (l) => l.credit },
    { header: label('runningBalance', t('reports.runningBalance')), value: (l) => l.runningBalance }
  ];

  return (
    <div>
      <ReportHeader title={t(`reports.${titleKey}`)}>
        <SearchableSelect
          value={accountId}
          onChange={(v) => setAccountId(Number(v))}
          options={[
            { value: 0, label: t('reports.selectAccount') },
            ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
          ]}
          style={{ minWidth: 200 }}
        />
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)} disabled={accountId === 0}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={data.lines} columns={columns} fileName={t(`reports.${titleKey}`)} title={t(`reports.${titleKey}`)} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <>
          <p style={{ fontSize: 13 }}>{label('openingBalance', t('reports.openingBalance'))}: <strong>{data.openingBalance.toLocaleString(i18n.language)}</strong></p>
          <StatementTable lines={data.lines} />
          <p style={{ fontSize: 13, marginTop: 8 }}>{label('closingBalance', t('reports.closingBalance'))}: <strong>{data.closingBalance.toLocaleString(i18n.language)}</strong></p>
        </>
      )}
    </div>
  );
}

function IncomeStatementReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useIncomeStatement(from, to, run);

  const exportRows = data
    ? [
        ...data.revenue.map((l) => ({ section: t('reports.totalRevenue'), code: l.code, name: l.name, amount: l.amount })),
        { section: '', code: '', name: t('reports.totalRevenue'), amount: data.totalRevenue },
        ...data.expenses.map((l) => ({ section: t('reports.totalExpenses'), code: l.code, name: l.name, amount: l.amount })),
        { section: '', code: '', name: t('reports.totalExpenses'), amount: data.totalExpenses },
        { section: '', code: '', name: t('reports.netIncome'), amount: data.netIncome }
      ]
    : [];
  const exportColumns: ExportColumn<(typeof exportRows)[number]>[] = [
    { header: t('reports.account'), value: (r) => `${r.code} ${r.name}`.trim() },
    { header: t('reports.debit'), value: (r) => r.amount }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.incomeStatement')}>
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={exportRows} columns={exportColumns} fileName={t('reports.incomeStatement')} title={t('reports.incomeStatement')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <div style={{ fontSize: 13 }}>
          <h4>{label('totalRevenue', t('reports.totalRevenue'))}</h4>
          {data.revenue.map((l) => <Row key={l.accountId} label={`${l.code} - ${l.name}`} value={l.amount} lang={i18n.language} />)}
          <Row label={label('totalRevenue', t('reports.totalRevenue'))} value={data.totalRevenue} bold lang={i18n.language} />
          <h4>{label('totalExpenses', t('reports.totalExpenses'))}</h4>
          {data.expenses.map((l) => <Row key={l.accountId} label={`${l.code} - ${l.name}`} value={l.amount} lang={i18n.language} />)}
          <Row label={label('totalExpenses', t('reports.totalExpenses'))} value={data.totalExpenses} bold lang={i18n.language} />
          <div style={{ height: 1, background: 'var(--color-border)', margin: '8px 0' }} />
          <Row label={label('netIncome', t('reports.netIncome'))} value={data.netIncome} bold color={data.netIncome >= 0 ? 'var(--color-success)' : 'var(--color-error)'} lang={i18n.language} />
        </div>
      )}
    </div>
  );
}

function BalanceSheetReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [asOf, setAsOf] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useBalanceSheet(asOf, run);

  const exportRows = data
    ? [
        ...data.assets.map((l) => ({ section: t('reports.totalAssets'), code: l.code, name: l.name, amount: l.amount })),
        { section: '', code: '', name: t('reports.totalAssets'), amount: data.totalAssets },
        ...data.liabilities.map((l) => ({ section: t('reports.totalLiabilities'), code: l.code, name: l.name, amount: l.amount })),
        { section: '', code: '', name: t('reports.totalLiabilities'), amount: data.totalLiabilities },
        ...data.equity.map((l) => ({ section: t('reports.totalEquity'), code: l.code, name: l.name, amount: l.amount })),
        { section: '', code: '', name: t('reports.totalEquity'), amount: data.totalEquity }
      ]
    : [];
  const exportColumns: ExportColumn<(typeof exportRows)[number]>[] = [
    { header: t('reports.account'), value: (r) => `${r.code} ${r.name}`.trim() },
    { header: t('reports.balance'), value: (r) => r.amount }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.balanceSheet')}>
        <FieldInline label={label('asOf', t('reports.asOf'))}><Input type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={exportRows} columns={exportColumns} fileName={t('reports.balanceSheet')} title={t('reports.balanceSheet')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <div style={{ fontSize: 13, display: 'flex', gap: 32 }}>
          <div style={{ flex: 1 }}>
            <h4>{label('totalAssets', t('reports.totalAssets'))}</h4>
            {data.assets.map((l) => <Row key={l.accountId} label={`${l.code} - ${l.name}`} value={l.amount} lang={i18n.language} />)}
            <Row label={label('totalAssets', t('reports.totalAssets'))} value={data.totalAssets} bold lang={i18n.language} />
          </div>
          <div style={{ flex: 1 }}>
            <h4>{label('totalLiabilities', t('reports.totalLiabilities'))}</h4>
            {data.liabilities.map((l) => <Row key={l.accountId} label={`${l.code} - ${l.name}`} value={l.amount} lang={i18n.language} />)}
            <Row label={label('totalLiabilities', t('reports.totalLiabilities'))} value={data.totalLiabilities} bold lang={i18n.language} />
            <h4>{label('totalEquity', t('reports.totalEquity'))}</h4>
            {data.equity.map((l) => <Row key={l.accountId} label={l.name} value={l.amount} lang={i18n.language} />)}
            <Row label={label('totalEquity', t('reports.totalEquity'))} value={data.totalEquity} bold lang={i18n.language} />
          </div>
        </div>
      )}
    </div>
  );
}

function CashFlowReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const { data: accounts } = useAccountsList();
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useCashFlow(selectedIds, from, to, run && selectedIds.length > 0);

  const exportRows = data
    ? [
        { label: t('reports.openingBalance'), amount: data.openingBalance },
        { label: t('reports.debit'), amount: data.totalDebit },
        { label: t('reports.credit'), amount: data.totalCredit },
        { label: t('reports.closingBalance'), amount: data.closingBalance }
      ]
    : [];
  const exportColumns: ExportColumn<(typeof exportRows)[number]>[] = [
    { header: t('reports.description'), value: (r) => r.label },
    { header: t('reports.balance'), value: (r) => r.amount }
  ];

  return (
    <div>
      <p style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
        {t('reports.cashFlowSimplifiedNote')}
      </p>
      <ReportHeader title={t('reports.cashFlow')}>
        <SearchableMultiSelect
          value={selectedIds}
          onChange={(v) => setSelectedIds(v.map(Number))}
          options={accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? []}
          placeholder={t('reports.selectAccount')}
          style={{ minWidth: 260 }}
        />
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)} disabled={selectedIds.length === 0}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={exportRows} columns={exportColumns} fileName={t('reports.cashFlow')} title={t('reports.cashFlow')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <div style={{ fontSize: 13 }}>
          <Row label={label('openingBalance', t('reports.openingBalance'))} value={data.openingBalance} lang={i18n.language} />
          <Row label={label('debit', t('reports.debit'))} value={data.totalDebit} lang={i18n.language} />
          <Row label={label('credit', t('reports.credit'))} value={data.totalCredit} lang={i18n.language} />
          <Row label={label('closingBalance', t('reports.closingBalance'))} value={data.closingBalance} bold lang={i18n.language} />
        </div>
      )}
    </div>
  );
}

function CostCenterStatementReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const { data: dimensions } = useDimensionsList();
  const costCenterDimension = dimensions?.find((d) => d.code === 'COST_CENTER');
  const { data: values } = useDimensionValues(costCenterDimension?.id);
  const [valueId, setValueId] = useState(0);
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useCostCenterStatement(valueId, from, to, run && valueId > 0);

  const columns: ExportColumn<AccountStatementLine>[] = [
    { header: label('date', t('reports.date')), value: (l) => l.date },
    { header: label('entryNumber', t('reports.entryNumber')), value: (l) => l.entryNumber },
    { header: label('description', t('reports.description')), value: (l) => l.description },
    { header: label('debit', t('reports.debit')), value: (l) => l.debit },
    { header: label('credit', t('reports.credit')), value: (l) => l.credit },
    { header: label('runningBalance', t('reports.runningBalance')), value: (l) => l.runningBalance }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.costCenterStatement')}>
        <SearchableSelect
          value={valueId}
          onChange={(v) => setValueId(Number(v))}
          options={[
            { value: 0, label: t('reports.selectCostCenter') },
            ...(values?.map((v) => ({ value: v.id, label: v.nameAr })) ?? [])
          ]}
          style={{ minWidth: 200 }}
        />
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)} disabled={valueId === 0}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={data.lines} columns={columns} fileName={t('reports.costCenterStatement')} title={t('reports.costCenterStatement')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <>
          <StatementTable lines={data.lines} />
          <p style={{ fontSize: 13, marginTop: 8 }}>{label('closingBalance', t('reports.closingBalance'))}: <strong>{data.closingBalance.toLocaleString(i18n.language)}</strong></p>
        </>
      )}
    </div>
  );
}

function TreasuryPositionReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [asOf, setAsOf] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useTreasuryPosition(asOf, run);

  const columns: ExportColumn<TreasuryPositionLine>[] = [
    { header: label('account', t('reports.account')), value: (l) => `${l.code} - ${l.nameAr}` },
    { header: label('balance', t('reports.balance')), value: (l) => l.balance }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.treasuryPosition')}>
        <FieldInline label={label('asOf', t('reports.asOf'))}><Input type="date" value={asOf} onChange={(e) => setAsOf(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('reports.treasuryPosition')} title={t('reports.treasuryPosition')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr><th style={{ textAlign: 'start', padding: 6 }}>{label('account', t('reports.account'))}</th><th style={{ textAlign: 'start', padding: 6 }}>{label('balance', t('reports.balance'))}</th></tr></thead>
          <tbody>
            {data.map((l) => (
              <tr key={l.accountId}><td style={{ padding: 6 }}>{l.code} - {l.nameAr}</td><td style={{ padding: 6 }}>{l.balance.toLocaleString(i18n.language)}</td></tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function ExpensesByCostCenterReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useExpensesByCostCenter(from, to, run);

  const columns: ExportColumn<CostCenterExpenseLine>[] = [
    { header: t('dimensions.nameAr'), value: (l) => l.nameAr },
    { header: label('debit', t('reports.debit')), value: (l) => l.amount }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.expensesByCostCenter')}>
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('reports.expensesByCostCenter')} title={t('reports.expensesByCostCenter')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr><th style={{ textAlign: 'start', padding: 6 }}>{t('dimensions.nameAr')}</th><th style={{ textAlign: 'start', padding: 6 }}>{label('debit', t('reports.debit'))}</th></tr></thead>
          <tbody>
            {data.map((l) => (
              <tr key={l.dimensionValueId}><td style={{ padding: 6 }}>{l.nameAr}</td><td style={{ padding: 6 }}>{l.amount.toLocaleString(i18n.language)}</td></tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

const JOURNAL_STATUSES = ['Draft', 'Posted', 'Rejected', 'Reversed'] as const;

function JournalBookReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [status, setStatus] = useState('');
  const [run, setRun] = useState(false);
  const { data, isFetching } = useJournalBook(from, to, status || undefined, run);

  const exportRows = (data ?? []).flatMap((e) => e.lines.map((l) => ({ ...e, line: l })));
  const columns: ExportColumn<(typeof exportRows)[number]>[] = [
    { header: label('entryNumber', t('reports.entryNumber')), value: (r) => r.entryNumber },
    { header: label('date', t('reports.date')), value: (r) => r.entryDate },
    { header: label('description', t('reports.description')), value: (r) => r.description },
    { header: t('reports.status'), value: (r) => t(`status.${r.status}`) },
    { header: label('account', t('reports.account')), value: (r) => `${r.line.accountCode} - ${r.line.accountName}` },
    { header: label('debit', t('reports.debit')), value: (r) => r.line.debit },
    { header: label('credit', t('reports.credit')), value: (r) => r.line.credit }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.journalBook')}>
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <SearchableSelect
          value={status}
          onChange={(v) => setStatus(String(v))}
          options={[{ value: '', label: t('reports.allStatuses') }, ...JOURNAL_STATUSES.map((s) => ({ value: s, label: t(`status.${s}`) }))]}
          style={{ minWidth: 140 }}
        />
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={exportRows} columns={columns} fileName={t('reports.journalBook')} title={t('reports.journalBook')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>}
      {data && data.map((entry) => (
        <div key={entry.id} style={{ marginBottom: 16, border: '1px solid var(--color-border)', borderRadius: 8, padding: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
            <span>{entry.entryNumber} — {entry.entryDate}</span>
            <span>{t(`status.${entry.status}`)}</span>
          </div>
          <p style={{ fontSize: 13, color: 'var(--color-text-muted)', margin: '0 0 6px' }}>{entry.description}</p>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 4 }}>{label('account', t('reports.account'))}</th>
                <th style={{ textAlign: 'start', padding: 4 }}>{label('debit', t('reports.debit'))}</th>
                <th style={{ textAlign: 'start', padding: 4 }}>{label('credit', t('reports.credit'))}</th>
              </tr>
            </thead>
            <tbody>
              {entry.lines.map((l, i) => (
                <tr key={i}>
                  <td style={{ padding: 4 }}>{l.accountCode} - {l.accountName}</td>
                  <td style={{ padding: 4 }}>{l.debit ? l.debit.toLocaleString(i18n.language) : ''}</td>
                  <td style={{ padding: 4 }}>{l.credit ? l.credit.toLocaleString(i18n.language) : ''}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr style={{ fontWeight: 700 }}>
                <td style={{ padding: 4 }}>{t('common.total')}</td>
                <td style={{ padding: 4 }}>{entry.totalDebit.toLocaleString(i18n.language)}</td>
                <td style={{ padding: 4 }}>{entry.totalCredit.toLocaleString(i18n.language)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      ))}
    </div>
  );
}

function TreasuryBankReport() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useTreasuryBankStatement(from, to, run);

  const exportRows = (data ?? []).flatMap((acc) => acc.lines.map((l) => ({ ...l, accountCode: acc.code, accountName: acc.nameAr })));
  const columns: ExportColumn<(typeof exportRows)[number]>[] = [
    { header: label('account', t('reports.account')), value: (r) => `${r.accountCode} - ${r.accountName}` },
    { header: label('date', t('reports.date')), value: (r) => r.date },
    { header: label('entryNumber', t('reports.entryNumber')), value: (r) => r.entryNumber },
    { header: label('description', t('reports.description')), value: (r) => r.description },
    { header: label('debit', t('reports.debit')), value: (r) => r.debit },
    { header: label('credit', t('reports.credit')), value: (r) => r.credit },
    { header: label('runningBalance', t('reports.runningBalance')), value: (r) => r.runningBalance }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.treasuryBankReport')}>
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={exportRows} columns={columns} fileName={t('reports.treasuryBankReport')} title={t('reports.treasuryBankReport')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>}
      {data && data.map((acc) => (
        <div key={acc.accountId} style={{ marginBottom: 16, border: '1px solid var(--color-border)', borderRadius: 8, padding: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
            <span>{acc.code} — {acc.nameAr}</span>
          </div>
          <p style={{ fontSize: 13 }}>{label('openingBalance', t('reports.openingBalance'))}: <strong>{acc.openingBalance.toLocaleString(i18n.language)}</strong></p>
          <StatementTable lines={acc.lines} />
          <p style={{ fontSize: 13, marginTop: 6 }}>{label('closingBalance', t('reports.closingBalance'))}: <strong>{acc.closingBalance.toLocaleString(i18n.language)}</strong></p>
        </div>
      ))}
    </div>
  );
}

function CustodyReportView() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const { data, isFetching } = useCustodyReport(true);

  const columns: ExportColumn<NonNullable<typeof data>[number]>[] = [
    { header: t('reports.employeeId'), value: (r) => r.employeeId },
    { header: t('reports.custodyAmount'), value: (r) => r.amount },
    { header: t('reports.date'), value: (r) => r.issueDate },
    { header: t('reports.status'), value: (r) => t(`status.${r.status}`) },
    { header: t('reports.totalSettled'), value: (r) => r.totalSettled }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.custodyReport')}>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('reports.custodyReport')} title={t('reports.custodyReport')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>}
      {data && data.map((c) => (
        <div key={c.id} style={{ marginBottom: 16, border: '1px solid var(--color-border)', borderRadius: 8, padding: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
            <span>{t('reports.employeeId')} {c.employeeId} — {c.issueDate}</span>
            <span>{t(`status.${c.status}`)}</span>
          </div>
          <p style={{ fontSize: 13 }}>
            {t('reports.custodyAmount')}: <strong>{c.amount.toLocaleString(i18n.language)}</strong>
            {' · '}
            {t('reports.totalSettled')}: <strong>{c.totalSettled.toLocaleString(i18n.language)}</strong>
          </p>
          {c.settlements.length === 0 ? (
            <p style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('reports.noSettlements')}</p>
          ) : (
            c.settlements.map((s, i) => (
              <div key={i} style={{ marginTop: 8 }}>
                <p style={{ fontSize: 12, fontWeight: 600, margin: '0 0 4px' }}>
                  {t('reports.settlementDate')}: {s.settlementDate} — {s.totalAmount.toLocaleString(i18n.language)}
                </p>
                <table style={{ width: '100%', fontSize: 12 }}>
                  <thead>
                    <tr>
                      <th style={{ textAlign: 'start', padding: 4 }}>{label('account', t('reports.account'))}</th>
                      <th style={{ textAlign: 'start', padding: 4 }}>{label('description', t('reports.description'))}</th>
                      <th style={{ textAlign: 'start', padding: 4 }}>{t('reports.amount')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {s.lines.map((l, li) => (
                      <tr key={li}>
                        <td style={{ padding: 4 }}>{l.accountCode} - {l.accountName}</td>
                        <td style={{ padding: 4 }}>{l.description ?? ''}</td>
                        <td style={{ padding: 4 }}>{l.amount.toLocaleString(i18n.language)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))
          )}
        </div>
      ))}
    </div>
  );
}

function BankReconciliationReportView() {
  const { t, i18n } = useTranslation();
  const { label } = useFieldLabels('ACCOUNTING_REPORTS');
  const { data: treasuryAccounts } = useTreasuryPosition(today, true);
  const [bankAccountId, setBankAccountId] = useState(0);
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useBankReconciliationReport(bankAccountId, from, to, run && bankAccountId > 0);

  const columns: ExportColumn<BankReconciliationReportLine>[] = [
    { header: t('reports.period'), value: (r) => `${r.periodFrom} - ${r.periodTo}` },
    { header: t('reports.status'), value: (r) => t(`status.${r.runStatus}`) },
    { header: t('reports.systemTransaction'), value: (r) => r.systemTransactionType ?? '' },
    { header: t('reports.matchedAmount'), value: (r) => r.matchedAmount },
    { header: t('reports.matchType'), value: (r) => (r.isAutoMatched ? t('reports.autoMatched') : t('reports.manualMatched')) }
  ];

  return (
    <div>
      <ReportHeader title={t('reports.bankReconciliationReport')}>
        <SearchableSelect
          value={bankAccountId}
          onChange={(v) => setBankAccountId(Number(v))}
          options={[
            { value: 0, label: t('reports.selectBankAccount') },
            ...(treasuryAccounts?.map((a) => ({ value: a.accountId, label: `${a.code} - ${a.nameAr}` })) ?? [])
          ]}
          style={{ minWidth: 220 }}
        />
        <FieldInline label={label('from', t('reports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('reports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)} disabled={bankAccountId === 0}>{t('reports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('reports.bankReconciliationReport')} title={t('reports.bankReconciliationReport')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>}
      {data && data.length > 0 && (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('reports.period')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('reports.status')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('reports.systemTransaction')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('reports.bankStatementLine')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('reports.matchedAmount')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('reports.matchType')}</th>
            </tr>
          </thead>
          <tbody>
            {data.map((l, i) => (
              <tr key={i}>
                <td style={{ padding: 6 }}>{l.periodFrom} - {l.periodTo}</td>
                <td style={{ padding: 6 }}>{t(`status.${l.runStatus}`)}</td>
                <td style={{ padding: 6 }}>{l.systemTransactionType ?? '—'}{l.systemTransactionId ? ` #${l.systemTransactionId}` : ''}</td>
                <td style={{ padding: 6 }}>{l.bankStatementLineId ?? '—'}</td>
                <td style={{ padding: 6 }}>{l.matchedAmount.toLocaleString(i18n.language)}</td>
                <td style={{ padding: 6 }}>{l.isAutoMatched ? t('reports.autoMatched') : t('reports.manualMatched')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function ReportHeader({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 16 }}>
      <h3 style={{ marginTop: 0, fontSize: 15 }}>{title}</h3>
      <div style={{ display: 'flex', gap: 8, alignItems: 'end', flexWrap: 'wrap' }}>{children}</div>
    </div>
  );
}

function FieldInline({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 12, color: 'var(--color-text-muted)' }}>
      {label}
      {children}
    </label>
  );
}

function Row({ label, value, bold, color, lang }: { label: string; value: number; bold?: boolean; color?: string; lang: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 0', fontWeight: bold ? 700 : 400, color }}>
      <span>{label}</span>
      <span>{value.toLocaleString(lang)}</span>
    </div>
  );
}
