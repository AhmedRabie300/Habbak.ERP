import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCreateDiscount, useDeleteDiscount, useDiscount, useUpdateDiscount } from './api';

const DISCOUNT_TYPES = ['Percentage', 'FixedAmount'] as const;

interface FormValues {
  code: string;
  nameAr: string;
  nameEn: string;
  discountType: (typeof DISCOUNT_TYPES)[number];
  value: string;
  applicationPriority: string;
  isStackable: boolean;
  minInvoiceAmount: string;
  minQuantity: string;
  isHappyHour: boolean;
  happyHourFromTime: string;
  happyHourToTime: string;
  effectiveFromDate: string;
  effectiveToDate: string;
  isActive: boolean;
}

/** /sales/discounts/:id — screen #3 (04-Module-Sales.md, section 5). Happy Hour time fields show
 * conditionally when IsHappyHour is checked (قاعدة 10). */
export function DiscountEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const discountId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const [isDeleted, setIsDeleted] = useState(false);
  const { data: discount, isLoading } = useDiscount(isDeleted ? undefined : discountId);
  const { label } = useFieldLabels('SALES_DISCOUNTS');

  const { register, control, handleSubmit, reset, watch } = useForm<FormValues>({
    defaultValues: {
      code: '', nameAr: '', nameEn: '', discountType: 'Percentage', value: '', applicationPriority: '1',
      isStackable: false, minInvoiceAmount: '', minQuantity: '', isHappyHour: false,
      happyHourFromTime: '', happyHourToTime: '', effectiveFromDate: '', effectiveToDate: '', isActive: true
    }
  });

  const isHappyHour = watch('isHappyHour');

  useEffect(() => {
    if (discount) {
      reset({
        code: discount.code,
        nameAr: discount.nameAr,
        nameEn: discount.nameEn,
        discountType: discount.discountType as (typeof DISCOUNT_TYPES)[number],
        value: discount.value.toString(),
        applicationPriority: discount.applicationPriority.toString(),
        isStackable: discount.isStackable,
        minInvoiceAmount: discount.minInvoiceAmount?.toString() ?? '',
        minQuantity: discount.minQuantity?.toString() ?? '',
        isHappyHour: discount.isHappyHour,
        happyHourFromTime: discount.happyHourFromTime ?? '',
        happyHourToTime: discount.happyHourToTime ?? '',
        effectiveFromDate: discount.effectiveFromDate,
        effectiveToDate: discount.effectiveToDate ?? '',
        isActive: discount.isActive
      });
    }
  }, [discount, reset]);

  const createMutation = useCreateDiscount();
  const updateMutation = useUpdateDiscount(discountId ?? 0);
  const deleteMutation = useDeleteDiscount();

  const onSave = handleSubmit(async (values) => {
    const payload = {
      code: values.code || undefined,
      nameAr: values.nameAr,
      nameEn: values.nameEn,
      discountType: values.discountType,
      value: Number(values.value || 0),
      applicationPriority: Number(values.applicationPriority || 0),
      isStackable: values.isStackable,
      minInvoiceAmount: values.minInvoiceAmount ? Number(values.minInvoiceAmount) : undefined,
      minQuantity: values.minQuantity ? Number(values.minQuantity) : undefined,
      isHappyHour: values.isHappyHour,
      happyHourFromTime: values.isHappyHour && values.happyHourFromTime ? values.happyHourFromTime : undefined,
      happyHourToTime: values.isHappyHour && values.happyHourToTime ? values.happyHourToTime : undefined,
      effectiveFromDate: values.effectiveFromDate,
      effectiveToDate: values.effectiveToDate || undefined,
      isActive: values.isActive
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(payload);
        showToast(t('discounts.createSuccess'), 'success');
        navigate(`/sales/discounts/${newId}`);
      } else {
        await updateMutation.mutateAsync(payload);
        showToast(t('discounts.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleDelete = async () => {
    if (!discountId) return;
    setIsDeleted(true);
    try {
      await deleteMutation.mutateAsync(discountId);
      showToast(t('discounts.deleteSuccess'), 'success');
      navigate('/sales/discounts');
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
      <h2 style={{ margin: 0 }}>{isNew ? t('discounts.addDiscount') : `${discount?.code ?? ''} — ${discount?.nameAr ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/sales/discounts') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('discounts.deleteConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave}>
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('code', t('discounts.code'))}>
                <Input placeholder={t('codingRules.autoGeneratedPlaceholder')} {...register('code')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameAr', t('discounts.nameAr'))}>
                <Input {...register('nameAr')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameEn', t('discounts.nameEn'))}>
                <Input {...register('nameEn')} />
              </FieldWrapper>
              <FieldWrapper label={label('discountType', t('discounts.discountType'))}>
                <Controller
                  control={control}
                  name="discountType"
                  render={({ field }) => (
                    <SearchableSelect
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={DISCOUNT_TYPES.map((dt) => ({ value: dt, label: t(`discounts.type${dt}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('value', t('discounts.value'))}>
                <Input type="number" step="0.01" style={{ width: 120 }} {...register('value')} />
              </FieldWrapper>
              <FieldWrapper label={label('applicationPriority', t('discounts.applicationPriority'))}>
                <Input type="number" style={{ width: 100 }} {...register('applicationPriority')} />
              </FieldWrapper>
              <FieldWrapper label={label('minInvoiceAmount', t('discounts.minInvoiceAmount'))}>
                <Input type="number" step="0.01" style={{ width: 140 }} {...register('minInvoiceAmount')} />
              </FieldWrapper>
              <FieldWrapper label={label('minQuantity', t('discounts.minQuantity'))}>
                <Input type="number" step="0.01" style={{ width: 120 }} {...register('minQuantity')} />
              </FieldWrapper>
              <FieldWrapper label={label('effectiveFromDate', t('discounts.effectiveFromDate'))}>
                <Input type="date" {...register('effectiveFromDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('effectiveToDate', t('discounts.effectiveToDate'))}>
                <Input type="date" {...register('effectiveToDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('isStackable', t('discounts.isStackable'))}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" {...register('isStackable')} />
                </label>
              </FieldWrapper>
              <FieldWrapper label={label('isHappyHour', t('discounts.isHappyHour'))}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" {...register('isHappyHour')} />
                </label>
              </FieldWrapper>
              {isHappyHour && (
                <>
                  <FieldWrapper label={label('happyHourFromTime', t('discounts.happyHourFromTime'))}>
                    <Input type="time" {...register('happyHourFromTime')} />
                  </FieldWrapper>
                  <FieldWrapper label={label('happyHourToTime', t('discounts.happyHourToTime'))}>
                    <Input type="time" {...register('happyHourToTime')} />
                  </FieldWrapper>
                </>
              )}
              {!isNew && (
                <FieldWrapper label={label('isActive', t('discounts.isActive'))}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                    <input type="checkbox" {...register('isActive')} />
                  </label>
                </FieldWrapper>
              )}
            </div>
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
