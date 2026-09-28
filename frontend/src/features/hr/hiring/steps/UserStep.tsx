import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useUsersList } from '../../../settings/security/api';
import { useAssignUserToEmployee } from '../../employees/api';

/** خطوة 5 (اختيارية) — ربط مستخدم بالموظف الجديد، أو تخطيها وربطه لاحقًا من شاشة الموظف نفسها. */
export function UserStep({ employeeId, onFinish }: { employeeId: number; onFinish: () => void }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: users } = useUsersList('', '');
  const [userId, setUserId] = useState<number | ''>('');
  const assignUser = useAssignUserToEmployee(employeeId);

  const userOptions = useMemo(() => (users ?? []).map((u) => ({ value: u.id, label: `${u.username} — ${u.fullName}` })), [users]);

  const handleFinish = async () => {
    if (userId === '') {
      onFinish();
      return;
    }
    try {
      await assignUser.mutateAsync(Number(userId));
      showToast(t('hr.employees.assignUserSuccess'), 'success');
      onFinish();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <FieldWrapper label={t('hr.employees.linkedUser')}>
            <SearchableSelect value={userId} onChange={(v) => setUserId(v === '' ? '' : Number(v))} options={userOptions} style={{ minWidth: 260 }} />
          </FieldWrapper>
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
          <Button variant="primary" onClick={handleFinish}>{userId === '' ? t('hr.hiring.skipAndFinish') : t('hr.hiring.assignAndFinish')}</Button>
        </div>
      </CardBody>
    </Card>
  );
}
