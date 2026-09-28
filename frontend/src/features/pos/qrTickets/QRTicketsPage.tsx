import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useItemsList } from '../../inventory/items/api';
import { useGenerateQRTicket } from './api';
import type { QRTicketItemInput } from './types';

interface DraftLine {
  itemId: number | '';
  quantity: number;
  unitPrice: number | '';
}

/** /pos/qr-tickets — قاعدة 18 / 00-Project-Overview.md قسم 14.3. مفيش موديول "استشاري تصنيع"
 * حقيقي لسه بيولّد التذاكر دي فعليًا — هذه الشاشة بديل إداري/اختباري لإصدار تذكرة ذاتية الاحتواء
 * (Idempotency Key) يدويًا؛ استخدام التذكرة (مسح/Redeem) بيحصل من شاشة البيع نفسها
 * (CheckEditPage → "مسح تذكرة QR"). */
export function QRTicketsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: items } = useItemsList();

  const [lines, setLines] = useState<DraftLine[]>([{ itemId: '', quantity: 1, unitPrice: 0 }]);
  const [generated, setGenerated] = useState<{ id: number; idempotencyKey: string } | null>(null);

  const generateMutation = useGenerateQRTicket();

  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));

  const updateLine = (index: number, patch: Partial<DraftLine>) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  };

  const addLine = () => setLines((prev) => [...prev, { itemId: '', quantity: 1, unitPrice: 0 }]);
  const removeLine = (index: number) => setLines((prev) => prev.filter((_, i) => i !== index));

  const handleGenerate = async () => {
    const validLines = lines.filter((l) => l.itemId !== '' && l.quantity > 0);
    if (validLines.length === 0) {
      showToast(t('qrTickets.invalidForm'), 'error');
      return;
    }
    const payload: QRTicketItemInput[] = validLines.map((l) => ({
      itemId: l.itemId as number, quantity: l.quantity, unitPrice: l.unitPrice === '' ? 0 : l.unitPrice
    }));
    try {
      const result = await generateMutation.mutateAsync(payload);
      setGenerated(result);
      showToast(t('qrTickets.generateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <h2 style={{ margin: 0 }}>{t('qrTickets.title')}</h2>

      <ActionBar primary={{ key: 'generate', label: t('qrTickets.generate'), onClick: handleGenerate }} />

      <Card>
        <CardBody>
          <div style={{ fontSize: 13, color: 'var(--color-text-muted)', marginBottom: 12 }}>{t('qrTickets.description')}</div>
          {lines.map((line, index) => (
            <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
              <SearchableSelect style={{ minWidth: 220 }} value={line.itemId} onChange={(v) => updateLine(index, { itemId: v === '' ? '' : Number(v) })} options={itemOptions} />
              <FieldWrapper label={t('checkEdit.quantity')}>
                <Input type="number" step="0.0001" min={0} style={{ width: 90 }} value={line.quantity} onChange={(e) => updateLine(index, { quantity: Number(e.target.value) })} />
              </FieldWrapper>
              <FieldWrapper label={t('checkEdit.unitPrice')}>
                <Input type="number" step="0.01" min={0} style={{ width: 100 }} value={line.unitPrice} onChange={(e) => updateLine(index, { unitPrice: e.target.value === '' ? '' : Number(e.target.value) })} />
              </FieldWrapper>
              <button onClick={() => removeLine(index)} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: 'var(--color-error)', fontSize: 15, cursor: 'pointer', marginTop: 18 }}>✕</button>
            </div>
          ))}
          <Button variant="ghost" onClick={addLine}>{t('deliveryOrders.addItem')}</Button>
        </CardBody>
      </Card>

      {generated && (
        <Card>
          <CardBody>
            <div style={{ fontWeight: 700, marginBottom: 8 }}>{t('qrTickets.generatedTitle')}</div>
            <div style={{ fontSize: 13, marginBottom: 8 }}>{t('qrTickets.ticketId')}: {generated.id}</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 13 }}>{t('qrTickets.idempotencyKey')}:</span>
              <code style={{ background: 'var(--color-surface-2)', padding: '4px 8px', borderRadius: 6, fontSize: 13 }}>{generated.idempotencyKey}</code>
            </div>
            <div style={{ fontSize: 12, color: 'var(--color-text-muted)', marginTop: 8 }}>{t('qrTickets.redeemHint')}</div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
