import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useLoyaltyProgramSettings, useUpdateLoyaltyProgramSettings } from './api';

/** /sales/settings/loyalty-program — screen #4 (04-Module-Sales.md, section 5). One row per
 * company: كام جنيه إنفاق = نقطة واحدة، وقيمة النقطة عند الاستبدال. */
export function LoyaltyProgramSettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: settings, isLoading } = useLoyaltyProgramSettings();
  const updateMutation = useUpdateLoyaltyProgramSettings();

  const [pointsEarnRate, setPointsEarnRate] = useState('1');
  const [pointsRedemptionValue, setPointsRedemptionValue] = useState('1');

  useEffect(() => {
    if (settings) {
      setPointsEarnRate(settings.pointsEarnRate.toString());
      setPointsRedemptionValue(settings.pointsRedemptionValue.toString());
    }
  }, [settings]);

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({
        pointsEarnRate: Number(pointsEarnRate || 0),
        pointsRedemptionValue: Number(pointsRedemptionValue || 0)
      });
      showToast(t('loyaltyProgramSettings.saveSuccess'), 'success');
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
      <h2 style={{ margin: 0 }}>{t('loyaltyProgramSettings.title')}</h2>
      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }} />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('loyaltyProgramSettings.pointsEarnRate')}>
              <Input type="number" step="0.01" style={{ width: 140 }} value={pointsEarnRate} onChange={(e) => setPointsEarnRate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('loyaltyProgramSettings.pointsRedemptionValue')}>
              <Input type="number" step="0.01" style={{ width: 140 }} value={pointsRedemptionValue} onChange={(e) => setPointsRedemptionValue(e.target.value)} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
