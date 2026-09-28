import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { FieldWrapper } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useAppStore } from '../../../store/appStore';
import { useBranchesList } from './api';

interface BranchFieldProps {
  value: number | undefined;
  onChange: (value: number | undefined) => void;
  label: string;
  /** True only for a brand-new document: auto-fills and locks the field when the current
   * session is pinned to one branch. An existing document keeps showing/editing its own
   * already-stored branch instead (Bug-002). */
  autoSelect: boolean;
  disabled?: boolean;
}

/** Branch picker shared by Voucher/TreasuryTransfer/JournalEntry/Custody (Bug-002): a session
 * already pinned to one branch never has to pick one; a company-wide session always does, from
 * every company branch (reusing useBranchesList as-is). */
export function BranchField({ value, onChange, label, autoSelect, disabled }: BranchFieldProps) {
  const { t } = useTranslation();
  const sessionBranchId = useAppStore((s) => s.branchId);
  const { data: branches } = useBranchesList();

  useEffect(() => {
    if (autoSelect && sessionBranchId != null && value == null) onChange(sessionBranchId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoSelect, sessionBranchId]);

  const locked = autoSelect && sessionBranchId != null;

  return (
    <FieldWrapper label={label}>
      <SearchableSelect
        disabled={disabled || locked}
        value={value ?? ''}
        onChange={(v) => onChange(v === '' ? undefined : Number(v))}
        options={[
          { value: '', label: t('common.selectBranch') },
          ...(branches ?? []).map((b) => ({ value: b.id, label: `${b.code} - ${b.nameAr}` }))
        ]}
      />
    </FieldWrapper>
  );
}
