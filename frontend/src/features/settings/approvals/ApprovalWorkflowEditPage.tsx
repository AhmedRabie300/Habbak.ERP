import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller, type Control } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { Button } from '../../../ui-kit/Button';
import { Icon } from '../../../ui-kit/Icon';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useEmployeesLookup } from '../../hr/employees/api';
import { useJobGrades } from '../../hr/jobGrades/api';
import { useRolesList } from '../security/api';
import { useApprovalWorkflow, useCreateApprovalWorkflow, useSetApprovalWorkflowActive, useUpdateApprovalWorkflow } from './api';
import type { ApproverType, ApprovalWorkflowStepInput, StepMode } from './types';

interface FormValues {
  code: string;
  nameAr: string;
  nameEn: string;
  steps: ApprovalWorkflowStepInput[];
}

const emptyStep = (order: number): ApprovalWorkflowStepInput => ({
  stepOrder: order,
  mode: 'AnyOne',
  approvers: [{ approverType: 'DirectManager', approverReferenceId: null }]
});

/** /settings/approval-workflows/:id — SETTINGS_APPROVAL_WORKFLOWS edit screen (00-Project-Overview.md §12). */
export function ApprovalWorkflowEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const workflowId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: workflow, isLoading } = useApprovalWorkflow(workflowId);
  const createMutation = useCreateApprovalWorkflow();
  const updateMutation = useUpdateApprovalWorkflow(workflowId);
  const setActiveMutation = useSetApprovalWorkflowActive();

  const { control, register, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    defaultValues: { code: '', nameAr: '', nameEn: '', steps: [emptyStep(1)] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'steps' });

  useEffect(() => {
    if (workflow) {
      reset({
        code: workflow.code,
        nameAr: workflow.nameAr,
        nameEn: workflow.nameEn,
        steps: workflow.steps.map((s) => ({
          stepOrder: s.stepOrder,
          mode: s.mode,
          approvers: s.approvers.map((a) => ({ approverType: a.approverType, approverReferenceId: a.approverReferenceId }))
        }))
      });
    }
  }, [workflow, reset]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      if (isNew) {
        const result = await createMutation.mutateAsync(values);
        showToast(t('approvals.workflows.saveSuccess'), 'success');
        navigate(`/settings/approval-workflows/${result.id}`);
      } else {
        const result = await updateMutation.mutateAsync(values);
        showToast(
          result.isNewVersion ? t('approvals.workflows.newVersionCreated', { version: result.versionNumber }) : t('approvals.workflows.saveSuccess'),
          'success'
        );
        if (result.isNewVersion) navigate(`/settings/approval-workflows/${result.id}`);
      }
    } catch (err) {
      const message = getFieldErrorMessage(err); if (message) showToast(message, 'error');
    }
  });

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 10 }}>
          {isNew ? t('approvals.workflows.add') : workflow?.nameAr}
          {workflow && <StatusBadge status={workflow.isActive ? 'Active' : 'Inactive'} />}
          {workflow && <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('approvals.workflows.version', { version: workflow.versionNumber })}</span>}
        </h2>
      </div>

      <Card>
        <CardBody style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
            <FieldWrapper label={t('approvals.workflows.code')} error={errors.code?.message}>
              <Input {...register('code', { required: true })} disabled={!isNew} />
            </FieldWrapper>
            <FieldWrapper label={t('approvals.workflows.nameAr')} error={errors.nameAr?.message}>
              <Input {...register('nameAr', { required: true })} />
            </FieldWrapper>
            <FieldWrapper label={t('approvals.workflows.nameEn')} error={errors.nameEn?.message}>
              <Input {...register('nameEn', { required: true })} />
            </FieldWrapper>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ margin: 0, fontSize: 15 }}>{t('approvals.workflows.steps')}</h3>
              <Button variant="secondary" onClick={() => append(emptyStep(fields.length + 1))}>
                <Icon name="plus" size={14} />
                {t('approvals.workflows.addStep')}
              </Button>
            </div>

            {fields.map((field, index) => (
              <StepEditor key={field.id} control={control} register={register} stepIndex={index} onRemove={() => remove(index)} canRemove={fields.length > 1} />
            ))}
          </div>
        </CardBody>
      </Card>

      <ActionBar
        primary={{ key: 'save', label: t('common.save'), onClick: onSubmit }}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/settings/approval-workflows') },
          ...(workflow
            ? [{
                key: 'toggleActive',
                label: workflow.isActive ? t('approvals.common.deactivate') : t('approvals.common.activate'),
                onClick: () => setActiveMutation.mutate({ id: workflow.id, isActive: !workflow.isActive })
              }]
            : [])
        ]}
      />
    </div>
  );
}

function StepEditor({
  control, register, stepIndex, onRemove, canRemove
}: {
  control: Control<FormValues>;
  register: ReturnType<typeof useForm<FormValues>>['register'];
  stepIndex: number;
  onRemove: () => void;
  canRemove: boolean;
}) {
  const { t } = useTranslation();
  const { fields, append, remove } = useFieldArray({ control, name: `steps.${stepIndex}.approvers` });
  const { data: employees } = useEmployeesLookup();
  const { data: jobGrades } = useJobGrades();
  const { data: roles } = useRolesList();

  const approverTypeOptions = [
    { value: 'SpecificEmployee', label: t('approvals.approverType.SpecificEmployee') },
    { value: 'Role', label: t('approvals.approverType.Role') },
    { value: 'DirectManager', label: t('approvals.approverType.DirectManager') },
    { value: 'JobGrade', label: t('approvals.approverType.JobGrade') }
  ];

  const referenceOptionsFor = (type: ApproverType) => {
    if (type === 'SpecificEmployee') return (employees ?? []).map((e) => ({ value: e.id, label: e.nameAr }));
    if (type === 'Role') return (roles ?? []).map((r) => ({ value: r.id, label: r.nameAr }));
    if (type === 'JobGrade') return (jobGrades ?? []).map((g) => ({ value: g.id, label: g.nameAr }));
    return [];
  };

  return (
    <div style={{ border: '1px solid var(--color-border)', borderRadius: 'var(--radius-lg)', padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end' }}>
        <FieldWrapper label={t('approvals.workflows.stepOrder')}>
          <Input type="number" style={{ width: 90 }} {...register(`steps.${stepIndex}.stepOrder`, { valueAsNumber: true, required: true, min: 1 })} />
        </FieldWrapper>
        <FieldWrapper label={t('approvals.workflows.mode')}>
          <Controller
            control={control}
            name={`steps.${stepIndex}.mode`}
            render={({ field }) => (
              <SearchableSelect
                value={field.value}
                onChange={(v) => field.onChange(v as StepMode)}
                options={[
                  { value: 'AnyOne', label: t('approvals.stepMode.AnyOne') },
                  { value: 'All', label: t('approvals.stepMode.All') }
                ]}
                style={{ width: 160 }}
              />
            )}
          />
        </FieldWrapper>
        {canRemove && (
          <Button variant="ghost" onClick={onRemove} style={{ marginInlineStart: 'auto' }}>
            <Icon name="trash" size={14} />
            {t('approvals.workflows.removeStep')}
          </Button>
        )}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('approvals.workflows.approvers')}</span>
        {fields.map((field, approverIndex) => (
          <ApproverRow
            key={field.id}
            control={control}
            stepIndex={stepIndex}
            approverIndex={approverIndex}
            approverTypeOptions={approverTypeOptions}
            referenceOptionsFor={referenceOptionsFor}
            onRemove={() => remove(approverIndex)}
            canRemove={fields.length > 1}
          />
        ))}
        <Button variant="secondary" onClick={() => append({ approverType: 'DirectManager', approverReferenceId: null })} style={{ alignSelf: 'flex-start' }}>
          <Icon name="plus" size={14} />
          {t('approvals.workflows.addApprover')}
        </Button>
      </div>
    </div>
  );
}

function ApproverRow({
  control, stepIndex, approverIndex, approverTypeOptions, referenceOptionsFor, onRemove, canRemove
}: {
  control: Control<FormValues>;
  stepIndex: number;
  approverIndex: number;
  approverTypeOptions: { value: string; label: string }[];
  referenceOptionsFor: (type: ApproverType) => { value: number; label: string }[];
  onRemove: () => void;
  canRemove: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Controller
      control={control}
      name={`steps.${stepIndex}.approvers.${approverIndex}`}
      render={({ field }) => (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <SearchableSelect
            value={field.value.approverType}
            onChange={(v) => field.onChange({ approverType: v as ApproverType, approverReferenceId: v === 'DirectManager' ? null : field.value.approverReferenceId })}
            options={approverTypeOptions}
            style={{ width: 180 }}
          />
          {field.value.approverType !== 'DirectManager' && (
            <SearchableSelect
              value={field.value.approverReferenceId ?? undefined}
              onChange={(v) => field.onChange({ ...field.value, approverReferenceId: Number(v) })}
              options={referenceOptionsFor(field.value.approverType)}
              placeholder={t('approvals.common.select')}
              style={{ width: 220 }}
            />
          )}
          {canRemove && (
            <Button variant="ghost" onClick={onRemove}>
              <Icon name="x" size={14} />
            </Button>
          )}
        </div>
      )}
    />
  );
}
