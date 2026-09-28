import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardHeader, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { Button } from '../../../ui-kit/Button';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import { useUnitsOfMeasureList } from '../unitsOfMeasure/api';
import { useItemGroupsList } from '../itemGroups/api';
import { usePOSCategoriesList } from '../posCategories/api';
import { useWarehousesList } from '../warehouses/api';
import { useBranchesList } from '../../organization/branches/api';
import { useCreateItem, useDeleteItem, useItem, useUpdateItem } from './api';
import type { BranchItemLimitInput, CostMethod, ItemStatus, ItemType, ItemUnitConversionInput, ItemWarehouseSettingsInput, SaleMethod } from './api';

const itemTypeOptions: { value: ItemType; labelKey: string }[] = [
  { value: 'RawMaterial', labelKey: 'items.typeRawMaterial' },
  { value: 'SemiFinished', labelKey: 'items.typeSemiFinished' },
  { value: 'FinishedGood', labelKey: 'items.typeFinishedGood' },
  { value: 'Consumable', labelKey: 'items.typeConsumable' },
  { value: 'Service', labelKey: 'items.typeService' }
];

const saleMethodOptions: { value: SaleMethod; labelKey: string }[] = [
  { value: 'ByPiece', labelKey: 'items.saleMethodByPiece' },
  { value: 'ByWeight', labelKey: 'items.saleMethodByWeight' },
  { value: 'ByVolume', labelKey: 'items.saleMethodByVolume' }
];

const costMethodOptions: { value: CostMethod; labelKey: string }[] = [
  { value: 'WeightedAverage', labelKey: 'items.costMethodWeightedAverage' },
  { value: 'Fifo', labelKey: 'items.costMethodFifo' }
];

const statusOptions: { value: ItemStatus; labelKey: string }[] = [
  { value: 'Active', labelKey: 'status.Active' },
  { value: 'UnderReview', labelKey: 'status.UnderReview' },
  { value: 'Inactive', labelKey: 'status.Inactive' }
];

/** /inventory/items/:id — Edit screen (standard List/Edit pattern, 00-Frontend-Specs.md, section
 * 5-7). Unit conversions and per-warehouse settings are sections within this screen, not separate
 * List/Edit screens (per the module doc's screen list) — only editable once the item exists,
 * matching AccountCostCentersEditor's need for an existing accountId. Excludes the Recipe/BOM
 * section mentioned in the module doc: that depends on modules not yet built.
 *
 * Fields beyond section 2.1's own field table (barcode, purchase/sell unit, sale/cost method,
 * default price, isStocked/trackSerial/purchasable/sellable/manufacturable/allowSubstitutes,
 * the 3-state status) were added to match the reference mockup's item card at the user's
 * explicit request. */
export function ItemEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const itemId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  // Fix for the spurious "not found" toast on delete: once the delete mutation starts, this page
  // stops asking for the item at all (rather than relying on cache invalidation timing) — the
  // query would otherwise still be `enabled` while this component is mounted mid-navigate-away,
  // see it's now missing from the cache, and refetch straight into a 404.
  const [isDeleted, setIsDeleted] = useState(false);
  const { data: item, isLoading } = useItem(isDeleted ? undefined : itemId);
  const { data: codingRule } = useCodingRule('INVENTORY_ITEMS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const { data: units } = useUnitsOfMeasureList();
  const { data: itemGroups } = useItemGroupsList();
  const { data: posCategories } = usePOSCategoriesList();
  const { data: warehouses } = useWarehousesList();
  const { data: branches } = useBranchesList();

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [itemGroupId, setItemGroupId] = useState<number | ''>('');
  const [posCategoryId, setPosCategoryId] = useState<number | ''>('');
  const [itemType, setItemType] = useState<ItemType>('RawMaterial');
  const [barcode, setBarcode] = useState('');
  const [taxCode, setTaxCode] = useState('');
  const [baseUnitOfMeasureId, setBaseUnitOfMeasureId] = useState<number | ''>('');
  const [purchaseUnitOfMeasureId, setPurchaseUnitOfMeasureId] = useState<number | ''>('');
  const [sellUnitOfMeasureId, setSellUnitOfMeasureId] = useState<number | ''>('');
  const [saleMethod, setSaleMethod] = useState<SaleMethod>('ByPiece');
  const [costMethod, setCostMethod] = useState<CostMethod>('WeightedAverage');
  const [defaultPrice, setDefaultPrice] = useState('');
  const [isStocked, setIsStocked] = useState(true);
  const [isTracked, setIsTracked] = useState(false);
  const [trackSerial, setTrackSerial] = useState(false);
  const [shelfLifeDays, setShelfLifeDays] = useState<string>('');
  const [standardCost, setStandardCost] = useState<string>('');
  const [isPurchasable, setIsPurchasable] = useState(true);
  const [isSellable, setIsSellable] = useState(true);
  const [isManufacturable, setIsManufacturable] = useState(false);
  const [allowSubstitutes, setAllowSubstitutes] = useState(false);
  const [status, setStatus] = useState<ItemStatus>('Active');
  const [unitConversions, setUnitConversions] = useState<ItemUnitConversionInput[]>([]);
  const [warehouseSettings, setWarehouseSettings] = useState<ItemWarehouseSettingsInput[]>([]);
  const [branchItemLimits, setBranchItemLimits] = useState<BranchItemLimitInput[]>([]);

  useEffect(() => {
    if (item) {
      setNameAr(item.nameAr);
      setNameEn(item.nameEn);
      setItemGroupId(item.itemGroupId ?? '');
      setPosCategoryId(item.posCategoryId ?? '');
      setItemType(item.itemType);
      setBarcode(item.barcode ?? '');
      setTaxCode(item.taxCode ?? '');
      setBaseUnitOfMeasureId(item.baseUnitOfMeasureId);
      setPurchaseUnitOfMeasureId(item.purchaseUnitOfMeasureId ?? '');
      setSellUnitOfMeasureId(item.sellUnitOfMeasureId ?? '');
      setSaleMethod(item.saleMethod);
      setCostMethod(item.costMethod);
      setDefaultPrice(item.defaultPrice?.toString() ?? '');
      setIsStocked(item.isStocked);
      setIsTracked(item.isTracked);
      setTrackSerial(item.trackSerial);
      setShelfLifeDays(item.shelfLifeDays?.toString() ?? '');
      setStandardCost(item.standardCost?.toString() ?? '');
      setIsPurchasable(item.isPurchasable);
      setIsSellable(item.isSellable);
      setIsManufacturable(item.isManufacturable);
      setAllowSubstitutes(item.allowSubstitutes);
      setStatus(item.status);
      setUnitConversions(item.unitConversions.map((c) => ({ alternateUnitOfMeasureId: c.alternateUnitOfMeasureId, conversionFactor: c.conversionFactor })));
      setWarehouseSettings(item.warehouseSettings.map((w) => ({ warehouseId: w.warehouseId, minStockLevel: w.minStockLevel, maxStockLevel: w.maxStockLevel, reorderPoint: w.reorderPoint })));
      setBranchItemLimits(item.branchItemLimits.map((l) => ({ branchId: l.branchId, minRequestQuantity: l.minRequestQuantity, maxRequestQuantity: l.maxRequestQuantity })));
    }
  }, [item]);

  const createMutation = useCreateItem();
  const updateMutation = useUpdateItem(itemId ?? 0);
  const deleteMutation = useDeleteItem();

  const unitOptions = (units ?? []).map((u) => ({ value: u.id, label: `${u.code} — ${u.nameAr}` }));
  const unitOptionsWithNone = [{ value: '', label: t('items.none') }, ...unitOptions];
  const alternateUnitOptions = unitOptions.filter((o) => o.value !== baseUnitOfMeasureId);
  const itemGroupOptions = [{ value: '', label: t('items.none') }, ...(itemGroups ?? []).map((g) => ({ value: g.id, label: g.nameAr }))];
  const posCategoryOptions = [{ value: '', label: t('items.none') }, ...(posCategories ?? []).map((c) => ({ value: c.id, label: c.nameAr }))];
  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const branchOptions = (branches ?? []).map((b) => ({ value: b.id, label: `${b.code} — ${b.nameAr}` }));

  const addUnitConversion = () => {
    const firstAvailable = alternateUnitOptions.find((o) => !unitConversions.some((c) => c.alternateUnitOfMeasureId === o.value));
    if (!firstAvailable) return;
    setUnitConversions((prev) => [...prev, { alternateUnitOfMeasureId: Number(firstAvailable.value), conversionFactor: 1 }]);
  };

  const addWarehouseSetting = () => {
    const firstAvailable = warehouseOptions.find((o) => !warehouseSettings.some((w) => w.warehouseId === o.value));
    if (!firstAvailable) return;
    setWarehouseSettings((prev) => [...prev, { warehouseId: Number(firstAvailable.value), minStockLevel: null, maxStockLevel: null, reorderPoint: null }]);
  };

  const addBranchItemLimit = () => {
    const firstAvailable = branchOptions.find((o) => !branchItemLimits.some((l) => l.branchId === o.value));
    if (!firstAvailable) return;
    setBranchItemLimits((prev) => [...prev, { branchId: Number(firstAvailable.value), minRequestQuantity: null, maxRequestQuantity: 0 }]);
  };

  const handleSave = async () => {
    try {
      const basics = {
        nameAr,
        nameEn,
        itemGroupId: itemGroupId === '' ? null : Number(itemGroupId),
        posCategoryId: posCategoryId === '' ? null : Number(posCategoryId),
        itemType,
        barcode: barcode || null,
        taxCode: taxCode.trim() || null,
        baseUnitOfMeasureId: Number(baseUnitOfMeasureId),
        purchaseUnitOfMeasureId: purchaseUnitOfMeasureId === '' ? null : Number(purchaseUnitOfMeasureId),
        sellUnitOfMeasureId: sellUnitOfMeasureId === '' ? null : Number(sellUnitOfMeasureId),
        saleMethod,
        costMethod,
        defaultPrice: defaultPrice === '' ? null : Number(defaultPrice),
        isStocked,
        isTracked,
        trackSerial,
        shelfLifeDays: shelfLifeDays === '' ? null : Number(shelfLifeDays),
        standardCost: standardCost === '' ? null : Number(standardCost),
        isPurchasable,
        isSellable,
        isManufacturable,
        allowSubstitutes,
        status
      };

      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({ code: codeIsAutomatic ? undefined : code, ...basics });
        showToast(t('items.createSuccess'), 'success');
        navigate(`/inventory/items/${newId}`);
      } else {
        await updateMutation.mutateAsync({ ...basics, unitConversions, warehouseSettings, branchItemLimits });
        showToast(t('items.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!itemId) return;
    setIsDeleted(true);
    try {
      await deleteMutation.mutateAsync(itemId);
      showToast(t('items.deleteSuccess'), 'success');
      navigate('/inventory/items');
    } catch (error) {
      setIsDeleted(false);
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 900 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('items.addItem') : `${t('items.title')} — ${item?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/items') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('items.deleteConfirm') }]
            : []
        }
      />

      <Card>
        <CardHeader>{t('items.basicInfo')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('items.code')}>
              {codeIsAutomatic ? (
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (item?.code ?? '')} disabled />
              ) : (
                <Input value={isNew ? code : (item?.code ?? '')} onChange={(e) => setCode(e.target.value)} disabled={!isNew} />
              )}
            </FieldWrapper>

            <FieldWrapper label={t('items.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('items.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('items.itemType')}>
              <SearchableSelect
                value={itemType}
                onChange={(v) => setItemType(v as ItemType)}
                options={itemTypeOptions.map((o) => ({ value: o.value, label: t(o.labelKey) }))}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.barcode')}>
              <Input value={barcode} onChange={(e) => setBarcode(e.target.value)} style={{ minWidth: 160 }} />
            </FieldWrapper>

            <FieldWrapper label={t('items.taxCode')}>
              <Input value={taxCode} onChange={(e) => setTaxCode(e.target.value)} maxLength={50} dir="ltr" placeholder={t('items.taxCodeHint')} style={{ minWidth: 180 }} />
            </FieldWrapper>

            <FieldWrapper label={t('items.itemGroup')}>
              <SearchableSelect
                value={itemGroupId}
                onChange={(v) => setItemGroupId(v === '' ? '' : Number(v))}
                options={itemGroupOptions}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.posCategory')}>
              <SearchableSelect
                value={posCategoryId}
                onChange={(v) => setPosCategoryId(v === '' ? '' : Number(v))}
                options={posCategoryOptions}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('items.unitsAndPricing')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('items.baseUnit')}>
              <SearchableSelect
                value={baseUnitOfMeasureId}
                onChange={(v) => setBaseUnitOfMeasureId(Number(v))}
                options={unitOptions}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.purchaseUnit')}>
              <SearchableSelect
                value={purchaseUnitOfMeasureId}
                onChange={(v) => setPurchaseUnitOfMeasureId(v === '' ? '' : Number(v))}
                options={unitOptionsWithNone}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.sellUnit')}>
              <SearchableSelect
                value={sellUnitOfMeasureId}
                onChange={(v) => setSellUnitOfMeasureId(v === '' ? '' : Number(v))}
                options={unitOptionsWithNone}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.saleMethod')}>
              <SearchableSelect
                value={saleMethod}
                onChange={(v) => setSaleMethod(v as SaleMethod)}
                options={saleMethodOptions.map((o) => ({ value: o.value, label: t(o.labelKey) }))}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.costMethod')}>
              <SearchableSelect
                value={costMethod}
                onChange={(v) => setCostMethod(v as CostMethod)}
                options={costMethodOptions.map((o) => ({ value: o.value, label: t(o.labelKey) }))}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('items.defaultPrice')}>
              <Input type="number" min={0} step="0.0001" value={defaultPrice} onChange={(e) => setDefaultPrice(e.target.value)} style={{ width: 140 }} />
            </FieldWrapper>

            <FieldWrapper label={t('items.standardCost')}>
              <Input type="number" min={0} step="0.0001" value={standardCost} onChange={(e) => setStandardCost(e.target.value)} style={{ width: 140 }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('items.stockAndTracking')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('items.isStocked')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isStocked} onChange={(e) => setIsStocked(e.target.checked)} />
              </label>
            </FieldWrapper>

            <FieldWrapper label={t('items.isTracked')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isTracked} onChange={(e) => setIsTracked(e.target.checked)} />
              </label>
            </FieldWrapper>

            <FieldWrapper label={t('items.trackSerial')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={trackSerial} onChange={(e) => setTrackSerial(e.target.checked)} />
              </label>
            </FieldWrapper>

            {isTracked && (
              <FieldWrapper label={t('items.shelfLifeDays')}>
                <Input type="number" min={1} value={shelfLifeDays} onChange={(e) => setShelfLifeDays(e.target.value)} style={{ width: 120 }} />
              </FieldWrapper>
            )}
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('items.purchaseAndSales')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('items.isPurchasable')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isPurchasable} onChange={(e) => setIsPurchasable(e.target.checked)} />
              </label>
            </FieldWrapper>

            <FieldWrapper label={t('items.isSellable')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isSellable} onChange={(e) => setIsSellable(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('items.manufacturing')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('items.isManufacturable')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isManufacturable} onChange={(e) => setIsManufacturable(e.target.checked)} />
              </label>
            </FieldWrapper>

            <FieldWrapper label={t('items.allowSubstitutes')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={allowSubstitutes} onChange={(e) => setAllowSubstitutes(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('items.status')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('items.itemStatus')}>
              <SearchableSelect
                value={status}
                onChange={(v) => setStatus(v as ItemStatus)}
                options={statusOptions.map((o) => ({ value: o.value, label: t(o.labelKey) }))}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      {!isNew && (
        <Card>
          <CardHeader>{t('items.unitConversions')}</CardHeader>
          <CardBody>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {unitConversions.length === 0 && (
                <div style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('items.noUnitConversions')}</div>
              )}
              {unitConversions.map((conversion, index) => (
                <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <SearchableSelect
                    value={conversion.alternateUnitOfMeasureId}
                    onChange={(v) => setUnitConversions((prev) => prev.map((c, i) => (i === index ? { ...c, alternateUnitOfMeasureId: Number(v) } : c)))}
                    options={alternateUnitOptions}
                    style={{ minWidth: 200 }}
                  />
                  <Input
                    type="number"
                    min={0}
                    step="0.000001"
                    value={conversion.conversionFactor}
                    onChange={(e) => setUnitConversions((prev) => prev.map((c, i) => (i === index ? { ...c, conversionFactor: Number(e.target.value) } : c)))}
                    placeholder={t('items.conversionFactor')}
                    style={{ width: 140 }}
                  />
                  <Button variant="ghost" size="sm" onClick={() => setUnitConversions((prev) => prev.filter((_, i) => i !== index))}>
                    {t('common.remove')}
                  </Button>
                </div>
              ))}
              <Button variant="secondary" size="sm" onClick={addUnitConversion} disabled={alternateUnitOptions.length === 0}>
                {t('items.addUnitConversion')}
              </Button>
            </div>
          </CardBody>
        </Card>
      )}

      {!isNew && (
        <Card>
          <CardHeader>{t('items.warehouseSettings')}</CardHeader>
          <CardBody>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {warehouseSettings.length === 0 && (
                <div style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('items.noWarehouseSettings')}</div>
              )}
              {warehouseSettings.map((setting, index) => (
                <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <SearchableSelect
                    value={setting.warehouseId}
                    onChange={(v) => setWarehouseSettings((prev) => prev.map((w, i) => (i === index ? { ...w, warehouseId: Number(v) } : w)))}
                    options={warehouseOptions}
                    style={{ minWidth: 200 }}
                  />
                  <Input
                    type="number"
                    min={0}
                    value={setting.minStockLevel ?? ''}
                    onChange={(e) => setWarehouseSettings((prev) => prev.map((w, i) => (i === index ? { ...w, minStockLevel: e.target.value === '' ? null : Number(e.target.value) } : w)))}
                    placeholder={t('items.minStockLevel')}
                    style={{ width: 130 }}
                  />
                  <Input
                    type="number"
                    min={0}
                    value={setting.maxStockLevel ?? ''}
                    onChange={(e) => setWarehouseSettings((prev) => prev.map((w, i) => (i === index ? { ...w, maxStockLevel: e.target.value === '' ? null : Number(e.target.value) } : w)))}
                    placeholder={t('items.maxStockLevel')}
                    style={{ width: 130 }}
                  />
                  <Input
                    type="number"
                    min={0}
                    value={setting.reorderPoint ?? ''}
                    onChange={(e) => setWarehouseSettings((prev) => prev.map((w, i) => (i === index ? { ...w, reorderPoint: e.target.value === '' ? null : Number(e.target.value) } : w)))}
                    placeholder={t('items.reorderPoint')}
                    style={{ width: 130 }}
                  />
                  <Button variant="ghost" size="sm" onClick={() => setWarehouseSettings((prev) => prev.filter((_, i) => i !== index))}>
                    {t('common.remove')}
                  </Button>
                </div>
              ))}
              <Button variant="secondary" size="sm" onClick={addWarehouseSetting} disabled={warehouseOptions.length === 0}>
                {t('items.addWarehouseSetting')}
              </Button>
            </div>
          </CardBody>
        </Card>
      )}

      {!isNew && (
        <Card>
          <CardHeader>{t('items.branchItemLimits')}</CardHeader>
          <CardBody>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {branchItemLimits.length === 0 && (
                <div style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('items.noBranchItemLimits')}</div>
              )}
              {branchItemLimits.map((limit, index) => (
                <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <SearchableSelect
                    value={limit.branchId}
                    onChange={(v) => setBranchItemLimits((prev) => prev.map((l, i) => (i === index ? { ...l, branchId: Number(v) } : l)))}
                    options={branchOptions}
                    style={{ minWidth: 200 }}
                  />
                  <Input
                    type="number"
                    min={0}
                    value={limit.minRequestQuantity ?? ''}
                    onChange={(e) => setBranchItemLimits((prev) => prev.map((l, i) => (i === index ? { ...l, minRequestQuantity: e.target.value === '' ? null : Number(e.target.value) } : l)))}
                    placeholder={t('items.minRequestQuantity')}
                    style={{ width: 140 }}
                  />
                  <Input
                    type="number"
                    min={0}
                    value={limit.maxRequestQuantity}
                    onChange={(e) => setBranchItemLimits((prev) => prev.map((l, i) => (i === index ? { ...l, maxRequestQuantity: Number(e.target.value) } : l)))}
                    placeholder={t('items.maxRequestQuantity')}
                    style={{ width: 140 }}
                  />
                  <Button variant="ghost" size="sm" onClick={() => setBranchItemLimits((prev) => prev.filter((_, i) => i !== index))}>
                    {t('common.remove')}
                  </Button>
                </div>
              ))}
              <Button variant="secondary" size="sm" onClick={addBranchItemLimit} disabled={branchOptions.length === 0}>
                {t('items.addBranchItemLimit')}
              </Button>
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
