import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useInventorySettings, useUpdateInventorySettings } from './api';

/** /settings/inventory-settings — screen #23. One row per company (rule 36's default of 90 days). */
export function GeneralInventorySettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: settings, isLoading } = useInventorySettings();
  const updateMutation = useUpdateInventorySettings();

  const [slowMovingThresholdDays, setSlowMovingThresholdDays] = useState(90);

  useEffect(() => {
    if (settings) {
      setSlowMovingThresholdDays(settings.slowMovingThresholdDays);
    }
  }, [settings]);

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({ slowMovingThresholdDays });
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
      <h2 style={{ margin: 0 }}>{t('inventorySettings.generalTitle')}</h2>
      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }} />
      <Card>
        <CardBody>
          <FieldWrapper label={t('inventorySettings.slowMovingThresholdDays')}>
            <Input
              type="number"
              min={1}
              style={{ width: 140 }}
              value={slowMovingThresholdDays}
              onChange={(e) => setSlowMovingThresholdDays(Number(e.target.value))}
            />
          </FieldWrapper>
        </CardBody>
      </Card>
    </div>
  );
}
