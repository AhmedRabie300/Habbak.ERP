import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useButtonPermission } from '../../../auth/access';
import { useBanks } from '../../../settings/banks/api';
import { useCities } from '../../../settings/cities/api';
import { useCreatePersonalData, useRevealPiiField, useUpdatePersonalData } from '../api';
import type { EmployeeDetail, Gender, MaritalStatus, PersonalDataInput } from '../types';

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

const emptyForm = (): FormState => ({
  nationalId: '', bankIban: '', bankName: '', bankId: '', cityId: '', birthDate: '', gender: 'Male',
  maritalStatus: 'Single', address: '', phoneNumber: '', personalEmail: '', emergencyContactName: '', emergencyContactPhone: ''
});

interface PersonalInfoTabProps {
  employeeId: number;
  employee: EmployeeDetail | null;
}

/**
 * تبويب "شخصية" — البيانات الحسّاسة (PII) بنمط Last4 + زرار إظهار محكوم بصلاحية زرار مستقلة
 * (`[ScreenButton("HR_EMPLOYEES", "RevealPii")]`، Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5).
 *
 * **حقول اتشالت من النموذج عمدًا**: `NationalityId`/`MilitaryStatusId`/`QualificationTypeId`/
 * `EmergencyContactRelationshipTypeId` — الكيانات دي مسجّلة "Seed Only بدون شاشة في هذه المرحلة"
 * أصلًا في `HR-Core-Plan.md §1.5` (قرار سابق قبل هذا الـ Sub-Batch)، ومفيش حتى Endpoint قراءة واحد
 * لأي منهم (اتأكد بالفحص الفعلي) — نفس منطق "بدون شاشة" اتمد هنا لـ"بدون Picker" لغاية ما توصل
 * مرحلتها. الحقول التانية اللي ليها شاشة جاهزة فعلًا (`BankId` من 1.5.2، `CityId` من 1.5.2) معروضة عادي.
 */
export function PersonalInfoTab({ employeeId, employee }: PersonalInfoTabProps) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const canRevealPii = useButtonPermission('HR_EMPLOYEES', 'RevealPii');

  const personalData = employee?.personalData ?? null;
  const hasPersonalData = personalData !== null;

  const { data: banks } = useBanks();
  const bankOptions = useMemo(() => [{ value: '', label: t('hr.employees.noBank') }, ...(banks ?? []).map((b) => ({ value: b.id, label: b.nameAr }))], [banks, t]);

  const { data: cities } = useCities();
  const cityOptions = useMemo(() => [{ value: '', label: t('hr.employees.noCity') }, ...(cities ?? []).map((c) => ({ value: c.id, label: c.nameAr }))], [cities, t]);

  const [form, setForm] = useState<FormState>(emptyForm());
  const [revealedNationalId, setRevealedNationalId] = useState<string | null>(null);
  const [revealedBankIban, setRevealedBankIban] = useState<string | null>(null);

  useEffect(() => {
    if (!personalData) return;
    setForm({
      nationalId: '', bankIban: '', bankName: personalData.bankName ?? '', bankId: personalData.bankId ?? '',
      cityId: personalData.cityId ?? '', birthDate: personalData.birthDate ?? '', gender: personalData.gender ?? 'Male',
      maritalStatus: personalData.maritalStatus ?? 'Single', address: personalData.address ?? '',
      phoneNumber: personalData.phoneNumber ?? '', personalEmail: personalData.personalEmail ?? '',
      emergencyContactName: personalData.emergencyContactName ?? '', emergencyContactPhone: personalData.emergencyContactPhone ?? ''
    });
    setRevealedNationalId(null);
    setRevealedBankIban(null);
  }, [personalData]);

  const createPersonalData = useCreatePersonalData(employeeId);
  const updatePersonalData = useUpdatePersonalData(employeeId);
  const revealPii = useRevealPiiField();

  const handleSave = async () => {
    // Both Create and Update require the real NationalId every time (Update's validator re-checks
    // the 14-digit format on whatever is sent) — the field only ever shows the Last4 placeholder or
    // a freshly revealed/typed value, never a stand-in, so re-saving without entering it is refused
    // client-side rather than silently resending a masked placeholder that would fail validation.
    if (!form.nationalId) {
      showToast(t(hasPersonalData ? 'hr.employees.nationalIdRequiredForUpdate' : 'hr.employees.nationalIdRequired'), 'error');
      return;
    }
    // BirthDate is required by CreateEmployeePersonalDataCommand/UpdateEmployeePersonalDataCommand
    // (DateOnly, non-nullable) — an empty string fails JSON model binding with a raw ASP.NET error
    // before it ever reaches FluentValidation, so this is caught client-side with a normal message.
    if (!form.birthDate) {
      showToast(t('hr.employees.birthDateRequired'), 'error');
      return;
    }
    // Same reasoning for the IBAN: Update overwrites every field it's sent (no partial-update
    // semantics on this endpoint), so an untouched masked IBAN must never be resubmitted blank —
    // that would silently erase it. Only guard when one is actually on file; a brand-new employee
    // with no IBAN yet is free to leave the field empty.
    if (personalData?.bankIbanLast4 && !form.bankIban) {
      showToast(t('hr.employees.bankIbanRequiredForUpdate'), 'error');
      return;
    }

    const data: PersonalDataInput = {
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
    };

    try {
      if (hasPersonalData) {
        await updatePersonalData.mutateAsync(data);
      } else {
        await createPersonalData.mutateAsync(data);
      }
      showToast(t('hr.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleReveal = async (fieldName: 'NationalIdEncrypted' | 'BankIbanEncrypted') => {
    if (!personalData) return;
    try {
      const result = await revealPii.mutateAsync({ entityId: personalData.id, fieldName });
      if (fieldName === 'NationalIdEncrypted') setRevealedNationalId(result.value);
      else setRevealedBankIban(result.value);
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
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <Input
                value={revealedNationalId ?? form.nationalId}
                placeholder={hasPersonalData ? `••••••••${personalData!.nationalIdLast4}` : undefined}
                onChange={(e) => { setForm((f) => ({ ...f, nationalId: e.target.value })); setRevealedNationalId(null); }}
                style={{ minWidth: 180 }}
              />
              {hasPersonalData && canRevealPii && !revealedNationalId && (
                <Button variant="secondary" size="sm" onClick={() => handleReveal('NationalIdEncrypted')}>{t('hr.employees.reveal')}</Button>
              )}
            </div>
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.bankIban')}>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <Input
                value={revealedBankIban ?? form.bankIban}
                placeholder={personalData?.bankIbanLast4 ? `••••••••${personalData.bankIbanLast4}` : undefined}
                onChange={(e) => { setForm((f) => ({ ...f, bankIban: e.target.value })); setRevealedBankIban(null); }}
                style={{ minWidth: 180 }}
              />
              {personalData?.bankIbanLast4 && canRevealPii && !revealedBankIban && (
                <Button variant="secondary" size="sm" onClick={() => handleReveal('BankIbanEncrypted')}>{t('hr.employees.reveal')}</Button>
              )}
            </div>
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
        {!hasPersonalData && <p style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('hr.employees.nationalIdHint')}</p>}
        {hasPersonalData && <p style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('hr.employees.nationalIdUpdateHint')}</p>}
        <div style={{ marginTop: 16 }}>
          <Button variant="primary" onClick={handleSave}>{t('common.saveChanges')}</Button>
        </div>
      </CardBody>
    </Card>
  );
}
