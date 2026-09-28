import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { getFieldErrorMessage } from '../../../app/api';
import { BranchField } from '../../organization/branches/BranchField';
import { useAccountsList } from '../accounts/api';
import { useCreateCustody, useCreateCustodySettlement, useCustody, useCustodyList, type CustodySettlementLine } from './api';
import { todayLocal } from '../../../lib/date';

/** /accounting/custody-registers — screens 8-9 (01-Module-Accounting.md, section 5). */
export function CustodyPage() {
  const { t, i18n } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: custodyList } = useCustodyList();
  const { data: accounts } = useAccountsList(true);
  const accountOptions = [
    { value: 0, label: t('common.selectAccount') },
    ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
  ];
  const [search, setSearch] = useState('');
  const filteredCustodyList = custodyList?.filter((c) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return (
      String(c.employeeId).includes(query) ||
      c.status.toLowerCase().includes(query) ||
      String(c.amount).includes(query)
    );
  });
  const [selectedId, setSelectedId] = useState<number | undefined>();
  const { data: selected } = useCustody(selectedId);
  const { label } = useFieldLabels('ACCOUNTING_CUSTODY');

  const [employeeId, setEmployeeId] = useState(1);
  const [branchId, setBranchId] = useState<number | undefined>();
  const [amount, setAmount] = useState(0);
  const [issueDate, setIssueDate] = useState(todayLocal());
  const [treasuryAccountId, setTreasuryAccountId] = useState(0);
  const [receivableAccountId, setReceivableAccountId] = useState(0);
  const createCustody = useCreateCustody();

  const [settlementLines, setSettlementLines] = useState<CustodySettlementLine[]>([{ accountId: 0, amount: 0, description: '' }]);
  const createSettlement = useCreateCustodySettlement(selectedId ?? 0);

  const handleCreateCustody = async () => {
    if (!branchId) {
      showToast(t('custody.branchRequired'), 'error');
      return;
    }
    try {
      await createCustody.mutateAsync({
        employeeId, branchId, amount, issueDate, treasuryAccountId, custodyReceivableAccountId: receivableAccountId
      });
      showToast(t('custody.createSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleSettle = async () => {
    if (!selected) return;
    try {
      await createSettlement.mutateAsync({
        settlementDate: todayLocal(),
        custodyReceivableAccountId: receivableAccountId,
        treasuryAccountId,
        lines: settlementLines.filter((l) => l.accountId > 0 && l.amount > 0)
      });
      showToast(t('custody.settleSuccess'), 'success');
      setSettlementLines([{ accountId: 0, amount: 0, description: '' }]);
    } catch (error) {
      // Field-level errors (e.g. rule 22's exceeds-custody message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <h2 style={{ margin: 0 }}>{t('custody.title')}</h2>

      <Card>
        <CardHeader>{t('custody.newCustody')}</CardHeader>
        <CardBody>
        <ActionBar primary={{ key: 'issueCustody', label: t('custody.issueCustody'), onClick: handleCreateCustody }} />
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <Field label={label('employeeId', t('custody.employeeId'))}><Input type="number" value={employeeId} onChange={(e) => setEmployeeId(Number(e.target.value))} style={{ width: 90 }} /></Field>
          <BranchField value={branchId} onChange={setBranchId} label={label('branch', t('custody.branch'))} autoSelect />
          <Field label={label('amount', t('custody.amount'))}><Input type="number" value={amount} onChange={(e) => setAmount(Number(e.target.value))} style={{ width: 100 }} /></Field>
          <Field label={label('date', t('custody.date'))}><Input type="date" value={issueDate} onChange={(e) => setIssueDate(e.target.value)} /></Field>
          <Field label={label('treasuryAccount', t('custody.treasuryAccount'))}>
            <SearchableSelect value={treasuryAccountId} onChange={(v) => setTreasuryAccountId(Number(v))} options={accountOptions} style={{ width: 180 }} />
          </Field>
          <Field label={label('receivableAccount', t('custody.receivableAccount'))}>
            <SearchableSelect value={receivableAccountId} onChange={(v) => setReceivableAccountId(Number(v))} options={accountOptions} style={{ width: 180 }} />
          </Field>
        </div>
        </CardBody>
      </Card>

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ flex: 1 }}>
          <CardHeader>{t('custody.listHeading')}</CardHeader>
          <CardBody>
          <Input placeholder={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} style={{ width: '100%', marginBottom: 12 }} />
          {filteredCustodyList?.length === 0 && (
            <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
          )}
          {filteredCustodyList?.map((c) => (
            <div
              key={c.id}
              onClick={() => setSelectedId(c.id)}
              style={{
                display: 'flex', justifyContent: 'space-between', padding: 10, borderRadius: 6, cursor: 'pointer', fontSize: 13, marginBottom: 6,
                background: selectedId === c.id ? 'var(--color-navy-500)' : '#f4f5f7', color: selectedId === c.id ? '#fff' : 'inherit'
              }}
            >
              <span>{t('custody.employeeId')} {c.employeeId} — {c.amount.toLocaleString(i18n.language)}</span>
              {selectedId !== c.id && <StatusBadge status={c.status} />}
            </div>
          ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 2 }}>
          {!selected ? (
            <CardBody>
              <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('custody.selectCustody')}</p>
            </CardBody>
          ) : (
            <>
              <CardHeader end={<StatusBadge status={selected.status} />}>
                {t('custody.employeeId')} {selected.employeeId} — {selected.amount.toLocaleString(i18n.language)}
              </CardHeader>
              <CardBody>
              {(selected.status === 'Open' || selected.status === 'PartiallySettled') && (
                <ActionBar
                  primary={{ key: 'settle', label: t('custody.settle'), onClick: handleSettle }}
                  secondary={[
                    {
                      key: 'addLine',
                      label: t('custody.addLine'),
                      onClick: () => setSettlementLines((prev) => [...prev, { accountId: 0, amount: 0 }])
                    }
                  ]}
                />
              )}
              {selected.settlements.map((s) => (
                <div key={s.id} style={{ fontSize: 13, padding: 8, background: '#f4f5f7', borderRadius: 6, marginBottom: 8 }}>
                  {label('settlementOn', t('custody.settlementOn'))} {s.settlementDate} — {label('difference', t('custody.difference'))}: {s.remainingAmount.toLocaleString(i18n.language)}
                </div>
              ))}

              {(selected.status === 'Open' || selected.status === 'PartiallySettled') && (
                <div style={{ borderTop: '1px solid var(--color-border)', paddingTop: 16, marginTop: 16 }}>
                  <h4 style={{ fontSize: 14 }}>{t('custody.settlementHeading')}</h4>
                  {settlementLines.map((line, i) => (
                    <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
                      <SearchableSelect
                        value={line.accountId}
                        onChange={(v) => setSettlementLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, accountId: Number(v) } : l)))}
                        options={[
                          { value: 0, label: t('custody.selectExpenseAccount') },
                          ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
                        ]}
                        style={{ flex: 1 }}
                      />
                      <Input
                        type="number"
                        placeholder={t('custody.amount')}
                        value={line.amount}
                        onChange={(e) => setSettlementLines((prev) => prev.map((l, idx) => (idx === i ? { ...l, amount: Number(e.target.value) } : l)))}
                        style={{ width: 100 }}
                      />
                    </div>
                  ))}
                </div>
              )}
              </CardBody>
            </>
          )}
        </Card>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 12, color: 'var(--color-text-muted)' }}>
      {label}
      {children}
    </label>
  );
}
