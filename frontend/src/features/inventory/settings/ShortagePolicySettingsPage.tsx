import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper } from '../../../ui-kit/Field';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useShortagePolicy, useUpdateShortagePolicy } from './api';

/** /settings/shortage-policy — screen #22 (02-Module-Inventory-Manufacturing.md, section 5). One
 * row per company (rule 33: OverrideShortage is a single unified permission gated by this policy,
 * covering both the negative-balance override, rule 1, and the branch-request-limit override, rule 4). */
export function ShortagePolicySettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: policy, isLoading } = useShortagePolicy();
  const updateMutation = useUpdateShortagePolicy();

  const [allowOverride, setAllowOverride] = useState(false);
  const [requiresApproval, setRequiresApproval] = useState(false);

  useEffect(() => {
    if (policy) {
      setAllowOverride(policy.allowOverrideOnShortage);
      setRequiresApproval(policy.requiresApprovalForOverride);
    }
  }, [policy]);

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({ allowOverrideOnShortage: allowOverride, requiresApprovalForOverride: requiresApproval });
      showToast(t('inventorySettings.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('inventorySettings.shortagePolicyTitle')}</h2>
      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }} />
      <Card>
        <CardBody>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <FieldWrapper label={t('inventorySettings.allowOverrideOnShortage')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={allowOverride} onChange={(e) => setAllowOverride(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('inventorySettings.requiresApprovalForOverride')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={requiresApproval} onChange={(e) => setRequiresApproval(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
