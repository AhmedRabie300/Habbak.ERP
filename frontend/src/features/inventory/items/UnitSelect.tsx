import { useTranslation } from 'react-i18next';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import type { ItemListItem } from './api';

/**
 * The unit a line's quantity is in (Remarks3): the item's base unit or one of its conversion units,
 * each labelled with how many base units it holds. Empty until an item is picked. The server
 * refuses any other unit.
 */
export function UnitSelect({ item, value, onChange, disabled, width = 130 }: {
  item: ItemListItem | undefined;
  value: number | '' | null | undefined;
  onChange: (unitId: number) => void;
  disabled?: boolean;
  width?: number;
}) {
  const { t } = useTranslation();
  const units = item?.units ?? [];
  const base = units[0];
  const options = units.map((u) => ({
    value: u.unitId,
    label: u.factor === 1 ? u.nameAr : `${u.nameAr} (${t('units.factorOf', { factor: u.factor, base: base?.nameAr ?? '' })})`
  }));
  return (
    <SearchableSelect
      disabled={disabled || !item}
      style={{ width }}
      value={value ?? ''}
      onChange={(v) => onChange(Number(v))}
      options={options}
    />
  );
}

/** The unit a new line of this item starts in: its base unit. */
export const defaultUnitId = (item: ItemListItem | undefined) => item?.baseUnitOfMeasureId ?? item?.units[0]?.unitId;

/** Base units in one of `unitId` for this item (1 when unknown). */
export const unitFactor = (item: ItemListItem | undefined, unitId: number | '' | null | undefined) =>
  item?.units.find((u) => u.unitId === Number(unitId))?.factor ?? 1;
