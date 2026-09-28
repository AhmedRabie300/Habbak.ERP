import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useBranchesList } from '../../organization/branches/api';
import { useHrSettings, useUpdateHrSettings, type LeaveDayCountingMode } from './api';

const MODES: LeaveDayCountingMode[] = ['Calendar', 'WorkingDays'];

/** /hr/settings — screen HR_SETTINGS. أول Frontend لهذه الشاشة (Backend موجود من Phase 1.5.0) —
 * بُنيت هنا لأن Phase 3 محتاجة الدروب داون leaveDayCountingMode عليها. */
export function HrSettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);

  const { data: settings, isLoading } = useHrSettings();
  const { data: branches } = useBranchesList();
  const branchOptions = useMemo(
    () => [{ value: '', label: t('hr.hrSettings.noBranch') }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))],
    [branches, t]
  );

  const [defaultProbationDays, setDefaultProbationDays] = useState(90);
  const [defaultBranchId, setDefaultBranchId] = useState<number | ''>('');
  const [requireNationalIdForActivation, setRequireNationalIdForActivation] = useState(true);
  const [leaveDayCountingMode, setLeaveDayCountingMode] = useState<LeaveDayCountingMode>('Calendar');

  useEffect(() => {
    if (!settings) return;
    setDefaultProbationDays(settings.defaultProbationDays);
    setDefaultBranchId(settings.defaultBranchId ?? '');
    setRequireNationalIdForActivation(settings.requireNationalIdForActivation);
    setLeaveDayCountingMode(settings.leaveDayCountingMode);
  }, [settings]);

  const update = useUpdateHrSettings();

  const handleSave = async () => {
    try {
      await update.mutateAsync({
        defaultProbationDays,
        defaultBranchId: defaultBranchId === '' ? null : Number(defaultBranchId),
        requireNationalIdForActivation,
        leaveDayCountingMode
      });
      showToast(t('hr.hrSettings.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{t('hr.hrSettings.title')}</h2>

      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }} secondary={[]} />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.hrSettings.defaultProbationDays')}>
              <Input type="number" value={defaultProbationDays} onChange={(e) => setDefaultProbationDays(Number(e.target.value))} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.hrSettings.defaultBranch')}>
              <SearchableSelect value={defaultBranchId} onChange={(v) => setDefaultBranchId(v === '' ? '' : Number(v))} options={branchOptions} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.hrSettings.requireNationalIdForActivation')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={requireNationalIdForActivation} onChange={(e) => setRequireNationalIdForActivation(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.hrSettings.leaveDayCountingMode')}>
              <SearchableSelect
                value={leaveDayCountingMode}
                onChange={(v) => setLeaveDayCountingMode(v as LeaveDayCountingMode)}
                options={MODES.map((m) => ({ value: m, label: t(`hr.hrSettings.leaveDayCountingModes.${m}`) }))}
                style={{ minWidth: 260 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
