import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useWarehousesList } from '../warehouses/api';
import { useCustodyOfficersList } from '../custodyOfficers/api';
import { useBranchesList } from '../../organization/branches/api';
import { useApproveBranchRequest, useBranchRequest } from './api';
import { todayLocal } from '../../../lib/date';

interface LineFormValues {
  lineId: number;
  itemLabel: string;
  /** Both quantities are in this unit (the one the branch asked in). */
  unitNameAr?: string;
  requestedQuantity: number;
  approvedQuantity: number;
}

interface FormValues {
  sourceWarehouseId: number | '';
  custodyOfficerId: number | '';
  transferDocumentDate: string;
  lines: LineFormValues[];
}

/** /inventory/branch-requests/:id/approve — screen #13 (02-Module-Inventory-Manufacturing.md, section 5).
 * A one-time terminal decision — ApproveBranchRequestCommand requires every line to carry a decision
 * and computes the request's final status (Approved/PartiallyFulfilled/Rejected) from the totals, so
 * this screen collects all line decisions together rather than allowing iterative partial approval. */
export function BranchRequestApprovalPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const requestId = Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: request, isLoading } = useBranchRequest(requestId);
  const { data: warehouses } = useWarehousesList();
  const { data: custodyOfficers } = useCustodyOfficersList();
  const { data: branches } = useBranchesList();
  const { label } = useFieldLabels('INVENTORY_BRANCH_REQUEST');

  const { control, handleSubmit, register, reset } = useForm<FormValues>({
    defaultValues: { sourceWarehouseId: '', custodyOfficerId: '', transferDocumentDate: todayLocal(), lines: [] }
  });
  const { fields } = useFieldArray({ control, name: 'lines' });

  useEffect(() => {
    if (request) {
      reset({
        sourceWarehouseId: '',
        custodyOfficerId: '',
        transferDocumentDate: todayLocal(),
        lines: request.lines.map((l) => ({
          lineId: l.id ?? 0,
          itemLabel: `${l.itemCode} — ${l.itemNameAr}`,
          unitNameAr: l.unitNameAr,
          requestedQuantity: l.requestedQuantity,
          approvedQuantity: l.requestedQuantity
        }))
      });
    }
  }, [request, reset]);

  const approveMutation = useApproveBranchRequest(requestId);

  const branchName = (branchId?: number) => (branches ?? []).find((b) => b.id === branchId)?.nameAr ?? '';
  const warehouseOptions = (warehouses ?? [])
    .filter((w) => w.warehouseType === 'Main')
    .map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const custodyOfficerOptions = (custodyOfficers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));

  const onApprove = handleSubmit(async (values) => {
    try {
      const result = await approveMutation.mutateAsync({
        sourceWarehouseId: Number(values.sourceWarehouseId),
        custodyOfficerId: Number(values.custodyOfficerId),
        transferDocumentDate: values.transferDocumentDate,
        lines: values.lines.map((l) => ({ lineId: l.lineId, approvedQuantity: Number(l.approvedQuantity) }))
      });
      showToast(t('branchRequests.approveSuccessStatus', { status: t(`branchRequests.status${result.status}`) }), 'success');
      navigate('/inventory/branch-requests');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  if (isLoading || !request) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('branchRequests.approveTitle')} — {request.requestNumber}</h2>
        <StatusBadge status={request.status} />
      </div>

      <ActionBar
        primary={{ key: 'approve', label: t('branchRequests.approve'), onClick: () => onApprove() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/branch-requests') }]}
      />

      <form onSubmit={onApprove} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('branch', t('branchRequests.branch'))}>
                <Input value={branchName(request.branchId)} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('requestDate', t('branchRequests.requestDate'))}>
                <Input value={request.requestDate} disabled />
              </FieldWrapper>
              <FieldWrapper label={t('branchRequests.sourceWarehouse')}>
                <Controller
                  control={control}
                  name="sourceWarehouseId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={t('branchRequests.custodyOfficer')}>
                <Controller
                  control={control}
                  name="custodyOfficerId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={custodyOfficerOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={t('branchRequests.transferDocumentDate')}>
                <Input type="date" {...register('transferDocumentDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('branchRequests.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('branchRequests.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('requestedQuantity', t('branchRequests.requestedQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('approvedQuantity', t('branchRequests.approvedQuantity'))}</th>
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td style={{ padding: 8 }}>{field.itemLabel}</td>
                      <td style={{ padding: 8 }}>{field.unitNameAr ?? '—'}</td>
                      <td style={{ padding: 8 }}>{field.requestedQuantity}</td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" min={0} max={field.requestedQuantity} style={{ width: 110 }} {...register(`lines.${index}.approvedQuantity` as const, { valueAsNumber: true })} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p style={{ color: 'var(--color-text-muted)', fontSize: 13, marginTop: 8 }}>{t('branchRequests.approveHint')}</p>
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
