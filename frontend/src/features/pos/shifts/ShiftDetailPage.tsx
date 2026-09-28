import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useUserNameOf } from '../../settings/security/UserName';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useApproveShiftClose, useShift } from './api';

/** /pos/shifts/:id — عرض للقراءة فقط لوردية منتهية أو قيد الاعتماد، مع زر اعتماد الإغلاق لو
 * PendingCloseApproval (قاعدة 5). فتح/إغلاق فعلي بيتم من /pos/shift-console. */
export function ShiftDetailPage() {
  const { t } = useTranslation();
  const nameOf = useUserNameOf();
  const { id } = useParams();
  const shiftId = Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: shift, isLoading } = useShift(shiftId);
  const approveMutation = useApproveShiftClose(shiftId);

  const handleApprove = async () => {
    try {
      await approveMutation.mutateAsync();
      showToast(t('shifts.approveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading || !shift) {
    return <div>{t('common.loading')}</div>;
  }

  const openingCounts = shift.denominationCounts.filter((c) => c.countType === 'Opening');
  const closingCounts = shift.denominationCounts.filter((c) => c.countType === 'Closing');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{shift.posTerminalNameAr} — {t('shifts.title')} #{shift.id}</h2>
        <StatusBadge status={shift.status} />
      </div>

      <ActionBar
        primary={shift.status === 'PendingCloseApproval' ? { key: 'approve', buttonCode: 'ApproveClose', label: t('shifts.approveClose'), onClick: handleApprove } : undefined}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/pos/shifts') }]}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('shifts.cashierUserId')}>
              <Input value={nameOf(shift.cashierUserId)} disabled />
            </FieldWrapper>
            <FieldWrapper label={t('shifts.openedAt')}>
              <Input value={new Date(shift.openedAtUtc).toLocaleString()} disabled />
            </FieldWrapper>
            {shift.closedAtUtc && (
              <FieldWrapper label={t('shifts.closedAt')}>
                <Input value={new Date(shift.closedAtUtc).toLocaleString()} disabled />
              </FieldWrapper>
            )}
            <FieldWrapper label={t('shifts.openingCashAmount')}>
              <Input value={shift.openingCashAmount.toFixed(2)} disabled />
            </FieldWrapper>
            {shift.expectedClosingCashAmount != null && (
              <FieldWrapper label={t('shifts.expectedClosingCashAmount')}>
                <Input value={shift.expectedClosingCashAmount.toFixed(2)} disabled />
              </FieldWrapper>
            )}
            {shift.actualClosingCashAmount != null && (
              <FieldWrapper label={t('shifts.actualClosingCashAmount')}>
                <Input value={shift.actualClosingCashAmount.toFixed(2)} disabled />
              </FieldWrapper>
            )}
            {shift.differenceAmount != null && (
              <FieldWrapper label={t('shifts.differenceAmount')}>
                <Input value={shift.differenceAmount.toFixed(2)} disabled />
              </FieldWrapper>
            )}
          </div>

          <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginTop: 20 }}>
            <div>
              <h3 style={{ margin: '0 0 8px' }}>{t('shifts.openingCounts')}</h3>
              <DenominationTable rows={openingCounts} />
            </div>
            {closingCounts.length > 0 && (
              <div>
                <h3 style={{ margin: '0 0 8px' }}>{t('shifts.closingCounts')}</h3>
                <DenominationTable rows={closingCounts} />
              </div>
            )}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

function DenominationTable({ rows }: { rows: { denominationValue: number; count: number }[] }) {
  const { t } = useTranslation();
  return (
    <table style={{ fontSize: 13, borderCollapse: 'collapse' }}>
      <thead>
        <tr>
          <th style={{ textAlign: 'start', padding: 8 }}>{t('shifts.denominationValue')}</th>
          <th style={{ textAlign: 'start', padding: 8 }}>{t('shifts.count')}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr key={r.denominationValue}>
            <td style={{ padding: 8 }}>{r.denominationValue.toFixed(2)}</td>
            <td style={{ padding: 8 }}>{r.count}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
