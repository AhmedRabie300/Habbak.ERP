import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { Alert } from '../../../ui-kit/Alert';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { getFieldErrorMessage } from '../../../app/api';
import { useAccountsList } from '../accounts/api';
import {
  useAddBankReconciliationLine,
  useBankReconciliation,
  useBankReconciliationsList,
  useCompleteBankReconciliation,
  useCreateBankReconciliationRun
} from './api';
import { toLocalDateString } from '../../../lib/date';

const TRANSACTION_TYPES = ['Voucher', 'TreasuryTransfer', 'JournalEntry'];

/** My Remarks/Remarks2.md, remark 2.3 — a blank date field default; a whole calendar month is a
 * more useful default here than "today" for both ends, since periodTo always trails periodFrom. */
function currentMonthBounds() {
  const now = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), 1);
  const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
  return { start: toLocalDateString(start), end: toLocalDateString(end) };
}

/** /accounting/bank-reconciliations (01-Module-Accounting.md, section 5, screen 11). */
export function BankReconciliationsPage() {
  const { t, i18n } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: list } = useBankReconciliationsList();
  const { data: accounts } = useAccountsList();
  const [search, setSearch] = useState('');
  const filteredList = list?.filter((r) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return r.periodFrom.toLowerCase().includes(query) || r.periodTo.toLowerCase().includes(query) || r.status.toLowerCase().includes(query);
  });
  const [selectedId, setSelectedId] = useState<number | undefined>();
  const { data: selected } = useBankReconciliation(selectedId);
  const { label } = useFieldLabels('ACCOUNTING_BANK_RECONCILIATIONS');

  const [bankAccountId, setBankAccountId] = useState(0);
  const [periodFrom, setPeriodFrom] = useState(() => currentMonthBounds().start);
  const [periodTo, setPeriodTo] = useState(() => currentMonthBounds().end);
  const createRun = useCreateBankReconciliationRun();

  const [lineType, setLineType] = useState('');
  const [lineSystemId, setLineSystemId] = useState('');
  const [lineStatementId, setLineStatementId] = useState('');
  const [lineAmount, setLineAmount] = useState(0);
  const addLine = useAddBankReconciliationLine(selectedId ?? 0);

  const [adjustmentAmount, setAdjustmentAmount] = useState(0);
  const [adjustmentAccountId, setAdjustmentAccountId] = useState(0);
  const complete = useCompleteBankReconciliation(selectedId ?? 0);

  const handleCreateRun = async () => {
    try {
      await createRun.mutateAsync({ bankAccountId, periodFrom, periodTo });
      showToast(t('bankReconciliations.createSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleAddLine = async () => {
    try {
      await addLine.mutateAsync({
        systemTransactionType: lineType || undefined,
        systemTransactionId: lineSystemId ? Number(lineSystemId) : undefined,
        bankStatementLineId: lineStatementId ? Number(lineStatementId) : undefined,
        matchedAmount: lineAmount,
        isAutoMatched: false
      });
      showToast(t('bankReconciliations.lineSuccess'), 'success');
      setLineSystemId('');
      setLineStatementId('');
      setLineAmount(0);
    } catch (error) {
      // Field-level errors (e.g. rule 23's source-required message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleComplete = async () => {
    try {
      await complete.mutateAsync({ adjustmentAmount, adjustmentAccountId: adjustmentAccountId || undefined });
      showToast(t('bankReconciliations.finishSuccess'), 'success');
    } catch (error) {
      // Field-level errors (e.g. rule 7's adjustment-account-required message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <h2 style={{ margin: 0 }}>{t('bankReconciliations.title')}</h2>
      <Alert tone="warning">{t('bankReconciliations.noImportWarning')}</Alert>

      <Card>
        <CardHeader>{t('bankReconciliations.newHeading')}</CardHeader>
        <CardBody>
        <ActionBar primary={{ key: 'openNew', label: t('bankReconciliations.openNew'), onClick: handleCreateRun }} />
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <SearchableSelect
            value={bankAccountId}
            onChange={(v) => setBankAccountId(Number(v))}
            options={[
              { value: 0, label: t('bankReconciliations.selectBankAccount') },
              ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
            ]}
            style={{ width: 180 }}
          />
          <Input type="date" value={periodFrom} onChange={(e) => setPeriodFrom(e.target.value)} />
          <Input type="date" value={periodTo} onChange={(e) => setPeriodTo(e.target.value)} />
        </div>
        </CardBody>
      </Card>

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ flex: 1 }}>
          <CardHeader>{t('bankReconciliations.listHeading')}</CardHeader>
          <CardBody>
          <Input placeholder={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} style={{ width: '100%', marginBottom: 12 }} />
          {filteredList?.length === 0 && (
            <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
          )}
          {filteredList?.map((r) => (
            <div
              key={r.id}
              onClick={() => setSelectedId(r.id)}
              style={{
                display: 'flex', justifyContent: 'space-between', padding: 10, borderRadius: 6, cursor: 'pointer', fontSize: 13, marginBottom: 6,
                background: selectedId === r.id ? 'var(--color-navy-500)' : '#f4f5f7', color: selectedId === r.id ? '#fff' : 'inherit'
              }}
            >
              <span>{r.periodFrom} → {r.periodTo}</span>
              {selectedId !== r.id && <StatusBadge status={r.status} />}
            </div>
          ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 2 }}>
          {!selected ? (
            <CardBody>
              <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('bankReconciliations.selectReconciliation')}</p>
            </CardBody>
          ) : (
            <>
              <CardHeader end={<StatusBadge status={selected.status} />}>{selected.periodFrom} → {selected.periodTo}</CardHeader>
              <CardBody>
              {selected.status === 'InProgress' && (
                <ActionBar
                  primary={{ key: 'finish', label: t('bankReconciliations.finish'), onClick: handleComplete }}
                  secondary={[{ key: 'addLine', label: t('bankReconciliations.addLine'), onClick: handleAddLine }]}
                />
              )}
              <table style={{ width: '100%', fontSize: 12, marginBottom: 16 }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('transactionType', t('bankReconciliations.transactionType'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('systemReference', t('bankReconciliations.systemReference'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('statementReference', t('bankReconciliations.statementReference'))}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{label('value', t('bankReconciliations.value'))}</th>
                  </tr>
                </thead>
                <tbody>
                  {selected.lines.map((l) => (
                    <tr key={l.id}>
                      <td style={{ padding: 6 }}>{l.systemTransactionType ?? '—'}</td>
                      <td style={{ padding: 6 }}>{l.systemTransactionId ?? '—'}</td>
                      <td style={{ padding: 6 }}>{l.bankStatementLineId ?? '—'}</td>
                      <td style={{ padding: 6 }}>{l.matchedAmount.toLocaleString(i18n.language)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {selected.status === 'InProgress' && (
                <>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 16 }}>
                    <SearchableSelect
                      value={lineType}
                      onChange={setLineType}
                      options={[
                        { value: '', label: t('bankReconciliations.noSystemTransaction') },
                        ...TRANSACTION_TYPES.map((type) => ({ value: type, label: type }))
                      ]}
                      style={{ width: 140 }}
                    />
                    <Input placeholder={t('bankReconciliations.systemTransactionIdPlaceholder')} value={lineSystemId} onChange={(e) => setLineSystemId(e.target.value)} style={{ width: 130 }} />
                    <Input placeholder={t('bankReconciliations.statementReferencePlaceholder')} value={lineStatementId} onChange={(e) => setLineStatementId(e.target.value)} style={{ width: 130 }} />
                    <Input type="number" placeholder={label('value', t('bankReconciliations.value'))} value={lineAmount} onChange={(e) => setLineAmount(Number(e.target.value))} style={{ width: 100 }} />
                  </div>

                  <div style={{ borderTop: '1px solid var(--color-border)', paddingTop: 16 }}>
                    <h4 style={{ fontSize: 14 }}>{t('bankReconciliations.finishHeading')}</h4>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'end', flexWrap: 'wrap' }}>
                      <Input type="number" placeholder={label('adjustmentAmount', t('bankReconciliations.adjustmentAmount'))} value={adjustmentAmount} onChange={(e) => setAdjustmentAmount(Number(e.target.value))} style={{ width: 160 }} />
                      {adjustmentAmount !== 0 && (
                        <SearchableSelect
                          value={adjustmentAccountId}
                          onChange={(v) => setAdjustmentAccountId(Number(v))}
                          options={[
                            { value: 0, label: label('adjustmentAccount', t('bankReconciliations.adjustmentAccount')) },
                            ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
                          ]}
                          style={{ width: 180 }}
                        />
                      )}
                    </div>
                  </div>
                </>
              )}
              </CardBody>
            </>
          )}
        </Card>
      </div>
    </div>
  );
}
