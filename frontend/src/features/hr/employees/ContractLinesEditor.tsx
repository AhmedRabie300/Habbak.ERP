import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import type { ContractLineInput, ContractLineType } from './types';

const LINE_TYPES: ContractLineType[] = ['Earning', 'Deduction'];

/** Add/Remove-row line editor shared by ContractsTab and ContractStep (Hiring Wizard) —
 * Docs/Implementation/Phase-3C-Research.md §2: plain useState array, not react-hook-form's
 * useFieldArray, to stay consistent with both forms it's used inside. */
export function ContractLinesEditor({ lines, onChange }: { lines: ContractLineInput[]; onChange: (lines: ContractLineInput[]) => void }) {
  const { t } = useTranslation();

  const addLine = () =>
    onChange([...lines, { nameAr: '', nameEn: '', amount: 0, type: 'Earning', isTaxable: true, isInsurable: false, order: lines.length }]);
  const removeLine = (index: number) => onChange(lines.filter((_, i) => i !== index));
  const updateLine = (index: number, patch: Partial<ContractLineInput>) =>
    onChange(lines.map((line, i) => (i === index ? { ...line, ...patch } : line)));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <strong style={{ fontSize: 13 }}>{t('hr.employees.contracts.lines.title')}</strong>
        <Button variant="secondary" size="sm" onClick={addLine}>{t('hr.employees.contracts.lines.add')}</Button>
      </div>
      {lines.length === 0 && <p style={{ fontSize: 13, color: 'var(--color-text-muted)', margin: 0 }}>{t('common.noData')}</p>}
      {lines.map((line, index) => (
        <div key={index} style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <FieldWrapper label={t('hr.employees.contracts.lines.nameAr')}>
            <Input value={line.nameAr} onChange={(e) => updateLine(index, { nameAr: e.target.value })} style={{ width: 140 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.lines.nameEn')}>
            <Input value={line.nameEn} onChange={(e) => updateLine(index, { nameEn: e.target.value })} style={{ width: 140 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.lines.amount')}>
            <Input type="number" value={line.amount} onChange={(e) => updateLine(index, { amount: Number(e.target.value) })} style={{ width: 100 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.lines.type')}>
            <SearchableSelect
              value={line.type}
              onChange={(v) => updateLine(index, { type: v as ContractLineType })}
              options={LINE_TYPES.map((v) => ({ value: v, label: t(`hr.employees.contracts.lines.types.${v}`) }))}
              style={{ minWidth: 120 }}
            />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.lines.isTaxable')}>
            <input type="checkbox" checked={line.isTaxable} onChange={(e) => updateLine(index, { isTaxable: e.target.checked })} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.lines.isInsurable')}>
            <input type="checkbox" checked={line.isInsurable} onChange={(e) => updateLine(index, { isInsurable: e.target.checked })} />
          </FieldWrapper>
          <Button variant="secondary" size="sm" onClick={() => removeLine(index)}>{t('common.remove')}</Button>
        </div>
      ))}
    </div>
  );
}
