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
import { useWarehousesList } from '../../inventory/warehouses/api';
import { useAccountsList } from '../../accounting/accounts/api';
import { useApplyDefaultCurrency, useCurrenciesList } from '../../organization/currencies/api';
import { useCreateSupplier, useDeleteSupplier, useSupplier, useUpdateSupplier } from './api';
import { useFieldAccess } from '../../auth/access';

const PAYMENT_TERMS = ['Cash', 'Net15', 'Net30', 'Net60'] as const;

interface FormValues {
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber: string;
  phone: string;
  email: string;
  address: string;
  paymentTerms: (typeof PAYMENT_TERMS)[number];
  creditLimit: string;
  currencyCode: string | '';
  defaultWarehouseId: number | '';
  payableAccountId: number | '';
  expenseAccountId: number | '';
  isActive: boolean;
}

/** /purchasing/suppliers/:id — screen #1 (03-Module-Purchasing.md, section 8). Standard List/Edit,
 * no RowVersion concurrency — same reference-data pattern as Warehouses/Items/CustodyOfficers. */
export function SupplierEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const supplierId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  // Fix for the spurious "not found" toast on delete: once the delete mutation starts, this page
  // stops asking for the supplier at all (rather than relying on cache invalidation timing) — the
  // query would otherwise still be `enabled` while this component is mounted mid-navigate-away,
  // see it's now missing from the cache, and refetch straight into a 404.
  const [isDeleted, setIsDeleted] = useState(false);
  const { data: supplier, isLoading } = useSupplier(isDeleted ? undefined : supplierId);
  const { data: warehouses } = useWarehousesList();
  const { data: accounts } = useAccountsList(true);
  const { data: currencies } = useCurrenciesList();
  const { label } = useFieldLabels('PURCHASING_SUPPLIERS');
  const fieldAccess = useFieldAccess('PURCHASING_SUPPLIERS', 'Supplier');
  const phoneAccess = fieldAccess('Phone');
  const emailAccess = fieldAccess('Email');
  const creditAccess = fieldAccess('CreditLimit');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      code: '', nameAr: '', nameEn: '', taxNumber: '', phone: '', email: '', address: '',
      paymentTerms: 'Net30', creditLimit: '', currencyCode: '', defaultWarehouseId: '', payableAccountId: '', expenseAccountId: '', isActive: true
    }
  });
  // Remarks3 item 1: a new record starts in the default currency.
  useApplyDefaultCurrency(isNew, watch('currencyCode'), (code) => setValue('currencyCode', code), 'code');

  useEffect(() => {
    if (supplier) {
      reset({
        code: supplier.code,
        nameAr: supplier.nameAr,
        nameEn: supplier.nameEn,
        taxNumber: supplier.taxNumber ?? '',
        phone: supplier.phone ?? '',
        email: supplier.email ?? '',
        address: supplier.address ?? '',
        paymentTerms: supplier.paymentTerms as (typeof PAYMENT_TERMS)[number],
        creditLimit: supplier.creditLimit?.toString() ?? '',
        currencyCode: supplier.currencyCode,
        defaultWarehouseId: supplier.defaultWarehouseId ?? '',
        payableAccountId: supplier.payableAccountId ?? '',
        expenseAccountId: supplier.expenseAccountId ?? '',
        isActive: supplier.isActive
      });
    }
  }, [supplier, reset]);

  const createMutation = useCreateSupplier();
  const updateMutation = useUpdateSupplier(supplierId ?? 0);
  const deleteMutation = useDeleteSupplier();

  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));
  const currencyOptions = (currencies ?? []).map((c) => ({ value: c.code, label: `${c.code} — ${c.nameAr}` }));

  const onSave = handleSubmit(async (values) => {
    const payload = {
      code: values.code || undefined,
      nameAr: values.nameAr,
      nameEn: values.nameEn,
      taxNumber: values.taxNumber || undefined,
      phone: values.phone || undefined,
      email: values.email || undefined,
      address: values.address || undefined,
      paymentTerms: values.paymentTerms,
      creditLimit: values.creditLimit ? Number(values.creditLimit) : undefined,
      currencyCode: String(values.currencyCode),
      defaultWarehouseId: values.defaultWarehouseId === '' ? undefined : Number(values.defaultWarehouseId),
      payableAccountId: values.payableAccountId === '' ? undefined : Number(values.payableAccountId),
      expenseAccountId: values.expenseAccountId === '' ? undefined : Number(values.expenseAccountId),
      isActive: values.isActive
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(payload);
        showToast(t('suppliers.createSuccess'), 'success');
        navigate(`/purchasing/suppliers/${newId}`);
      } else {
        await updateMutation.mutateAsync(payload);
        showToast(t('suppliers.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleDelete = async () => {
    if (!supplierId) return;
    setIsDeleted(true);
    try {
      await deleteMutation.mutateAsync(supplierId);
      showToast(t('suppliers.deleteSuccess'), 'success');
      navigate('/purchasing/suppliers');
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
      <h2 style={{ margin: 0 }}>{isNew ? t('suppliers.addSupplier') : `${supplier?.code ?? ''} — ${supplier?.nameAr ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/suppliers') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('suppliers.deleteConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave}>
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('code', t('suppliers.code'))}>
                <Input placeholder={t('codingRules.autoGeneratedPlaceholder')} {...register('code')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameAr', t('suppliers.nameAr'))}>
                <Input {...register('nameAr')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameEn', t('suppliers.nameEn'))}>
                <Input {...register('nameEn')} />
              </FieldWrapper>
              <FieldWrapper label={label('taxNumber', t('suppliers.taxNumber'))}>
                <Input {...register('taxNumber')} />
              </FieldWrapper>
              {phoneAccess.canView && (
                <FieldWrapper label={label('phone', t('suppliers.phone'))}>
                  <Input disabled={!phoneAccess.canEdit} {...register('phone')} />
                </FieldWrapper>
              )}
              {emailAccess.canView && (
                <FieldWrapper label={label('email', t('suppliers.email'))}>
                  <Input type="email" disabled={!emailAccess.canEdit} {...register('email')} />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('address', t('suppliers.address'))}>
                <Input style={{ minWidth: 260 }} {...register('address')} />
              </FieldWrapper>
              <FieldWrapper label={label('paymentTerms', t('suppliers.paymentTerms'))}>
                <Controller
                  control={control}
                  name="paymentTerms"
                  render={({ field }) => (
                    <SearchableSelect
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={PAYMENT_TERMS.map((pt) => ({ value: pt, label: t(`suppliers.terms${pt}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              {creditAccess.canView && (
                <FieldWrapper label={label('creditLimit', t('suppliers.creditLimit'))}>
                  <Input type="number" step="0.01" style={{ width: 140 }} disabled={!creditAccess.canEdit} {...register('creditLimit')} />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('currencyCode', t('suppliers.currency'))}>
                <Controller
                  control={control}
                  name="currencyCode"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 160 }} value={field.value} onChange={(v) => field.onChange(v)} options={currencyOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('defaultWarehouse', t('suppliers.defaultWarehouse'))}>
                <Controller
                  control={control}
                  name="defaultWarehouseId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('payableAccount', t('suppliers.payableAccount'))}>
                <Controller
                  control={control}
                  name="payableAccountId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 220 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={accountOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('expenseAccount', t('suppliers.expenseAccount'))}>
                <Controller
                  control={control}
                  name="expenseAccountId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 220 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={accountOptions} />
                  )}
                />
              </FieldWrapper>
              {!isNew && (
                <FieldWrapper label={label('isActive', t('suppliers.isActive'))}>
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
