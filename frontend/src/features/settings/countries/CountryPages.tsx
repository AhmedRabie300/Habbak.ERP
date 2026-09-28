import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../codingRules/api';
import { toPaged } from '../listing';
import { useCountries, useDeleteCountry, useSaveCountry, type Country } from './api';

/** /settings/countries — screen SETTINGS_COUNTRIES. */
export function CountriesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useCountries();

  const rows = toPaged(data, search, (c, q) => `${c.code} ${c.nameAr} ${c.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<Country>[] = [
    { key: 'code', label: t('settings.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('settings.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'isoCode', label: t('settings.countries.isoCode'), render: (r) => r.isoCode ?? '—', exportValue: (r) => r.isoCode ?? '' },
    { key: 'isActive', label: t('settings.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('settings.countries.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/settings/countries/new')}>
            {t('settings.countries.add')}
          </Button>
        )}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/settings/countries/${row.id}`)}
        exportFileName={t('settings.countries.title')}
      />
    </div>
  );
}

/** /settings/countries/:id */
export function CountryEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const countryId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: countries, isLoading } = useCountries();
  const country = countries?.find((c) => c.id === countryId);
  const { data: codingRule } = useCodingRule('SETTINGS_COUNTRIES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [isoCode, setIsoCode] = useState('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!country) return;
    setNameAr(country.nameAr);
    setNameEn(country.nameEn);
    setIsoCode(country.isoCode ?? '');
    setIsActive(country.isActive);
  }, [country]);

  const save = useSaveCountry(countryId);
  const remove = useDeleteCountry();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: { nameAr, nameEn, isoCode: isoCode === '' ? null : isoCode, isActive }
      });
      showToast(t('settings.saveSuccess'), 'success');
      if (isNew) navigate(`/settings/countries/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!countryId) return;
    try {
      await remove.mutateAsync(countryId);
      showToast(t('settings.deleteSuccess'), 'success');
      navigate('/settings/countries');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('settings.countries.add') : `${t('settings.countries.title')} — ${country?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/countries') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('settings.countries.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('settings.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (country?.code ?? '')}
                onChange={(e) => setCode(e.target.value)}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('settings.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('settings.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('settings.countries.isoCode')}>
              <Input value={isoCode} onChange={(e) => setIsoCode(e.target.value)} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('settings.isActive')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
