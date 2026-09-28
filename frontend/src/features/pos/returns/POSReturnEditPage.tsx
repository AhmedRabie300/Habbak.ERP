import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useShiftsList } from '../shifts/api';
import { usePOSInvoice, usePOSInvoicesList } from '../invoices/api';
import { useCreatePOSReturn, usePOSReturn } from './api';
import type { POSReturnLineInput } from './types';
import { todayLocal } from '../../../lib/date';

/** /pos/returns/:id — screen #6 (05-Module-POS-Shifts.md، قاعدة 16). لا يوجد Draft/Update — يتسجل
 * ويترحّل في نفس الفعل (زي POSInvoice)، فالشاشة دي Create-only + عرض للقراءة فقط بعد الإنشاء. */
export function POSReturnEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const returnId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: posReturn, isLoading } = usePOSReturn(returnId);

  const { data: terminals } = usePOSTerminalsList();
  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');
  const { data: shifts } = useShiftsList(posTerminalId === '' ? undefined : posTerminalId, undefined);
  const [shiftId, setShiftId] = useState<number | ''>('');
  const { data: invoices } = usePOSInvoicesList(posTerminalId === '' ? undefined : posTerminalId);
  const [sourceInvoiceId, setSourceInvoiceId] = useState<number | ''>('');
  const { data: sourceInvoice } = usePOSInvoice(sourceInvoiceId === '' ? undefined : sourceInvoiceId);

  const [returnDate, setReturnDate] = useState(todayLocal());
  const [reason, setReason] = useState('');
  const [lines, setLines] = useState<POSReturnLineInput[]>([]);

  useEffect(() => {
    if (terminals && terminals.length > 0 && posTerminalId === '') {
      setPosTerminalId(terminals[0].id);
    }
  }, [terminals, posTerminalId]);

  useEffect(() => {
    if (shifts && shifts.length > 0) {
      const openShift = shifts.find((s) => s.status === 'Open');
      setShiftId(openShift?.id ?? shifts[0].id);
    } else {
      setShiftId('');
    }
  }, [shifts]);

  useEffect(() => {
    if (sourceInvoice && isNew) {
      setLines(sourceInvoice.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice })));
    }
  }, [sourceInvoice, isNew]);

  const createMutation = useCreatePOSReturn();

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));
  const shiftOptions = (shifts ?? []).map((s) => ({ value: s.id, label: `#${s.id} — ${t(`status.${s.status}`, s.status)}` }));
  const postedInvoiceOptions = (invoices ?? []).filter((i) => i.status === 'Posted').map((i) => ({ value: i.id, label: `${i.invoiceNumber} — ${i.total.toFixed(2)}` }));

  const updateLineQuantity = (index: number, quantity: number) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, quantity } : l)));
  };

  const handleSave = async () => {
    if (shiftId === '' || sourceInvoiceId === '' || !reason.trim() || lines.some((l) => l.quantity <= 0)) {
      showToast(t('posReturns.invalidForm'), 'error');
      return;
    }
    try {
      const { id: newId } = await createMutation.mutateAsync({
        shiftId, sourceInvoiceId, returnDate, reason: reason.trim(), lines: lines.filter((l) => l.quantity > 0)
      });
      showToast(t('posReturns.createSuccess'), 'success');
      navigate(`/pos/returns/${newId}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  if (!isNew && posReturn) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h2 style={{ margin: 0 }}>{posReturn.returnNumber}</h2>
          <StatusBadge status={posReturn.status} />
        </div>

        <ActionBar secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/pos/returns') }]} />

        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', fontSize: 13, marginBottom: 16 }}>
              <div>{t('posReturns.sourceInvoice')}: {posReturn.sourceInvoiceNumber}</div>
              <div>{t('posReturns.returnDate')}: {posReturn.returnDate}</div>
              <div>{t('posReturns.reason')}: {posReturn.reason}</div>
            </div>

            <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.item')}</th>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.quantity')}</th>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.unitPrice')}</th>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.lineTotal')}</th>
                </tr>
              </thead>
              <tbody>
                {posReturn.lines.map((line) => (
                  <tr key={line.itemId}>
                    <td style={{ padding: 8 }}>{line.itemCode} — {line.itemNameAr}</td>
                    <td style={{ padding: 8 }}>{line.quantity}</td>
                    <td style={{ padding: 8 }}>{line.unitPrice.toFixed(2)}</td>
                    <td style={{ padding: 8 }}>{line.lineTotal.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div style={{ textAlign: 'end', marginTop: 16, fontWeight: 800, fontSize: 16 }}>
              {t('checkEdit.total')}: {posReturn.total.toFixed(2)}
            </div>
          </CardBody>
        </Card>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <h2 style={{ margin: 0 }}>{t('posReturns.addReturn')}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('posReturns.save'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/pos/returns') }]}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('posReturns.terminal')}>
              <SearchableSelect style={{ minWidth: 200 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('posReturns.shift')}>
              <SearchableSelect style={{ minWidth: 200 }} value={shiftId} onChange={(v) => setShiftId(v === '' ? '' : Number(v))} options={shiftOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('posReturns.sourceInvoice')}>
              <SearchableSelect style={{ minWidth: 220 }} value={sourceInvoiceId} onChange={(v) => setSourceInvoiceId(v === '' ? '' : Number(v))} options={postedInvoiceOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('posReturns.returnDate')}>
              <Input type="date" style={{ width: 160 }} value={returnDate} onChange={(e) => setReturnDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('posReturns.reason')}>
              <Input style={{ minWidth: 240 }} value={reason} onChange={(e) => setReason(e.target.value)} />
            </FieldWrapper>
          </div>

          {sourceInvoice && (
            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.item')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('posReturns.originalQuantity')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('posReturns.returnQuantity')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('checkEdit.unitPrice')}</th>
                  </tr>
                </thead>
                <tbody>
                  {sourceInvoice.lines.map((invoiceLine, index) => (
                    <tr key={invoiceLine.itemId}>
                      <td style={{ padding: 8 }}>{invoiceLine.itemCode} — {invoiceLine.itemNameAr}</td>
                      <td style={{ padding: 8 }}>{invoiceLine.quantity}</td>
                      <td style={{ padding: 8 }}>
                        <Input
                          type="number" step="0.0001" min={0} max={invoiceLine.quantity} style={{ width: 90 }}
                          value={lines[index]?.quantity ?? 0}
                          onChange={(e) => updateLineQuantity(index, Number(e.target.value))}
                        />
                      </td>
                      <td style={{ padding: 8 }}>{invoiceLine.unitPrice.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
