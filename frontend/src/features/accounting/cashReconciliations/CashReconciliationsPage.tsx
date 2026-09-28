import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { Badge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { getFieldErrorMessage } from '../../../app/api';
import { useAccountsList } from '../accounts/api';
import { useApproveCashReconciliation, useCashReconciliation, useCashReconciliationsList, useCreateCashReconciliation } from './api';
import { todayLocal } from '../../../lib/date';

/** /accounting/cash-reconciliations (01-Module-Accounting.md, section 5, screen 10). */
export function CashReconciliationsPage() {
  const { t, i18n } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const approvedLabel = (isApproved: boolean) => (isApproved ? t('status.Approved') : t('status.PendingApproval'));
  const { data: list } = useCashReconciliationsList();
  const { data: accounts } = useAccountsList();
  const [search, setSearch] = useState('');
  const filteredList = list?.filter((r) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return (
      r.reconciliationDate.toLowerCase().includes(query) ||
      String(r.differenceAmount).includes(query) ||
      approvedLabel(r.isApproved).toLowerCase().includes(query)
    );
  });
  const [selectedId, setSelectedId] = useState<number | undefined>();
  const { data: selected } = useCashReconciliation(selectedId);
  const { label } = useFieldLabels('ACCOUNTING_CASH_RECONCILIATIONS');

  const [treasuryAccountId, setTreasuryAccountId] = useState(0);
  const [reconciliationDate, setReconciliationDate] = useState(todayLocal());
  const [actualBalance, setActualBalance] = useState(0);
  const [differenceReason, setDifferenceReason] = useState('');
  const createReconciliation = useCreateCashReconciliation();
  const approveReconciliation = useApproveCashReconciliation(selectedId ?? 0);

  const handleCreate = async () => {
    try {
      await createReconciliation.mutateAsync({ treasuryAccountId, reconciliationDate, actualBalance, differenceReason: differenceReason || undefined });
      showToast(t('cashReconciliations.createSuccess'), 'success');
    } catch (error) {
      // Field-level errors (e.g. the reason-required message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleApprove = async () => {
    try {
      await approveReconciliation.mutateAsync();
      showToast(t('cashReconciliations.approveSuccess'), 'success');
    } catch (error) {
      // Field-level errors (e.g. rule 18's draft-vouchers message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <h2 style={{ margin: 0 }}>{t('cashReconciliations.title')}</h2>

      <Card>
        <CardHeader>{t('cashReconciliations.newHeading')}</CardHeader>
        <CardBody>
        <ActionBar primary={{ key: 'newReconciliation', label: t('cashReconciliations.newReconciliation'), onClick: handleCreate }} />
        <p style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('cashReconciliations.hint')}</p>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <SearchableSelect
            value={treasuryAccountId}
            onChange={(v) => setTreasuryAccountId(Number(v))}
            options={[
              { value: 0, label: t('cashReconciliations.selectTreasury') },
              ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
            ]}
            style={{ width: 180 }}
          />
          <Input type="date" value={reconciliationDate} onChange={(e) => setReconciliationDate(e.target.value)} />
          <Input type="number" placeholder={label('actualBalance', t('cashReconciliations.actualBalance'))} value={actualBalance} onChange={(e) => setActualBalance(Number(e.target.value))} style={{ width: 160 }} />
          <Input placeholder={t('cashReconciliations.differenceReason')} value={differenceReason} onChange={(e) => setDifferenceReason(e.target.value)} style={{ width: 200 }} />
        </div>
        </CardBody>
      </Card>

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ flex: 1 }}>
          <CardHeader>{t('cashReconciliations.listHeading')}</CardHeader>
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
              <span>{r.reconciliationDate} — {label('difference', t('cashReconciliations.difference'))} {r.differenceAmount.toLocaleString(i18n.language)}</span>
              {selectedId !== r.id && <Badge label={approvedLabel(r.isApproved)} tone={r.isApproved ? 'success' : 'warning'} />}
            </div>
          ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 1 }}>
          {!selected ? (
            <CardBody>
              <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('cashReconciliations.selectReconciliation')}</p>
            </CardBody>
          ) : (
            <>
              <CardHeader end={<Badge label={approvedLabel(selected.isApproved)} tone={selected.isApproved ? 'success' : 'warning'} />}>
                {selected.reconciliationDate}
              </CardHeader>
              <CardBody>
              {!selected.isApproved && (
                <ActionBar primary={{ key: 'approve', label: t('cashReconciliations.approveReconciliation'), onClick: handleApprove }} />
              )}
              <div style={{ fontSize: 13, display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 16 }}>
                <div>{label('expectedBalance', t('cashReconciliations.expectedBalance'))}: {selected.expectedBalance.toLocaleString(i18n.language)}</div>
                <div>{label('actualBalance', t('cashReconciliations.actualBalanceLabel'))}: {selected.actualBalance.toLocaleString(i18n.language)}</div>
                <div style={{ color: selected.differenceAmount === 0 ? 'var(--color-success)' : 'var(--color-error)' }}>
                  {label('difference', t('cashReconciliations.difference'))}: {selected.differenceAmount.toLocaleString(i18n.language)}
                </div>
                {selected.differenceReason && <div>{label('reason', t('cashReconciliations.reason'))}: {selected.differenceReason}</div>}
              </div>
              </CardBody>
            </>
          )}
        </Card>
      </div>
    </div>
  );
}
