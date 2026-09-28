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
import { useBanks, useDeleteBank, useSaveBank, type Bank } from './api';

/** /settings/banks — screen SETTINGS_BANKS. */
export function BanksListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useBanks();

  const rows = toPaged(data, search, (b, q) => `${b.code} ${b.nameAr} ${b.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<Bank>[] = [
    { key: 'code', label: t('settings.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('settings.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'swiftCode', label: t('settings.banks.swiftCode'), render: (r) => r.swiftCode ?? '—', exportValue: (r) => r.swiftCode ?? '' },
    { key: 'isActive', label: t('settings.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('settings.banks.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/settings/banks/new')}>
            {t('settings.banks.add')}
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
        onRowClick={(row) => navigate(`/settings/banks/${row.id}`)}
        exportFileName={t('settings.banks.title')}
      />
    </div>
  );
}

/** /settings/banks/:id */
export function BankEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const bankId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: banks, isLoading } = useBanks();
  const bank = banks?.find((b) => b.id === bankId);
  const { data: codingRule } = useCodingRule('SETTINGS_BANKS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const { data: countries } = useCountries();
  const countryOptions = useMemo(
    () => [{ value: '', label: t('settings.banks.noCountry') }, ...(countries ?? []).map((c) => ({ value: c.id, label: c.nameAr }))],
    [countries, t]
  );

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [swiftCode, setSwiftCode] = useState('');
  const [address, setAddress] = useState('');
  const [countryId, setCountryId] = useState<number | ''>('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!bank) return;
    setNameAr(bank.nameAr);
    setNameEn(bank.nameEn);
    setSwiftCode(bank.swiftCode ?? '');
    setAddress(bank.address ?? '');
    setCountryId(bank.countryId ?? '');
    setIsActive(bank.isActive);
  }, [bank]);

  const save = useSaveBank(bankId);
  const remove = useDeleteBank();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: {
          nameAr,
          nameEn,
          swiftCode: swiftCode === '' ? null : swiftCode,
          address: address === '' ? null : address,
          countryId: countryId === '' ? null : Number(countryId),
          isActive
        }
      });
      showToast(t('settings.saveSuccess'), 'success');
      if (isNew) navigate(`/settings/banks/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!bankId) return;
    try {
      await remove.mutateAsync(bankId);
      showToast(t('settings.deleteSuccess'), 'success');
      navigate('/settings/banks');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('settings.banks.add') : `${t('settings.banks.title')} — ${bank?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/banks') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('settings.banks.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('settings.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (bank?.code ?? '')}
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
            <FieldWrapper label={t('settings.banks.swiftCode')}>
              <Input value={swiftCode} onChange={(e) => setSwiftCode(e.target.value)} style={{ width: 140 }} />
            </FieldWrapper>
            <FieldWrapper label={t('settings.banks.address')}>
              <Input value={address} onChange={(e) => setAddress(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('settings.countries.title')}>
              <SearchableSelect
                value={countryId}
                onChange={(v) => setCountryId(v === '' ? '' : Number(v))}
                options={countryOptions}
                style={{ minWidth: 200 }}
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
