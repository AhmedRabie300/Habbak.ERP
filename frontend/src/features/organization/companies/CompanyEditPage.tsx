import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useApplyDefaultCurrency, useCurrenciesList } from '../currencies/api';
import { useCompaniesList, useCreateCompany, useDeleteCompany, useUpdateCompany } from './api';

/** /settings/companies/:id — Edit screen (standard List/Edit pattern). */
export function CompanyEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const companyId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: companies, isLoading } = useCompaniesList();
  const company = companies?.find((c) => c.id === companyId);
  const { data: currencies } = useCurrenciesList();

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [commercialRegister, setCommercialRegister] = useState('');
  const [taxCard, setTaxCard] = useState('');
  const [baseCurrencyId, setBaseCurrencyId] = useState(0);
  const [isActive, setIsActive] = useState(true);
  // Remarks3 item 1: a new company starts with the default currency as its base.
  useApplyDefaultCurrency(isNew, baseCurrencyId, setBaseCurrencyId, 'id');

  useEffect(() => {
    if (company) {
      setNameAr(company.nameAr);
      setNameEn(company.nameEn);
      setCommercialRegister(company.commercialRegister ?? '');
      setTaxCard(company.taxCard ?? '');
      setBaseCurrencyId(company.baseCurrencyId);
      setIsActive(company.isActive);
    }
  }, [company]);

  const createMutation = useCreateCompany();
  const updateMutation = useUpdateCompany(companyId ?? 0);
  const deleteMutation = useDeleteCompany();

  const handleSave = async () => {
    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          code, nameAr, nameEn,
          commercialRegister: commercialRegister || undefined,
          taxCard: taxCard || undefined,
          baseCurrencyId
        });
        showToast(t('companies.createSuccess'), 'success');
        navigate(`/settings/companies/${newId}`);
      } else {
        await updateMutation.mutateAsync({
          nameAr, nameEn,
          commercialRegister: commercialRegister || undefined,
          taxCard: taxCard || undefined,
          baseCurrencyId,
          isActive
        });
        showToast(t('companies.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!companyId) return;
    try {
      await deleteMutation.mutateAsync(companyId);
      showToast(t('companies.deleteSuccess'), 'success');
      navigate('/settings/companies');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  const currencyOptions = [
    { value: 0, label: t('companies.selectCurrency') },
    ...(currencies?.map((c) => ({ value: c.id, label: `${c.code} - ${c.nameAr}` })) ?? [])
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('companies.addCompany') : `${t('companies.title')} — ${company?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/companies') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('companies.deleteConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('companies.code')}>
              <Input value={isNew ? code : (company?.code ?? '')} onChange={(e) => setCode(e.target.value)} disabled={!isNew} />
            </FieldWrapper>

            <FieldWrapper label={t('companies.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('companies.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>

            <FieldWrapper label={t('companies.commercialRegister')}>
              <Input value={commercialRegister} onChange={(e) => setCommercialRegister(e.target.value)} style={{ minWidth: 160 }} />
            </FieldWrapper>

            <FieldWrapper label={t('companies.taxCard')}>
              <Input value={taxCard} onChange={(e) => setTaxCard(e.target.value)} style={{ minWidth: 160 }} />
            </FieldWrapper>

            <FieldWrapper label={t('companies.baseCurrency')}>
              <SearchableSelect
                value={baseCurrencyId}
                onChange={(v) => setBaseCurrencyId(Number(v))}
                options={currencyOptions}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>

            {!isNew && (
              <FieldWrapper label={t('companies.isActive')}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                </label>
              </FieldWrapper>
            )}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
