import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Input } from '../../../ui-kit/Field';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePayableInvoicesList } from './api';
import { useAllocationProposal, usePaymentAllocations, useSetPaymentAllocations } from './allocationApi';

const money = (value: number) => value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Splitting one payment across the supplier's invoices (Remarks4, item 7). The oldest-due-first
 * proposal comes from the server so the screen and the posting rule agree on what "oldest" means;
 * the user is free to overwrite any row. Saving is refused unless the rows add up to the payment,
 * which is the same check the command makes.
 */
export function InvoiceAllocationTable({
  paymentId,
  supplierId,
  amount,
  currencyCode,
  editable
}: {
  paymentId: number | undefined;
  supplierId: number | undefined;
  amount: number;
  currencyCode: string;
  editable: boolean;
}) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: invoices } = usePayableInvoicesList(supplierId);
  const { data: saved } = usePaymentAllocations(paymentId);
  const proposal = useAllocationProposal();
  const save = useSetPaymentAllocations(paymentId ?? 0);

  const [rows, setRows] = useState<Record<number, string>>({});

  useEffect(() => {
    if (!saved) return;
    setRows(Object.fromEntries(saved.map((a) => [a.purchaseInvoiceId, String(a.amount)])));
  }, [saved]);

  const allocated = useMemo(
    () => Object.values(rows).reduce((sum, value) => sum + (Number(value) || 0), 0),
    [rows]
  );
  const difference = Math.round((amount - allocated) * 100) / 100;

  const propose = async () => {
    if (!supplierId || !currencyCode) return;
    try {
      const suggested = await proposal.mutateAsync({ supplierId, amount, currencyCode });
      setRows(Object.fromEntries(suggested.map((a) => [a.purchaseInvoiceId, String(a.amount)])));
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const persist = async () => {
    if (!paymentId) return;
    try {
      await save.mutateAsync(
        Object.entries(rows)
          .filter(([, value]) => Number(value) > 0)
          .map(([invoiceId, value]) => ({ purchaseInvoiceId: Number(invoiceId), amount: Number(value) }))
      );
      showToast(t('supplierPayments.allocationSaved'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  // A posted payment shows what it settled, even for invoices that now have nothing outstanding.
  const listed = editable
    ? (invoices ?? [])
    : (saved ?? []).map((a) => ({
        id: a.purchaseInvoiceId,
        invoiceNumber: a.invoiceNumber,
        totalAmount: a.invoiceTotal,
        amountPaid: a.amountPaid,
        remainingAmount: a.invoiceTotal - a.amountPaid,
        currencyCode
      }));

  if (!supplierId || (listed.length === 0 && (saved ?? []).length === 0)) {
    return null;
  }

  const cell: React.CSSProperties = { padding: '6px 8px', textAlign: 'start' };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <h3 style={{ margin: 0 }}>{t('supplierPayments.allocationTitle')}</h3>
          {editable && paymentId !== undefined && (
            <div style={{ display: 'flex', gap: 8 }}>
              <Button onClick={propose}>{t('supplierPayments.allocateOldestFirst')}</Button>
              <Button variant="primary" onClick={persist} disabled={difference !== 0}>
                {t('supplierPayments.saveAllocation')}
              </Button>
            </div>
          )}
        </div>

        {editable && paymentId === undefined && (
          <p style={{ color: 'var(--color-text-muted)' }}>{t('supplierPayments.allocationAfterSave')}</p>
        )}

        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13, marginTop: 8 }}>
          <thead>
            <tr>
              <th style={cell}>{t('supplierPayments.invoiceNumber')}</th>
              <th style={cell}>{t('supplierPayments.invoiceTotal')}</th>
              <th style={cell}>{t('supplierPayments.invoicePaid')}</th>
              <th style={cell}>{t('supplierPayments.invoiceOutstanding')}</th>
              <th style={cell}>{t('supplierPayments.allocatedAmount')}</th>
            </tr>
          </thead>
          <tbody>
            {listed.map((invoice) => (
              <tr key={invoice.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                <td style={cell}>{invoice.invoiceNumber}</td>
                <td style={cell}>{money(invoice.totalAmount)}</td>
                <td style={cell}>{money(invoice.amountPaid)}</td>
                <td style={cell}>{money(invoice.remainingAmount)}</td>
                <td style={cell}>
                  <Input
                    type="number"
                    step="0.01"
                    style={{ width: 130 }}
                    disabled={!editable || paymentId === undefined}
                    value={rows[invoice.id] ?? ''}
                    onChange={(e) => setRows((prev) => ({ ...prev, [invoice.id]: e.target.value }))}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div style={{ display: 'flex', gap: 24, marginTop: 12, flexWrap: 'wrap' }}>
          <span>
            {t('supplierPayments.allocatedTotal')}: <strong>{money(allocated)}</strong> {currencyCode}
          </span>
          <span style={{ color: difference === 0 ? 'var(--color-success)' : 'var(--color-warning)' }}>
            {t('supplierPayments.unallocated')}: <strong>{money(difference)}</strong> {currencyCode}
          </span>
        </div>
      </CardBody>
    </Card>
  );
}
