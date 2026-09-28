import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useParams } from 'react-router-dom';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useButtonPermission } from '../../auth/access';
import { usePOSTerminalsList } from '../terminals/api';
import { useBranchPOSSettings } from '../settings/api';
import { usePOSPaymentMethodConfigsList } from '../paymentMethodConfigs/api';
import { useCompleteCheckPayment } from '../invoices/api';
import { useCheck } from './api';

interface PaymentRow {
  paymentMethodId: number | '';
  amount: string;
  amountTendered: string;
  cardTransactionReference: string;
}

const emptyRow = (amount = ''): PaymentRow => ({ paymentMethodId: '', amount, amountTendered: '', cardTransactionReference: '' });

/** /pos/checks/:id/payment — screen مصمَّم على غرار "شاشة الدفع المتقدمة" في
 * Coffee_ERP_Full_System_Mockup.html: بانل ملخص الفاتورة (totals-strip) + بانل طرق الدفع
 * (جدول أسطر دفع قابل للإضافة + شريط رصيد Balanced/Unbalanced) + زر تأكيد كبير. قاعدة 12: إجمالي
 * الدفعات لازم يساوي الإجمالي بالضبط قبل ما يتفعّل زر التأكيد. */
export function PaymentPage() {
  const { t } = useTranslation();
  const canSplit = useButtonPermission('POS_TABLE_BOARD', 'SplitBill');
  const { id } = useParams();
  const checkId = Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: check, isLoading } = useCheck(checkId);
  const { data: terminals } = usePOSTerminalsList();
  const terminal = terminals?.find((tItem) => tItem.id === check?.posTerminalId);
  const { data: settings } = useBranchPOSSettings(terminal?.branchId);
  const { data: methodConfigs } = usePOSPaymentMethodConfigsList(check?.posTerminalId);
  const completeMutation = useCompleteCheckPayment();

  const enabledMethods = (methodConfigs ?? []).filter((c) => c.isEnabled);
  // check.manualDiscountAmount already comes pre-computed from the backend (ManualDiscountCalculator) —
  // reusing it here instead of re-deriving from type/value keeps this screen and CompleteCheckPaymentCommand
  // byte-identical without duplicating the percentage/fixed/clamp logic client-side.
  // Every figure below the line total comes from the backend, which computes them with the same
  // calculator CompleteCheckPaymentCommand charges with. Re-deriving the rates here is what used to
  // risk POS-PAYMENT-AMOUNT-MISMATCH on any rounding difference; the tip is the only part this
  // screen owns, because it is entered here.
  const serviceChargeAmount = check?.serviceChargeAmount ?? 0;
  const taxAmount = check?.taxAmount ?? 0;

  const [tipAmount, setTipAmount] = useState('0');
  const total = useMemo(() => {
    const tip = Number(tipAmount) || 0;
    return Math.round(((check?.payableTotal ?? 0) + tip) * 100) / 100;
  }, [check?.payableTotal, tipAmount]);

  const [rows, setRows] = useState<PaymentRow[]>([emptyRow()]);
  const [isSplit, setIsSplit] = useState(false);
  const [showSplitModal, setShowSplitModal] = useState(false);
  const [splitPeopleCount, setSplitPeopleCount] = useState('2');

  useEffect(() => {
    // ما نلمسش الأسطر لو المستخدم فعّل التقسيم بالفعل — إعادة توزيع الإجمالي تلقائيًا كل مرة الفاتورة
    // تتغيّر (زي إضافة بقشيش) هتبوّظ التقسيم اليدوي؛ الكاشير يقدر يلغي التقسيم ويرجّع سطر واحد لو حب.
    setRows((prev) => (prev.length === 1 && !isSplit ? [{ ...prev[0], amount: total.toFixed(2) }] : prev));
  }, [total, isSplit]);

  const amountsSum = rows.reduce((sum, r) => sum + (Number(r.amount) || 0), 0);
  const remaining = Math.round((total - amountsSum) * 100) / 100;
  const isBalanced = remaining === 0;

  const methodOptions = enabledMethods.map((c) => ({ value: c.paymentMethodId, label: c.paymentMethodNameAr }));

  const updateRow = (index: number, patch: Partial<PaymentRow>) => {
    setRows((prev) => prev.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  };

  const addRow = () => setRows((prev) => [...prev, emptyRow(remaining > 0 ? remaining.toFixed(2) : '')]);
  const removeRow = (index: number) => setRows((prev) => prev.filter((_, i) => i !== index));

  // مراجعة 2026-09-13 — تقسيم الفاتورة (Split Bill) مُطبَّق كمساعد تحصيل فقط: مفيش نموذج بيانات
  // جديد ولا فاتورة منفصلة لكل شخص — POSInvoice تفضل واحدة، والتقسيم بيوزّع نفس الإجمالي بالتساوي
  // على N سطر دفع (POSPayment) بدل ما الكاشير يحسبها يدويًا. الباقي (لو الإجمالي مش قابل للقسمة
  // بالظبط) بينضاف على آخر سطر عشان المجموع يفضل مطابق تمامًا للإجمالي.
  const applySplit = () => {
    const count = Math.max(2, Math.floor(Number(splitPeopleCount)) || 2);
    const baseShare = Math.floor((total / count) * 100) / 100;
    const shares = Array.from({ length: count }, () => baseShare);
    const remainder = Math.round((total - baseShare * count) * 100) / 100;
    shares[shares.length - 1] = Math.round((shares[shares.length - 1] + remainder) * 100) / 100;
    setRows(shares.map((share) => emptyRow(share.toFixed(2))));
    setIsSplit(true);
    setShowSplitModal(false);
  };

  const cancelSplit = () => {
    setIsSplit(false);
    setRows([emptyRow(total.toFixed(2))]);
  };

  const handleSubmit = async () => {
    if (rows.some((r) => r.paymentMethodId === '' || !r.amount || Number(r.amount) <= 0)) {
      showToast(t('paymentModal.invalidRows'), 'error');
      return;
    }

    try {
      const { id: invoiceId } = await completeMutation.mutateAsync({
        checkId,
        tipAmount: Number(tipAmount) || 0,
        payments: rows.map((r) => ({
          paymentMethodId: Number(r.paymentMethodId),
          amount: Number(r.amount),
          cardTransactionReference: r.cardTransactionReference || undefined,
          amountTendered: r.amountTendered ? Number(r.amountTendered) : undefined
        }))
      });
      showToast(t('paymentModal.success'), 'success');
      navigate(`/pos/invoices/${invoiceId}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading || !check) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0, maxWidth: 760 }}>
      <h2 style={{ margin: 0 }}>{t('paymentModal.title')}</h2>

      <ActionBar secondary={[{ key: 'back', label: t('paymentModal.backToCheck'), onClick: () => navigate(`/pos/checks/${checkId}`) }]} />

      <Card>
        <CardBody>
          <h4 style={{ margin: '0 0 14px', fontSize: 14.5 }}>{t('paymentModal.invoiceSummary')}</h4>
          <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.item')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.quantity')}</th>
                <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.lineTotal')}</th>
              </tr>
            </thead>
            <tbody>
              {check.lines.map((line) => (
                <tr key={line.id}>
                  <td style={{ padding: 8 }}>{line.itemNameAr}</td>
                  <td style={{ padding: 8 }}>{line.quantity}</td>
                  <td style={{ padding: 8 }}>{line.lineTotal.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 26, marginTop: 16, paddingTop: 14, borderTop: '1px dashed var(--color-border)', flexWrap: 'wrap' }}>
            <div style={{ textAlign: 'start' }}>
              <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('checkEdit.subtotal')}</div>
              <div style={{ fontSize: 15, fontWeight: 800, marginTop: 2 }}>{check.subtotal.toFixed(2)}</div>
            </div>
            {check.discountTotal > 0 && (
              <div style={{ textAlign: 'start' }}>
                <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('checkEdit.discountTotal')}</div>
                <div style={{ fontSize: 15, fontWeight: 800, marginTop: 2 }}>−{check.discountTotal.toFixed(2)}</div>
              </div>
            )}
            {check.manualDiscountAmount > 0 && (
              <div style={{ textAlign: 'start' }}>
                <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('checkEdit.manualDiscount')}</div>
                <div style={{ fontSize: 15, fontWeight: 800, marginTop: 2 }}>−{check.manualDiscountAmount.toFixed(2)}</div>
              </div>
            )}
            {check.loyaltyDiscountAmount > 0 && (
              <div style={{ textAlign: 'start' }}>
                <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('checkEdit.loyaltyPointsRedeemed', { points: check.loyaltyPointsToRedeem })}</div>
                <div style={{ fontSize: 15, fontWeight: 800, marginTop: 2 }}>−{check.loyaltyDiscountAmount.toFixed(2)}</div>
              </div>
            )}
            {settings?.serviceChargeEnabled && (
              <div style={{ textAlign: 'start' }}>
                <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('paymentModal.serviceCharge')}</div>
                <div style={{ fontSize: 15, fontWeight: 800, marginTop: 2 }}>{serviceChargeAmount.toFixed(2)}</div>
              </div>
            )}
            {settings?.vatEnabled && (
              <div style={{ textAlign: 'start' }}>
                <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('paymentModal.vat', { rate: settings.vatRate })}</div>
                <div style={{ fontSize: 15, fontWeight: 800, marginTop: 2 }}>{taxAmount.toFixed(2)}</div>
              </div>
            )}
            <div style={{ textAlign: 'start' }}>
              <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)' }}>{t('checkEdit.total')}</div>
              <div style={{ fontSize: 19, fontWeight: 800, marginTop: 2, color: 'var(--color-gold-600)' }}>{total.toFixed(2)}</div>
            </div>
          </div>

          {settings?.tipsEnabled && (
            <div style={{ marginTop: 16 }}>
              <FieldWrapper label={t('paymentModal.tip')}>
                <Input type="number" step="0.01" style={{ width: 140 }} value={tipAmount} onChange={(e) => setTipAmount(e.target.value)} />
              </FieldWrapper>
            </div>
          )}
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14, flexWrap: 'wrap', gap: 8 }}>
            <h4 style={{ margin: 0, fontSize: 14.5 }}>{t('paymentModal.methodsPanelTitle')}</h4>
            {settings?.allowSplitPayment && (
              isSplit
                ? <Button variant="ghost" onClick={cancelSplit}>{t('paymentModal.cancelSplit')}</Button>
                : canSplit && <Button variant="ghost" onClick={() => setShowSplitModal(true)}>{t('paymentModal.splitBill')}</Button>
            )}
          </div>

          {enabledMethods.length === 0 && (
            <div style={{ color: 'var(--color-error)', fontSize: 13, marginBottom: 12 }}>{t('paymentModal.noMethodsConfigured')}</div>
          )}

          {isSplit && (
            <div style={{ fontSize: 12.5, color: 'var(--color-text-muted)', marginBottom: 10 }}>
              {t('paymentModal.splitSummary', { count: rows.length, share: rows[0]?.amount })}
            </div>
          )}

          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', fontSize: 11.5, color: 'var(--color-text-muted)', fontWeight: 700, padding: '6px 8px' }}>{t('paymentModal.method')}</th>
                <th style={{ textAlign: 'start', fontSize: 11.5, color: 'var(--color-text-muted)', fontWeight: 700, padding: '6px 8px' }}>{t('paymentModal.amount')}</th>
                <th style={{ textAlign: 'start', fontSize: 11.5, color: 'var(--color-text-muted)', fontWeight: 700, padding: '6px 8px' }}>{t('paymentModal.amountTendered')}</th>
                <th style={{ textAlign: 'start', fontSize: 11.5, color: 'var(--color-text-muted)', fontWeight: 700, padding: '6px 8px' }}>{t('paymentModal.change')}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => {
                const change = row.amountTendered && Number(row.amountTendered) > 0 && Number(row.amount) > 0
                  ? Math.max(0, Number(row.amountTendered) - Number(row.amount))
                  : null;
                return (
                  <tr key={index}>
                    <td style={{ padding: 6 }}>
                      <SearchableSelect value={row.paymentMethodId} onChange={(v) => updateRow(index, { paymentMethodId: v === '' ? '' : Number(v) })} options={methodOptions} />
                    </td>
                    <td style={{ padding: 6 }}>
                      <Input type="number" step="0.01" value={row.amount} onChange={(e) => updateRow(index, { amount: e.target.value })} />
                    </td>
                    <td style={{ padding: 6 }}>
                      <Input type="number" step="0.01" value={row.amountTendered} onChange={(e) => updateRow(index, { amountTendered: e.target.value })} />
                    </td>
                    <td style={{ padding: 6, fontSize: 12.5, color: 'var(--color-text-muted)', whiteSpace: 'nowrap' }}>{change != null ? change.toFixed(2) : '—'}</td>
                    <td style={{ padding: 6 }}>
                      {settings?.allowSplitPayment && rows.length > 1 && (
                        <Button variant="ghost" onClick={() => removeRow(index)}>{t('common.remove')}</Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {settings?.allowSplitPayment && (
            <button
              type="button" onClick={addRow}
              style={{ marginTop: 10, fontSize: 12.5, fontWeight: 800, background: 'transparent', border: '1px dashed var(--color-text-muted)', borderRadius: 7, padding: '8px 14px', cursor: 'pointer', color: 'var(--color-text-muted)' }}
            >
              {t('paymentModal.addPayment')}
            </button>
          )}

          <div
            style={{
              display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14, padding: '12px 16px', borderRadius: 9, fontSize: 13,
              background: isBalanced ? 'var(--color-success-bg)' : 'var(--color-error-bg)', color: isBalanced ? 'var(--color-success)' : 'var(--color-error)'
            }}
          >
            <span>{t('paymentModal.paidVsRequired', { paid: amountsSum.toFixed(2), required: total.toFixed(2) })}</span>
            <span style={{ fontWeight: 800 }}>{isBalanced ? t('paymentModal.balanced') : t('paymentModal.remaining') + ': ' + remaining.toFixed(2)}</span>
          </div>

          <button
            onClick={handleSubmit}
            disabled={!isBalanced || enabledMethods.length === 0}
            style={{
              width: '100%', marginTop: 14, padding: 14, fontSize: 14.5, fontWeight: 800, borderRadius: 8, border: 'none', cursor: (!isBalanced || enabledMethods.length === 0) ? 'not-allowed' : 'pointer',
              opacity: (!isBalanced || enabledMethods.length === 0) ? 0.5 : 1, background: 'var(--color-gold-500)', color: '#fff'
            }}
          >
            {t('paymentModal.completePayment')}
          </button>
        </CardBody>
      </Card>

      {showSplitModal && (
        <div
          role="dialog" aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setShowSplitModal(false)}
        >
          <div style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-lg)', width: '90%', maxWidth: 380 }} onClick={(e) => e.stopPropagation()}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
              {t('paymentModal.splitBill')}
            </div>
            <div style={{ padding: '16px 24px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <FieldWrapper label={t('paymentModal.splitPeopleCount')}>
                <Input type="number" min={2} step="1" value={splitPeopleCount} onChange={(e) => setSplitPeopleCount(e.target.value)} />
              </FieldWrapper>
              {Number(splitPeopleCount) >= 2 && (
                <div style={{ fontSize: 12.5, color: 'var(--color-text-muted)' }}>
                  {t('paymentModal.splitPreview', { count: Math.floor(Number(splitPeopleCount)), share: (total / Math.max(2, Math.floor(Number(splitPeopleCount)) || 2)).toFixed(2) })}
                </div>
              )}
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <Button variant="ghost" onClick={() => setShowSplitModal(false)}>{t('common.cancel')}</Button>
              <Button variant="primary" disabled={!(Number(splitPeopleCount) >= 2)} onClick={applySplit}>{t('paymentModal.splitApply')}</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
