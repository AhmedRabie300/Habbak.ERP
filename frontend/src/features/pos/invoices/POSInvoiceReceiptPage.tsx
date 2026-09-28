import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { StatusBadge } from '../../../ui-kit/Badge';
import { usePOSInvoice } from './api';

/** /pos/invoices/:id — إيصال فاتورة نقطة بيع بعد إتمام الدفع (قاعدة 15: تُنشأ Posted مباشرة). */
export function POSInvoiceReceiptPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: invoice, isLoading } = usePOSInvoice(Number(id));

  if (isLoading || !invoice) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{invoice.invoiceNumber}</h2>
        <StatusBadge status={invoice.status} />
      </div>

      <ActionBar secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/pos/table-board') }]} />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', fontSize: 13, marginBottom: 16 }}>
            <div>{t('posInvoice.invoiceDate')}: {invoice.invoiceDate}</div>
            <div>{t('posInvoice.checkCode')}: {invoice.checkCode}</div>
            <div>{t('posInvoice.orderType')}: {t(`checkEdit.orderType${invoice.orderType}`)}</div>
            {invoice.etaReceiptStatus !== 'NotApplicable' && <div>{t('posInvoice.etaStatus')}: {t(`posInvoice.eta${invoice.etaReceiptStatus}`)}</div>}
          </div>

          <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.item')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.quantity')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.unitPrice')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.discount')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.lineTotal')}</th>
              </tr>
            </thead>
            <tbody>
              {invoice.lines.map((line) => (
                <tr key={line.itemId}>
                  <td style={{ padding: 8 }}>{line.itemCode} — {line.itemNameAr}</td>
                  <td style={{ padding: 8 }}>{line.quantity}</td>
                  <td style={{ padding: 8 }}>{line.unitPrice.toFixed(2)}</td>
                  <td style={{ padding: 8 }}>{line.discountAmount.toFixed(2)}</td>
                  <td style={{ padding: 8 }}>{line.lineTotal.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-end', marginTop: 16, fontSize: 13 }}>
            <div>{t('checkEdit.subtotal')}: {invoice.subtotal.toFixed(2)}</div>
            <div>{t('checkEdit.discountTotal')}: {invoice.discountAmount.toFixed(2)}</div>
            {invoice.manualDiscountAmount > 0 && <div>{t('checkEdit.manualDiscount')}: {invoice.manualDiscountAmount.toFixed(2)}</div>}
            {invoice.loyaltyDiscountAmount > 0 && <div>{t('checkEdit.loyaltyPointsRedeemed', { points: invoice.loyaltyPointsRedeemed })}: {invoice.loyaltyDiscountAmount.toFixed(2)}</div>}
            {invoice.serviceChargeAmount > 0 && <div>{t('paymentModal.serviceCharge')}: {invoice.serviceChargeAmount.toFixed(2)}</div>}
            {invoice.tipAmount > 0 && <div>{t('paymentModal.tip')}: {invoice.tipAmount.toFixed(2)}</div>}
            {invoice.taxAmount > 0 && <div>{t('paymentModal.vatShort')}: {invoice.taxAmount.toFixed(2)}</div>}
            <div style={{ fontWeight: 700, fontSize: 16 }}>{t('checkEdit.total')}: {invoice.total.toFixed(2)}</div>
          </div>

          <h3 style={{ margin: '20px 0 8px' }}>{t('posInvoice.payments')}</h3>
          <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('paymentModal.method')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('paymentModal.amount')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('paymentModal.amountTendered')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('paymentModal.change')}</th>
              </tr>
            </thead>
            <tbody>
              {invoice.payments.map((payment) => (
                <tr key={payment.id}>
                  <td style={{ padding: 8 }}>{payment.paymentMethodNameAr}</td>
                  <td style={{ padding: 8 }}>{payment.amount.toFixed(2)}</td>
                  <td style={{ padding: 8 }}>{payment.amountTendered?.toFixed(2) ?? '—'}</td>
                  <td style={{ padding: 8 }}>{payment.changeGiven?.toFixed(2) ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
