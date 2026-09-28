import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../ui-kit/Card';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useBlendTypesList, useGenerateBlendTicket } from './api';
import type { GenerateBlendTicketResult } from './api';

interface CompositionLine {
  blendTypeId: number;
  nameAr: string;
  pricePerGram: number;
  weightGrams: number;
}

/** /pos/blend-consultation — مراجعة 2026-09-13، بند 2.1: شاشة استشاري التصنيع (تابلت). العميل
 * يختار أكتر من نوع بن ووزن كل نوع بالجرام، والشاشة تحسب الوزن الإجمالي وسعر الكيلو المكافئ
 * والإجمالي لحظيًا، ثم تُصدر تذكرة QR ذاتية الاحتواء تُقرأ لاحقًا من شاشة البيع ("مسح تذكرة QR").
 * الشاشة نفسها مش محتاجة أي منطق Offline خاص بها (00-Project-Overview.md قسم 14.3) — التذكرة نفسها
 * (لو تحوّلت لـQR فعلي) هي اللي بتحمل الاستقلالية، مش الشاشة.</summary> */
export function BlendConsultationPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: blendTypes } = useBlendTypesList();
  const generateMutation = useGenerateBlendTicket();

  const [composition, setComposition] = useState<CompositionLine[]>([]);
  const [generated, setGenerated] = useState<GenerateBlendTicketResult | null>(null);

  const activeBlendTypes = (blendTypes ?? []).filter((b) => b.isActive);

  const handleAddBlendType = (blendTypeId: number) => {
    const blendType = activeBlendTypes.find((b) => b.id === blendTypeId);
    if (!blendType) return;
    setGenerated(null);
    setComposition((prev) => {
      const existing = prev.find((l) => l.blendTypeId === blendTypeId);
      if (existing) {
        return prev.map((l) => (l.blendTypeId === blendTypeId ? { ...l, weightGrams: l.weightGrams + 50 } : l));
      }
      return [...prev, { blendTypeId, nameAr: blendType.nameAr, pricePerGram: blendType.pricePerGram, weightGrams: 50 }];
    });
  };

  const updateWeight = (blendTypeId: number, weightGrams: number) => {
    setGenerated(null);
    setComposition((prev) => prev.map((l) => (l.blendTypeId === blendTypeId ? { ...l, weightGrams } : l)));
  };

  const removeLine = (blendTypeId: number) => {
    setGenerated(null);
    setComposition((prev) => prev.filter((l) => l.blendTypeId !== blendTypeId));
  };

  const totals = useMemo(() => {
    const totalWeightGrams = composition.reduce((sum, l) => sum + (l.weightGrams || 0), 0);
    const totalPrice = composition.reduce((sum, l) => sum + (l.weightGrams || 0) * l.pricePerGram, 0);
    const pricePerKg = totalWeightGrams > 0 ? (totalPrice / totalWeightGrams) * 1000 : 0;
    return { totalWeightGrams, totalPrice, pricePerKg };
  }, [composition]);

  const handleGenerate = async () => {
    const validLines = composition.filter((l) => l.weightGrams > 0);
    if (validLines.length === 0) {
      showToast(t('blendConsultation.invalidForm'), 'error');
      return;
    }
    try {
      const result = await generateMutation.mutateAsync(
        validLines.map((l) => ({ blendTypeId: l.blendTypeId, weightGrams: l.weightGrams }))
      );
      setGenerated(result);
      showToast(t('blendConsultation.generateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <h2 style={{ margin: 0 }}>{t('blendConsultation.title')}</h2>
      <div style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('blendConsultation.description')}</div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16, minWidth: 0, alignItems: 'start' }}>
        {/* blend-types grid */}
        <div style={{ flex: '2 1 380px', background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)', padding: '18px 20px', minWidth: 0 }}>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>{t('blendConsultation.availableBlends')}</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: 12 }}>
            {activeBlendTypes.map((blendType) => (
              <div
                key={blendType.id}
                onClick={() => handleAddBlendType(blendType.id)}
                style={{
                  textAlign: 'start', border: '1px solid var(--color-border)', borderRadius: 10, padding: 14, background: '#fff',
                  cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 14, minHeight: 90
                }}
              >
                <div>
                  <div style={{ fontWeight: 800, fontSize: 14 }}>{blendType.nameAr}</div>
                  <div style={{ fontSize: 11, color: 'var(--color-text-muted)', marginTop: 2 }}>{blendType.code}</div>
                </div>
                <div style={{ fontWeight: 800, fontSize: 14, color: 'var(--color-gold-600)' }}>
                  {blendType.pricePerGram.toFixed(2)} {t('blendConsultation.perGram')}
                </div>
              </div>
            ))}
            {activeBlendTypes.length === 0 && (
              <div style={{ color: 'var(--color-text-muted)', fontSize: 13, padding: 20 }}>{t('blendConsultation.noBlendTypes')}</div>
            )}
          </div>
        </div>

        {/* composition cart */}
        <aside style={{ flex: '1 1 300px', background: 'var(--color-navy-900)', color: '#fff', borderRadius: 'var(--radius-lg)', display: 'flex', flexDirection: 'column', padding: '18px 16px', minWidth: 0 }}>
          <h3 style={{ margin: '0 0 12px', fontSize: 15 }}>{t('blendConsultation.compositionTitle')}</h3>

          <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 12, minHeight: 120, maxHeight: 320 }}>
            {composition.length === 0 && (
              <div style={{ color: '#7C90A3', fontSize: 12.5, textAlign: 'center', padding: '24px 0' }}>{t('blendConsultation.compositionEmpty')}</div>
            )}
            {composition.map((line) => (
              <div key={line.blendTypeId} style={{ background: 'var(--color-navy-700)', borderRadius: 8, padding: '10px 12px', display: 'flex', flexDirection: 'column', gap: 8, fontSize: 13 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                  <div style={{ fontWeight: 700 }}>{line.nameAr}</div>
                  <button onClick={() => removeLine(line.blendTypeId)} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: '#A5937E', fontSize: 13, cursor: 'pointer' }}>✕</button>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <input
                      type="number" min={0} step="5"
                      value={line.weightGrams}
                      onChange={(e) => updateWeight(line.blendTypeId, Number(e.target.value))}
                      style={{ width: 70, background: '#2C2019', color: '#fff', border: '1px solid #40312A', borderRadius: 6, padding: '4px 6px', fontSize: 12 }}
                    />
                    <span style={{ fontSize: 11, color: '#92A5B7' }}>{t('blendConsultation.grams')}</span>
                  </div>
                  <span style={{ color: 'var(--color-gold-500)', fontWeight: 800, whiteSpace: 'nowrap' }}>
                    {(line.weightGrams * line.pricePerGram).toFixed(2)}
                  </span>
                </div>
              </div>
            ))}
          </div>

          <div style={{ borderTop: '1px solid #2C3F58', paddingTop: 12, fontSize: 13.5 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', color: '#B7C6D4' }}>
              <span>{t('blendConsultation.totalWeight')}</span><span>{totals.totalWeightGrams.toFixed(0)} {t('blendConsultation.grams')}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', color: '#B7C6D4' }}>
              <span>{t('blendConsultation.equivalentKgPrice')}</span><span>{totals.pricePerKg.toFixed(2)}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: '#fff', fontWeight: 800, fontSize: 17, borderTop: '1px dashed #2C3F58', marginTop: 6, paddingTop: 10 }}>
              <span>{t('blendConsultation.totalPrice')}</span><span>{totals.totalPrice.toFixed(2)}</span>
            </div>
          </div>

          {composition.length > 0 && (
            <button
              onClick={handleGenerate}
              style={{
                marginTop: 14, fontWeight: 800, fontSize: 14.5, padding: 13, borderRadius: 8,
                background: 'var(--color-gold-500)', border: '1px solid var(--color-gold-500)', color: '#fff', cursor: 'pointer'
              }}
            >
              {t('blendConsultation.generateTicket')}
            </button>
          )}
        </aside>
      </div>

      {generated && (
        <Card>
          <CardBody>
            <div style={{ fontWeight: 700, marginBottom: 8 }}>{t('blendConsultation.generatedTitle')}</div>
            <div style={{ fontSize: 13, marginBottom: 8 }}>{t('blendConsultation.ticketId')}: {generated.id}</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
              <span style={{ fontSize: 13 }}>{t('blendConsultation.idempotencyKey')}:</span>
              <code style={{ background: 'var(--color-surface-2)', padding: '4px 8px', borderRadius: 6, fontSize: 13 }}>{generated.idempotencyKey}</code>
            </div>
            <div style={{ fontSize: 13 }}>{t('blendConsultation.totalWeight')}: {generated.totalWeightGrams.toFixed(0)} {t('blendConsultation.grams')} — {t('blendConsultation.totalPrice')}: {generated.totalPrice.toFixed(2)}</div>
            <div style={{ fontSize: 12, color: 'var(--color-text-muted)', marginTop: 8 }}>{t('blendConsultation.redeemHint')}</div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
