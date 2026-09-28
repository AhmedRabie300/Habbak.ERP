import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCreateCurrency, useCurrenciesList, useDeleteCurrency, useUpdateCurrency } from './api';

/** /settings/currencies/:id — Edit screen (standard List/Edit pattern). */
export function CurrencyEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const currencyId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: currencies, isLoading } = useCurrenciesList();
  const currency = currencies?.find((c) => c.id === currencyId);

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [isDefault, setIsDefault] = useState(false);

  useEffect(() => {
    if (currency) {
      setNameAr(currency.nameAr);
      setNameEn(currency.nameEn);
      setIsActive(currency.isActive);
      setIsDefault(currency.isDefault);
    }
  }, [currency]);

  const createMutation = useCreateCurrency();
  const updateMutation = useUpdateCurrency(currencyId ?? 0);
  const deleteMutation = useDeleteCurrency();

  const handleSave = async () => {
    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({ code, nameAr, nameEn, isDefault });
        showToast(t('currencies.createSuccess'), 'success');
        navigate(`/settings/currencies/${newId}`);
      } else {
        await updateMutation.mutateAsync({ nameAr, nameEn, isActive, isDefault });
        showToast(t('currencies.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!currencyId) return;
    try {
      await deleteMutation.mutateAsync(currencyId);
      showToast(t('currencies.deleteSuccess'), 'success');
      navigate('/settings/currencies');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('currencies.addCurrency') : `${t('currencies.title')} — ${currency?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/currencies') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('currencies.deleteConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('currencies.code')}>
              <Input
                value={isNew ? code : (currency?.code ?? '')}
                onChange={(e) => setCode(e.target.value.toUpperCase())}
                disabled={!isNew}
                maxLength={3}
                style={{ width: 80, textTransform: 'uppercase' }}
              />
            </FieldWrapper>

            <FieldWrapper label={t('currencies.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('currencies.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            {!isNew && (
              <FieldWrapper label={t('currencies.isActive')}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" checked={isActive} disabled={currency?.isDefault} onChange={(e) => setIsActive(e.target.checked)} />
                </label>
              </FieldWrapper>
            )}

            <FieldWrapper label={t('currencies.isDefault')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38, fontSize: 12.5, color: 'var(--color-text-muted)' }}>
                {/* The default can only move: tick it on another currency to take it off this one. */}
                <input type="checkbox" checked={isDefault} disabled={currency?.isDefault} onChange={(e) => setIsDefault(e.target.checked)} />
                {currency?.isDefault ? t('currencies.isDefaultLocked') : t('currencies.isDefaultHint')}
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
