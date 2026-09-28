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
import { useAccountsList } from '../../accounting/accounts/api';
import { useLoyaltyTiersList } from '../loyaltyTiers/api';
import { useCreateCustomer, useDeleteCustomer, useCustomer, useUpdateCustomer } from './api';
import { useFieldAccess } from '../../auth/access';

const CUSTOMER_TYPES = ['Individual', 'Corporate'] as const;

interface FormValues {
  code: string;
  nameAr: string;
  nameEn: string;
  customerType: (typeof CUSTOMER_TYPES)[number];
  phone: string;
  email: string;
  address: string;
  creditLimit: string;
  paymentTermDays: string;
  receivableAccountId: number | '';
  loyaltyTierId: number | '';
  isActive: boolean;
}

/** /sales/customers/:id — screen #1 (04-Module-Sales.md, section 5). Standard List/Edit, no
 * RowVersion concurrency — same reference-data pattern as Suppliers. */
export function CustomerEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const customerId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const [isDeleted, setIsDeleted] = useState(false);
  const { data: customer, isLoading } = useCustomer(isDeleted ? undefined : customerId);
  const { data: accounts } = useAccountsList(true);
  const { data: loyaltyTiers } = useLoyaltyTiersList();
  const { label } = useFieldLabels('SALES_CUSTOMERS');
  const fieldAccess = useFieldAccess('SALES_CUSTOMERS', 'Customer');
  const phoneAccess = fieldAccess('Phone');
  const emailAccess = fieldAccess('Email');
  const creditAccess = fieldAccess('CreditLimit');

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: {
      code: '', nameAr: '', nameEn: '', customerType: 'Individual', phone: '', email: '', address: '',
      creditLimit: '0', paymentTermDays: '0', receivableAccountId: '', loyaltyTierId: '', isActive: true
    }
  });

  useEffect(() => {
    if (customer) {
      reset({
        code: customer.code,
        nameAr: customer.nameAr,
        nameEn: customer.nameEn,
        customerType: customer.customerType as (typeof CUSTOMER_TYPES)[number],
        phone: customer.phone ?? '',
        email: customer.email ?? '',
        address: customer.address ?? '',
        creditLimit: customer.creditLimit?.toString() ?? '0',
        paymentTermDays: customer.paymentTermDays.toString(),
        receivableAccountId: customer.receivableAccountId ?? '',
        loyaltyTierId: customer.loyaltyTierId ?? '',
        isActive: customer.isActive
      });
    }
  }, [customer, reset]);

  const createMutation = useCreateCustomer();
  const updateMutation = useUpdateCustomer(customerId ?? 0);
  const deleteMutation = useDeleteCustomer();

  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));
  const loyaltyTierOptions = (loyaltyTiers ?? []).map((lt) => ({ value: lt.id, label: `${lt.code} — ${lt.nameAr}` }));

  const onSave = handleSubmit(async (values) => {
    const payload = {
      code: values.code || undefined,
      nameAr: values.nameAr,
      nameEn: values.nameEn,
      customerType: values.customerType,
      phone: values.phone || undefined,
      email: values.email || undefined,
      address: values.address || undefined,
      creditLimit: values.creditLimit ? Number(values.creditLimit) : 0,
      paymentTermDays: values.paymentTermDays ? Number(values.paymentTermDays) : 0,
      receivableAccountId: values.receivableAccountId === '' ? undefined : Number(values.receivableAccountId),
      loyaltyTierId: values.loyaltyTierId === '' ? undefined : Number(values.loyaltyTierId),
      isActive: values.isActive
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(payload);
        showToast(t('customers.createSuccess'), 'success');
        navigate(`/sales/customers/${newId}`);
      } else {
        await updateMutation.mutateAsync(payload);
        showToast(t('customers.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleDelete = async () => {
    if (!customerId) return;
    setIsDeleted(true);
    try {
      await deleteMutation.mutateAsync(customerId);
      showToast(t('customers.deleteSuccess'), 'success');
      navigate('/sales/customers');
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
      <h2 style={{ margin: 0 }}>{isNew ? t('customers.addCustomer') : `${customer?.code ?? ''} — ${customer?.nameAr ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/sales/customers') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('customers.deleteConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave}>
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('code', t('customers.code'))}>
                <Input placeholder={t('codingRules.autoGeneratedPlaceholder')} {...register('code')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameAr', t('customers.nameAr'))}>
                <Input {...register('nameAr')} />
              </FieldWrapper>
              <FieldWrapper label={label('nameEn', t('customers.nameEn'))}>
                <Input {...register('nameEn')} />
              </FieldWrapper>
              <FieldWrapper label={label('customerType', t('customers.customerType'))}>
                <Controller
                  control={control}
                  name="customerType"
                  render={({ field }) => (
                    <SearchableSelect
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={CUSTOMER_TYPES.map((ct) => ({ value: ct, label: t(`customers.type${ct}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              {phoneAccess.canView && (
                <FieldWrapper label={label('phone', t('customers.phone'))}>
                  <Input disabled={!phoneAccess.canEdit} {...register('phone')} />
                </FieldWrapper>
              )}
              {emailAccess.canView && (
                <FieldWrapper label={label('email', t('customers.email'))}>
                  <Input type="email" disabled={!emailAccess.canEdit} {...register('email')} />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('address', t('customers.address'))}>
                <Input style={{ minWidth: 260 }} {...register('address')} />
              </FieldWrapper>
              {creditAccess.canView && (
                <FieldWrapper label={label('creditLimit', t('customers.creditLimit'))}>
                  <Input type="number" step="0.01" style={{ width: 140 }} disabled={!creditAccess.canEdit} {...register('creditLimit')} />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('paymentTermDays', t('customers.paymentTermDays'))}>
                <Input type="number" style={{ width: 120 }} {...register('paymentTermDays')} />
              </FieldWrapper>
              <FieldWrapper label={label('receivableAccount', t('customers.receivableAccount'))}>
                <Controller
                  control={control}
                  name="receivableAccountId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 220 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={accountOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('loyaltyTier', t('customers.loyaltyTier'))}>
                <Controller
                  control={control}
                  name="loyaltyTierId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={loyaltyTierOptions} />
                  )}
                />
              </FieldWrapper>
              {!isNew && (
                <>
                  <FieldWrapper label={label('loyaltyPointsBalance', t('customers.loyaltyPointsBalance'))}>
                    <Input value={customer?.loyaltyPointsBalance.toFixed(2) ?? '0.00'} disabled style={{ width: 120 }} />
                  </FieldWrapper>
                  <FieldWrapper label={label('isActive', t('customers.isActive'))}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                      <input type="checkbox" {...register('isActive')} />
                    </label>
                  </FieldWrapper>
                </>
              )}
            </div>
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
