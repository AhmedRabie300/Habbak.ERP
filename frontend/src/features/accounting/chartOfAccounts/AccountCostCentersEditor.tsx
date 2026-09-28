import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import {
  useAccountDimensionLinks,
  useCreateAccountDimensionLink,
  useDeleteAccountDimensionLink,
  useDimensionsList
} from '../dimensions/api';

const SLOT_COUNT = 5;

interface Slot {
  linkId?: number;
  dimensionId: number | '';
  order: number;
  mandatory: boolean;
}

const emptySlot = (order: number): Slot => ({ dimensionId: '', order, mandatory: false });

/** Rule 24: max 5 cost centers per account — fixed 5-slot editor, not a free-form add/remove list. */
export function AccountCostCentersEditor({ accountId }: { accountId: number }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: dimensions } = useDimensionsList();
  const { data: links } = useAccountDimensionLinks(accountId);
  const createLink = useCreateAccountDimensionLink();
  const deleteLink = useDeleteAccountDimensionLink();

  const [slots, setSlots] = useState<Slot[]>(Array.from({ length: SLOT_COUNT }, (_, i) => emptySlot(i + 1)));
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!links) return;
    const sorted = [...links].sort((a, b) => a.displayOrder - b.displayOrder);
    const next = Array.from({ length: SLOT_COUNT }, (_, i) => {
      const link = sorted[i];
      return link
        ? { linkId: link.id, dimensionId: link.costCenterDimensionId, order: link.displayOrder, mandatory: link.isMandatory }
        : emptySlot(i + 1);
    });
    setSlots(next);
  }, [links]);

  const updateSlot = (index: number, patch: Partial<Slot>) => {
    setSlots((prev) => prev.map((s, i) => (i === index ? { ...s, ...patch } : s)));
  };

  const handleSave = async () => {
    const filled = slots.filter((s) => s.dimensionId !== '');
    const dimensionIds = filled.map((s) => s.dimensionId);
    if (new Set(dimensionIds).size !== dimensionIds.length) {
      showToast(t('accountCostCenters.duplicateError'), 'error');
      return;
    }

    setSaving(true);
    try {
      const existingLinks = links ?? [];

      for (const existing of existingLinks) {
        const stillPresent = filled.find(
          (s) => s.linkId === existing.id && s.dimensionId === existing.costCenterDimensionId &&
            s.order === existing.displayOrder && s.mandatory === existing.isMandatory
        );
        if (!stillPresent) {
          await deleteLink.mutateAsync(existing.id);
        }
      }

      for (const slot of filled) {
        const unchanged = slot.linkId && existingLinks.some(
          (e) => e.id === slot.linkId && e.costCenterDimensionId === slot.dimensionId &&
            e.displayOrder === slot.order && e.isMandatory === slot.mandatory
        );
        if (unchanged) continue;

        await createLink.mutateAsync({
          accountId,
          dimensionId: Number(slot.dimensionId),
          displayOrder: slot.order,
          isMandatory: slot.mandatory
        });
      }

      showToast(t('accountCostCenters.saveSuccess'), 'success');
    } catch (error) {
      // Field-level errors (e.g. rule 24's max-5 message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardHeader>{t('accountCostCenters.heading')}</CardHeader>
      <CardBody>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {slots.map((slot, index) => (
            <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              <span style={{ fontSize: 12, color: 'var(--color-text-muted)', width: 60 }}>
                {t('accountCostCenters.slot')} {index + 1}
              </span>
              <SearchableSelect
                value={slot.dimensionId}
                onChange={(v) => updateSlot(index, { dimensionId: v === '' ? '' : Number(v) })}
                options={[
                  { value: '', label: t('accountCostCenters.none') },
                  ...(dimensions?.map((d) => ({ value: d.id, label: d.nameAr })) ?? [])
                ]}
                style={{ minWidth: 180 }}
              />
              <Input
                type="number"
                min={1}
                max={5}
                value={slot.order}
                onChange={(e) => updateSlot(index, { order: Number(e.target.value) })}
                style={{ width: 70 }}
                disabled={slot.dimensionId === ''}
              />
              <label style={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 4 }}>
                <input
                  type="checkbox"
                  checked={slot.mandatory}
                  onChange={(e) => updateSlot(index, { mandatory: e.target.checked })}
                  disabled={slot.dimensionId === ''}
                />
                {t('dimensions.mandatoryLabel')}
              </label>
            </div>
          ))}
        </div>
        <ActionBar primary={{ key: 'save', label: t('common.save'), onClick: handleSave, disabled: saving }} />
      </CardBody>
    </Card>
  );
}
