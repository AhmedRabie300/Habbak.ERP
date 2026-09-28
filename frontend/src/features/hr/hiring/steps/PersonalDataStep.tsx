import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useBanks } from '../../../settings/banks/api';
import { useCities } from '../../../settings/cities/api';
import { useCreatePersonalData } from '../../employees/api';
import type { Gender, MaritalStatus } from '../../employees/types';

const GENDERS: Gender[] = ['Male', 'Female'];
const MARITAL_STATUSES: MaritalStatus[] = ['Single', 'Married', 'Divorced', 'Widowed'];

interface FormState {
  nationalId: string;
  bankIban: string;
  bankName: string;
  bankId: number | '';
  cityId: number | '';
  birthDate: string;
  gender: Gender;
  maritalStatus: MaritalStatus;
  address: string;
  phoneNumber: string;
  personalEmail: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
}

/** خطوة 2: بيانات شخصية — نفس حقول PersonalInfoTab وقت الإنشاء الأول (مفيش داعي لزرار "إظهار"
 * PII هنا، الكيان جديد تمامًا ومفيش قيمة قديمة نخفيها). */
export function PersonalDataStep({ employeeId, onComplete }: { employeeId: number; onComplete: () => void }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);

  const { data: banks } = useBanks();
  const bankOptions = useMemo(() => [{ value: '', label: t('hr.employees.noBank') }, ...(banks ?? []).map((b) => ({ value: b.id, label: b.nameAr }))], [banks, t]);
  const { data: cities } = useCities();
  const cityOptions = useMemo(() => [{ value: '', label: t('hr.employees.noCity') }, ...(cities ?? []).map((c) => ({ value: c.id, label: c.nameAr }))], [cities, t]);

  const [form, setForm] = useState<FormState>({
    nationalId: '', bankIban: '', bankName: '', bankId: '', cityId: '', birthDate: '', gender: 'Male',
    maritalStatus: 'Single', address: '', phoneNumber: '', personalEmail: '', emergencyContactName: '', emergencyContactPhone: ''
  });

  const createPersonalData = useCreatePersonalData(employeeId);

  const handleNext = async () => {
    if (!form.nationalId) {
      showToast(t('hr.employees.nationalIdRequired'), 'error');
      return;
    }
    if (!form.birthDate) {
      showToast(t('hr.employees.birthDateRequired'), 'error');
      return;
    }
    try {
      await createPersonalData.mutateAsync({
        nationalId: form.nationalId,
        bankIban: form.bankIban || undefined,
        bankName: form.bankName || undefined,
        bankId: form.bankId === '' ? undefined : Number(form.bankId),
        cityId: form.cityId === '' ? undefined : Number(form.cityId),
        birthDate: form.birthDate,
        gender: form.gender,
        maritalStatus: form.maritalStatus,
        address: form.address || undefined,
        phoneNumber: form.phoneNumber || undefined,
        personalEmail: form.personalEmail || undefined,
        emergencyContactName: form.emergencyContactName || undefined,
        emergencyContactPhone: form.emergencyContactPhone || undefined
      });
      onComplete();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
          <FieldWrapper label={t('hr.employees.nationalId')}>
            <Input value={form.nationalId} onChange={(e) => setForm((f) => ({ ...f, nationalId: e.target.value }))} style={{ minWidth: 180 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.bankIban')}>
            <Input value={form.bankIban} onChange={(e) => setForm((f) => ({ ...f, bankIban: e.target.value }))} style={{ minWidth: 180 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.bankName')}>
            <Input value={form.bankName} onChange={(e) => setForm((f) => ({ ...f, bankName: e.target.value }))} style={{ minWidth: 180 }} />
          </FieldWrapper>
          <FieldWrapper label={t('settings.banks.title')}>
            <SearchableSelect value={form.bankId} onChange={(v) => setForm((f) => ({ ...f, bankId: v === '' ? '' : Number(v) }))} options={bankOptions} style={{ minWidth: 180 }} />
          </FieldWrapper>
          <FieldWrapper label={t('settings.cities.title')}>
            <SearchableSelect value={form.cityId} onChange={(v) => setForm((f) => ({ ...f, cityId: v === '' ? '' : Number(v) }))} options={cityOptions} style={{ minWidth: 180 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.birthDate')}>
            <Input type="date" value={form.birthDate} onChange={(e) => setForm((f) => ({ ...f, birthDate: e.target.value }))} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.gender')}>
            <SearchableSelect value={form.gender} onChange={(v) => setForm((f) => ({ ...f, gender: v as Gender }))} options={GENDERS.map((g) => ({ value: g, label: t(`hr.employees.genders.${g}`) }))} style={{ minWidth: 140 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.maritalStatus')}>
            <SearchableSelect value={form.maritalStatus} onChange={(v) => setForm((f) => ({ ...f, maritalStatus: v as MaritalStatus }))} options={MARITAL_STATUSES.map((m) => ({ value: m, label: t(`hr.employees.maritalStatuses.${m}`) }))} style={{ minWidth: 140 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.address')}>
            <Input value={form.address} onChange={(e) => setForm((f) => ({ ...f, address: e.target.value }))} style={{ minWidth: 220 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.phoneNumber')}>
            <Input value={form.phoneNumber} onChange={(e) => setForm((f) => ({ ...f, phoneNumber: e.target.value }))} style={{ minWidth: 160 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.personalEmail')}>
            <Input value={form.personalEmail} onChange={(e) => setForm((f) => ({ ...f, personalEmail: e.target.value }))} style={{ minWidth: 200 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.emergencyContactName')}>
            <Input value={form.emergencyContactName} onChange={(e) => setForm((f) => ({ ...f, emergencyContactName: e.target.value }))} style={{ minWidth: 180 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.emergencyContactPhone')}>
            <Input value={form.emergencyContactPhone} onChange={(e) => setForm((f) => ({ ...f, emergencyContactPhone: e.target.value }))} style={{ minWidth: 160 }} />
          </FieldWrapper>
        </div>
        <div style={{ marginTop: 16 }}>
          <Button variant="primary" onClick={handleNext}>{t('hr.hiring.next')}</Button>
        </div>
      </CardBody>
    </Card>
  );
}
