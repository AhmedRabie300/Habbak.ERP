import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../codingRules/api';
import { useCountries } from '../countries/api';
import { toPaged } from '../listing';
import { useCities, useDeleteCity, useSaveCity, type City } from './api';

/** /settings/cities — screen SETTINGS_CITIES. */
export function CitiesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useCities();
  const { data: countries } = useCountries();
  const countryName = (id: number) => countries?.find((c) => c.id === id)?.nameAr ?? '—';

  const rows = toPaged(data, search, (c, q) => `${c.code} ${c.nameAr} ${c.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<City>[] = [
    { key: 'code', label: t('settings.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('settings.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'country', label: t('settings.countries.title'), render: (r) => countryName(r.countryId), exportValue: (r) => countryName(r.countryId) },
    { key: 'isActive', label: t('settings.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('settings.cities.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/settings/cities/new')}>
            {t('settings.cities.add')}
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
        onRowClick={(row) => navigate(`/settings/cities/${row.id}`)}
        exportFileName={t('settings.cities.title')}
      />
    </div>
  );
}

/** /settings/cities/:id */
export function CityEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const cityId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: cities, isLoading } = useCities();
  const city = cities?.find((c) => c.id === cityId);
  const { data: codingRule } = useCodingRule('SETTINGS_CITIES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const { data: countries } = useCountries();
  const countryOptions = useMemo(() => (countries ?? []).map((c) => ({ value: c.id, label: c.nameAr })), [countries]);

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [countryId, setCountryId] = useState<number | ''>('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!city) return;
    setNameAr(city.nameAr);
    setNameEn(city.nameEn);
    setCountryId(city.countryId);
    setIsActive(city.isActive);
  }, [city]);

  const save = useSaveCity(cityId);
  const remove = useDeleteCity();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: { nameAr, nameEn, countryId: Number(countryId), isActive }
      });
      showToast(t('settings.saveSuccess'), 'success');
      if (isNew) navigate(`/settings/cities/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!cityId) return;
    try {
      await remove.mutateAsync(cityId);
      showToast(t('settings.deleteSuccess'), 'success');
      navigate('/settings/cities');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('settings.cities.add') : `${t('settings.cities.title')} — ${city?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/cities') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('settings.cities.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('settings.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (city?.code ?? '')}
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
            <FieldWrapper label={t('settings.countries.title')}>
              <SearchableSelect
                value={countryId}
                onChange={(v) => setCountryId(v === '' ? '' : Number(v))}
                options={countryOptions}
                style={{ minWidth: 220 }}
              />
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
