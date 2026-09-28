import { useTranslation } from 'react-i18next';
import { Input } from '../../../ui-kit/Field';
import type { DenominationCountInput } from './types';

/** الفئات النقدية المصرية القياسية — ثابتة لكل الشاشة (مفيش شاشة تعريف فئات منفصلة في هذه
 * المرحلة). صف واحد لكل فئة، الإجمالي بيتحدث لحظيًا مع كل تغيير (متطلب الشاشة #1/#3/#14). */
const DENOMINATIONS = [200, 100, 50, 20, 10, 5, 1];

interface DenominationGridProps {
  counts: Record<number, number>;
  onChange: (denominationValue: number, count: number) => void;
  disabled?: boolean;
}

export function denominationCountsToInput(counts: Record<number, number>): DenominationCountInput[] {
  return Object.entries(counts)
    .map(([value, count]) => ({ denominationValue: Number(value), count }))
    .filter((c) => c.count > 0);
}

export function DenominationGrid({ counts, onChange, disabled }: DenominationGridProps) {
  const { t } = useTranslation();
  const total = DENOMINATIONS.reduce((sum, d) => sum + d * (counts[d] ?? 0), 0);

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
        <thead>
          <tr>
            <th style={{ textAlign: 'start', padding: 8 }}>{t('shifts.denominationValue')}</th>
            <th style={{ textAlign: 'start', padding: 8 }}>{t('shifts.count')}</th>
            <th style={{ textAlign: 'start', padding: 8 }}>{t('shifts.subtotal')}</th>
          </tr>
        </thead>
        <tbody>
          {DENOMINATIONS.map((d) => (
            <tr key={d}>
              <td style={{ padding: 8 }}>{d.toFixed(2)}</td>
              <td style={{ padding: 8 }}>
                <Input
                  type="number" min={0} step={1} disabled={disabled} style={{ width: 100 }}
                  value={counts[d] ?? 0}
                  onChange={(e) => onChange(d, Number(e.target.value))}
                />
              </td>
              <td style={{ padding: 8 }}>{(d * (counts[d] ?? 0)).toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td style={{ padding: 8, fontWeight: 700 }}>{t('shifts.total')}</td>
            <td />
            <td style={{ padding: 8, fontWeight: 700 }}>{total.toFixed(2)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
