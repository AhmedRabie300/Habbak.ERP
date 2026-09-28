import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList } from '../../organization/branches/api';
import { useItemsList } from '../../inventory/items/api';
import { useCreatePriceList, useDeletePriceList, usePriceList, useUpdatePriceList } from './api';

interface FormValues {
  code: string;
  nameAr: string;
  nameEn: string;
  effectiveFromDate: string;
  effectiveToDate: string;
  isActive: boolean;
}

interface LinePrices {
  dineInPrice: string;
  takeawayPrice: string;
  deliveryPrice: string;
}

/** /sales/price-lists/:id — screen #2 (04-Module-Sales.md, section 5). Interactive screen: Multi-select
 * branches + a grid of every item with 3 independent price columns (قاعدة 4). A row with all three
 * prices at 0 is treated as "not priced on this list" and excluded from the saved payload. */
export function PriceListEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const priceListId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const [isDeleted, setIsDeleted] = useState(false);
  const { data: priceList, isLoading } = usePriceList(isDeleted ? undefined : priceListId);
  const { data: branches } = useBranchesList();
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('SALES_PRICE_LISTS');

  const [selectedBranchIds, setSelectedBranchIds] = useState<Set<number>>(new Set());
  const [linePrices, setLinePrices] = useState<Record<number, LinePrices>>({});
  const [itemSearch, setItemSearch] = useState('');

  const { register, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { code: '', nameAr: '', nameEn: '', effectiveFromDate: '', effectiveToDate: '', isActive: true }
  });

  useEffect(() => {
    if (priceList) {
      reset({
        code: priceList.code,
        nameAr: priceList.nameAr,
        nameEn: priceList.nameEn,
        effectiveFromDate: priceList.effectiveFromDate,
        effectiveToDate: priceList.effectiveToDate ?? '',
        isActive: priceList.isActive
      });
      setSelectedBranchIds(new Set(priceList.branchIds));
      const prices: Record<number, LinePrices> = {};
      for (const line of priceList.lines) {
        prices[line.itemId] = {
          dineInPrice: line.dineInPrice.toString(),
          takeawayPrice: line.takeawayPrice.toString(),
          deliveryPrice: line.deliveryPrice.toString()
        };
      }
      setLinePrices(prices);
    }
  }, [priceList, reset]);

  const createMutation = useCreatePriceList();
  const updateMutation = useUpdatePriceList(priceListId ?? 0);
  const deleteMutation = useDeletePriceList();

  const toggleBranch = (branchId: number) => {
    setSelectedBranchIds((prev) => {
      const next = new Set(prev);
      if (next.has(branchId)) next.delete(branchId);
      else next.add(branchId);
      return next;
    });
  };

  const updateLinePrice = (itemId: number, field: keyof LinePrices, value: string) => {
    setLinePrices((prev) => {
      // An item with no prices yet starts at zero; the index type does not say it may be missing.
      const current: LinePrices = (prev[itemId] as LinePrices | undefined) ?? { dineInPrice: '0', takeawayPrice: '0', deliveryPrice: '0' };
      return { ...prev, [itemId]: { ...current, [field]: value } };
    });
  };

  const onSave = handleSubmit(async (values) => {
    const lines = Object.entries(linePrices)
      .map(([itemId, prices]) => ({
        itemId: Number(itemId),
        dineInPrice: Number(prices.dineInPrice || 0),
        takeawayPrice: Number(prices.takeawayPrice || 0),
        deliveryPrice: Number(prices.deliveryPrice || 0)
      }))
      .filter((l) => l.dineInPrice > 0 || l.takeawayPrice > 0 || l.deliveryPrice > 0);

    const payload = {
      code: values.code || undefined,
      nameAr: values.nameAr,
      nameEn: values.nameEn,
      effectiveFromDate: values.effectiveFromDate,
      effectiveToDate: values.effectiveToDate || undefined,
      branchIds: Array.from(selectedBranchIds),
      lines,
      isActive: values.isActive
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(payload);
        showToast(t('priceLists.createSuccess'), 'success');
        navigate(`/sales/price-lists/${newId}`);
      } else {
        await updateMutation.mutateAsync(payload);
        showToast(t('priceLists.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleDelete = async () => {
    if (!priceListId) return;
    setIsDeleted(true);
    try {
      await deleteMutation.mutateAsync(priceListId);
      showToast(t('priceLists.deleteSuccess'), 'success');
      navigate('/sales/price-lists');
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
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('priceLists.addPriceList') : `${priceList?.code ?? ''} — ${priceList?.nameAr ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/sales/price-lists') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('priceLists.deleteConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave}>
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('code', t('priceLists.code'))}>
                <Input placeholder={t('codingRules.autoGeneratedPlaceholder')} {...register('code')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameAr', t('priceLists.nameAr'))}>
                <Input {...register('nameAr')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameEn', t('priceLists.nameEn'))}>
                <Input {...register('nameEn')} />
              </FieldWrapper>
              <FieldWrapper label={label('effectiveFromDate', t('priceLists.effectiveFromDate'))}>
                <Input type="date" {...register('effectiveFromDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('effectiveToDate', t('priceLists.effectiveToDate'))}>
                <Input type="date" {...register('effectiveToDate')} />
              </FieldWrapper>
              {!isNew && (
                <FieldWrapper label={label('isActive', t('priceLists.isActive'))}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                    <input type="checkbox" {...register('isActive')} />
                  </label>
                </FieldWrapper>
              )}
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <h3 style={{ marginTop: 0 }}>{t('priceLists.branches')}</h3>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10 }}>
              {(branches ?? []).map((b) => (
                <label key={b.id} style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13.5 }}>
                  <input type="checkbox" checked={selectedBranchIds.has(b.id)} onChange={() => toggleBranch(b.id)} />
                  {b.code} — {b.nameAr}
                </label>
              ))}
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8, marginBottom: 12 }}>
              <h3 style={{ margin: 0 }}>{t('priceLists.itemPrices')}</h3>
              <Input
                style={{ minWidth: 260 }}
                placeholder={t('priceLists.searchItems')}
                value={itemSearch}
                onChange={(e) => setItemSearch(e.target.value)}
              />
            </div>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('priceLists.item')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('priceLists.dineInPrice')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('priceLists.takeawayPrice')}</th>
                    <th style={{ textAlign: 'start', padding: 6 }}>{t('priceLists.deliveryPrice')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(items ?? [])
                    .filter((item) => {
                      const query = itemSearch.trim().toLowerCase();
                      if (!query) return true;
                      return (
                        item.code.toLowerCase().includes(query) ||
                        item.nameAr.toLowerCase().includes(query) ||
                        item.nameEn.toLowerCase().includes(query) ||
                        (item.barcode ?? '').toLowerCase().includes(query)
                      );
                    })
                    .map((item) => {
                    const prices = linePrices[item.id] ?? { dineInPrice: '', takeawayPrice: '', deliveryPrice: '' };
                    return (
                      <tr key={item.id}>
                        <td style={{ padding: 6 }}>{item.code} — {item.nameAr}</td>
                        <td style={{ padding: 6 }}>
                          <Input type="number" step="0.01" style={{ width: 100 }} value={prices.dineInPrice} onChange={(e) => updateLinePrice(item.id, 'dineInPrice', e.target.value)} />
                        </td>
                        <td style={{ padding: 6 }}>
                          <Input type="number" step="0.01" style={{ width: 100 }} value={prices.takeawayPrice} onChange={(e) => updateLinePrice(item.id, 'takeawayPrice', e.target.value)} />
                        </td>
                        <td style={{ padding: 6 }}>
                          <Input type="number" step="0.01" style={{ width: 100 }} value={prices.deliveryPrice} onChange={(e) => updateLinePrice(item.id, 'deliveryPrice', e.target.value)} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
