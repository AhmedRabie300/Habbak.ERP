import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../ui-kit/DataGrid';
import { Badge } from '../../ui-kit/Badge';
import { Button } from '../../ui-kit/Button';
import { ActionBar } from '../../ui-kit/ActionBar';
import { Card, CardBody } from '../../ui-kit/Card';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { SearchableSelect } from '../../ui-kit/SearchableSelect';
import { usePermission } from '../../ui-kit/usePermission';
import { useToastStore } from '../../store/toastStore';
import { getFieldErrorMessage } from '../../app/api';
import { useBranchesList } from '../organization/branches/api';
import { useSuppliersList } from '../purchasing/suppliers/api';
import { useItemsList } from '../inventory/items/api';
import { useWarehousesList } from '../inventory/warehouses/api';
import { useCodingRule } from '../settings/codingRules/api';
import { useFixedAssets } from '../fixedAssets/api';
import { money, toPaged, today, usePostableAccountOptions } from '../fixedAssets/listing';
import {
  useCreateMaintenanceIssue,
  useDeleteMaintenanceCategory,
  useDeleteMaintenanceSchedule,
  useMaintenanceCategories,
  useMaintenanceIssueAction,
  useMaintenanceIssues,
  useMaintenanceRequest,
  useMaintenanceRequestAction,
  useMaintenanceRequests,
  useMaintenanceSchedules,
  useSaveMaintenanceCategory,
  useSaveMaintenanceRequest,
  useSaveMaintenanceSchedule,
  type IssueSeverity,
  type MaintenanceCategory,
  type MaintenanceFrequency,
  type MaintenanceIssue,
  type MaintenanceIssueStatus,
  type MaintenanceRequestListItem,
  type MaintenanceRequestStatus,
  type MaintenanceSchedule,
  type MaintenanceSparePartInput,
  type MaintenanceType
} from './api';

const issueTone = {
  Reported: 'warning',
  UnderInspection: 'info',
  Repairing: 'info',
  Repaired: 'success',
  Rejected: 'error',
  Cancelled: 'neutral'
} as const;

export const requestTone = {
  Draft: 'neutral',
  Approved: 'info',
  InProgress: 'warning',
  Completed: 'success',
  Rejected: 'error',
  Cancelled: 'neutral'
} as const;

// ================================================================== categories (screen #8)

/** /maintenance/categories — the kinds of maintenance work (preventive, corrective, inspection). */
export function MaintenanceCategoriesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useMaintenanceCategories();

  const rows = toPaged(data, search, (c, q) => `${c.code} ${c.nameAr} ${c.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<MaintenanceCategory>[] = [
    { key: 'code', label: t('maintenance.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('maintenance.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    {
      key: 'type',
      label: t('maintenance.type'),
      render: (r) => t(`maintenance.types.${r.maintenanceType}`),
      exportValue: (r) => t(`maintenance.types.${r.maintenanceType}`)
    },
    {
      key: 'isActive',
      label: t('maintenance.isActive'),
      render: (r) => (r.isActive ? t('common.yes') : t('common.no')),
      exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no'))
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('maintenance.categoriesTitle')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/maintenance/categories/new')}>
            {t('maintenance.addCategory')}
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
        onRowClick={(row) => navigate(`/maintenance/categories/${row.id}`)}
        exportFileName={t('maintenance.categoriesTitle')}
      />
    </div>
  );
}

export function MaintenanceCategoryEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const categoryId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: categories } = useMaintenanceCategories();
  const category = categories?.find((c) => c.id === categoryId);
  const { data: codingRule } = useCodingRule('MAINTENANCE_CATEGORIES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [maintenanceType, setMaintenanceType] = useState<MaintenanceType>('Corrective');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!category) return;
    setNameAr(category.nameAr);
    setNameEn(category.nameEn);
    setMaintenanceType(category.maintenanceType);
    setIsActive(category.isActive);
  }, [category]);

  const save = useSaveMaintenanceCategory(categoryId);
  const remove = useDeleteMaintenanceCategory();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        nameAr,
        nameEn,
        maintenanceType,
        isActive
      });
      showToast(t('maintenance.saveSuccess'), 'success');
      if (isNew) navigate(`/maintenance/categories/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!categoryId) return;
    try {
      await remove.mutateAsync(categoryId);
      showToast(t('maintenance.deleteSuccess'), 'success');
      navigate('/maintenance/categories');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('maintenance.addCategory') : `${t('maintenance.categoriesTitle')} — ${category?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/maintenance/categories') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('maintenance.deleteCategoryConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('maintenance.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (category?.code ?? '')}
                onChange={(e) => setCode(e.target.value)}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.type')}>
              <SearchableSelect
                value={maintenanceType}
                onChange={(v) => setMaintenanceType(v as MaintenanceType)}
                options={(['Preventive', 'Corrective', 'Inspection'] as MaintenanceType[]).map((m) => ({ value: m, label: t(`maintenance.types.${m}`) }))}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.isActive')}>
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

// ================================================================== fault reports (screen #9)

/** /maintenance/issues — anyone reports a fault, on a registered asset or any other device. */
export function MaintenanceIssuesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<MaintenanceIssueStatus | ''>('');
  const { data, isLoading } = useMaintenanceIssues(status === '' ? undefined : status);

  const rows = toPaged(data, search, (i, q) =>
    `${i.issueNumber} ${i.assetNumber ?? ''} ${i.assetNameAr ?? ''} ${i.deviceName ?? ''} ${i.description}`.toLowerCase().includes(q)
  );

  const columns: DataGridColumn<MaintenanceIssue>[] = [
    { key: 'number', label: t('maintenance.issueNumber'), render: (r) => r.issueNumber, exportValue: (r) => r.issueNumber },
    {
      key: 'device',
      label: t('maintenance.device'),
      render: (r) => (r.fixedAssetId ? `${r.assetNumber} — ${r.assetNameAr}` : (r.deviceName ?? '—')),
      exportValue: (r) => r.assetNumber ?? r.deviceName ?? ''
    },
    { key: 'description', label: t('maintenance.description'), render: (r) => r.description, exportValue: (r) => r.description },
    {
      key: 'severity',
      label: t('maintenance.severity'),
      render: (r) => t(`maintenance.severities.${r.severity}`),
      exportValue: (r) => t(`maintenance.severities.${r.severity}`)
    },
    { key: 'reported', label: t('maintenance.reportedAt'), render: (r) => r.reportedAtUtc.slice(0, 10), exportValue: (r) => r.reportedAtUtc },
    {
      key: 'status',
      label: t('maintenance.status'),
      render: (r) => <Badge label={t(`maintenance.issueStatuses.${r.status}`)} tone={issueTone[r.status]} />,
      exportValue: (r) => t(`maintenance.issueStatuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{t('maintenance.issuesTitle')}</h2>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <SearchableSelect
            value={status}
            onChange={(v) => setStatus(v === '' ? '' : (v as MaintenanceIssueStatus))}
            options={[
              { value: '', label: t('maintenance.allStatuses') },
              ...(['Reported', 'UnderInspection', 'Repairing', 'Repaired', 'Rejected', 'Cancelled'] as MaintenanceIssueStatus[]).map((s) => ({
                value: s,
                label: t(`maintenance.issueStatuses.${s}`)
              }))
            ]}
            style={{ minWidth: 180 }}
          />
          {canAdd && (
            <Button variant="primary" onClick={() => navigate('/maintenance/issues/new')}>
              {t('maintenance.addIssue')}
            </Button>
          )}
        </div>
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/maintenance/issues/${row.id}`)}
        exportFileName={t('maintenance.issuesTitle')}
      />
    </div>
  );
}

export function MaintenanceIssueEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: issues } = useMaintenanceIssues();
  const issue = isNew ? undefined : issues?.find((i) => i.id === Number(id));
  const { data: assets } = useFixedAssets();
  const { data: branches } = useBranchesList();

  const [fixedAssetId, setFixedAssetId] = useState<number | ''>('');
  const [branchId, setBranchId] = useState<number | ''>('');
  const [deviceName, setDeviceName] = useState('');
  const [description, setDescription] = useState('');
  const [severity, setSeverity] = useState<IssueSeverity>('Medium');
  const [notes, setNotes] = useState('');

  const create = useCreateMaintenanceIssue();
  const act = useMaintenanceIssueAction();

  const handleCreate = async () => {
    try {
      const result = await create.mutateAsync({
        branchId: branchId === '' ? null : Number(branchId),
        fixedAssetId: fixedAssetId === '' ? null : Number(fixedAssetId),
        deviceName: deviceName || null,
        description,
        severity,
        notes: notes || null
      });
      showToast(t('maintenance.saveSuccess'), 'success');
      navigate(`/maintenance/issues/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const perform = async (action: 'inspect' | 'reject' | 'cancel') => {
    if (!issue) return;
    try {
      await act.mutateAsync({ id: issue.id, action });
      showToast(t('maintenance.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const open = issue?.status === 'Reported' || issue?.status === 'UnderInspection';
  const serviceableAssets = (assets ?? []).filter((a) => a.status !== 'Draft' && a.status !== 'Disposed' && a.status !== 'WrittenOff');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 760 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('maintenance.addIssue') : `${t('maintenance.issuesTitle')} — ${issue?.issueNumber ?? ''}`}</h2>
        {issue && <Badge label={t(`maintenance.issueStatuses.${issue.status}`)} tone={issueTone[issue.status]} />}
      </div>

      <ActionBar
        primary={isNew ? { key: 'save', label: t('common.saveDraft'), onClick: handleCreate } : undefined}
        secondary={[
          ...(open ? [{ key: 'inspect', label: t('maintenance.markUnderInspection'), onClick: () => perform('inspect') }] : []),
          ...(open && issue?.fixedAssetId
            ? [
                {
                  key: 'createRequest',
                  label: t('maintenance.createRequestFromIssue'),
                  onClick: () => navigate(`/maintenance/requests/new?issueId=${issue.id}&assetId=${issue.fixedAssetId}`)
                }
              ]
            : []),
          ...(open ? [{ key: 'reject', label: t('common.reject'), onClick: () => perform('reject') }] : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/maintenance/issues') }
        ]}
        destructive={open ? [{ key: 'cancel', label: t('common.cancel'), onClick: () => perform('cancel'), confirmMessage: t('maintenance.cancelConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('maintenance.asset')}>
              {isNew ? (
                <SearchableSelect
                  value={fixedAssetId}
                  onChange={(v) => setFixedAssetId(v === '' ? '' : Number(v))}
                  options={[
                    { value: '', label: t('maintenance.otherDevice') },
                    ...serviceableAssets.map((a) => ({ value: a.id, label: `${a.assetNumber} — ${a.nameAr}` }))
                  ]}
                  style={{ minWidth: 260 }}
                />
              ) : (
                <Input value={issue?.fixedAssetId ? `${issue.assetNumber} — ${issue.assetNameAr}` : (issue?.deviceName ?? '')} disabled style={{ minWidth: 260 }} />
              )}
            </FieldWrapper>
            {isNew && fixedAssetId === '' && (
              <>
                <FieldWrapper label={t('maintenance.deviceName')}>
                  <Input value={deviceName} onChange={(e) => setDeviceName(e.target.value)} style={{ minWidth: 200 }} />
                </FieldWrapper>
                <FieldWrapper label={t('maintenance.branch')}>
                  <SearchableSelect
                    value={branchId}
                    onChange={(v) => setBranchId(v === '' ? '' : Number(v))}
                    options={[{ value: '', label: t('maintenance.none') }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))]}
                    style={{ minWidth: 200 }}
                  />
                </FieldWrapper>
              </>
            )}
            <FieldWrapper label={t('maintenance.severity')}>
              <SearchableSelect
                value={isNew ? severity : (issue?.severity ?? '')}
                onChange={(v) => setSeverity(v as IssueSeverity)}
                options={(['Low', 'Medium', 'High', 'Critical'] as IssueSeverity[]).map((s) => ({ value: s, label: t(`maintenance.severities.${s}`) }))}
                disabled={!isNew}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.description')}>
              <Input
                value={isNew ? description : (issue?.description ?? '')}
                onChange={(e) => setDescription(e.target.value)}
                disabled={!isNew}
                style={{ minWidth: 420 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.notes')}>
              <Input value={isNew ? notes : (issue?.notes ?? '')} onChange={(e) => setNotes(e.target.value)} disabled={!isNew} style={{ minWidth: 300 }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

// ================================================================== requests (screen #10)

/** /maintenance/requests — the job itself: labor, spare parts, and what it actually cost. */
export function MaintenanceRequestsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<MaintenanceRequestStatus | ''>('');
  const { data, isLoading } = useMaintenanceRequests(status === '' ? undefined : { status });

  const rows = toPaged(data, search, (r, q) => `${r.requestNumber} ${r.assetNumber} ${r.assetNameAr} ${r.technicianName ?? ''}`.toLowerCase().includes(q));

  const columns: DataGridColumn<MaintenanceRequestListItem>[] = [
    { key: 'number', label: t('maintenance.requestNumber'), render: (r) => r.requestNumber, exportValue: (r) => r.requestNumber },
    { key: 'asset', label: t('maintenance.asset'), render: (r) => `${r.assetNumber} — ${r.assetNameAr}`, exportValue: (r) => r.assetNumber },
    { key: 'category', label: t('maintenance.category'), render: (r) => r.categoryNameAr, exportValue: (r) => r.categoryNameAr },
    { key: 'requestDate', label: t('maintenance.requestDate'), render: (r) => r.requestDate, exportValue: (r) => r.requestDate },
    { key: 'technician', label: t('maintenance.technician'), render: (r) => r.technicianName ?? '—', exportValue: (r) => r.technicianName ?? '' },
    { key: 'cost', label: t('maintenance.actualCost'), render: (r) => money(r.actualCost), exportValue: (r) => String(r.actualCost) },
    {
      key: 'source',
      label: t('maintenance.source'),
      render: (r) => (r.fromSchedule ? t('maintenance.preventive') : t('maintenance.manual')),
      exportValue: (r) => (r.fromSchedule ? t('maintenance.preventive') : t('maintenance.manual'))
    },
    {
      key: 'status',
      label: t('maintenance.status'),
      render: (r) => <Badge label={t(`maintenance.requestStatuses.${r.status}`)} tone={requestTone[r.status]} />,
      exportValue: (r) => t(`maintenance.requestStatuses.${r.status}`)
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{t('maintenance.requestsTitle')}</h2>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <SearchableSelect
            value={status}
            onChange={(v) => setStatus(v === '' ? '' : (v as MaintenanceRequestStatus))}
            options={[
              { value: '', label: t('maintenance.allStatuses') },
              ...(['Draft', 'Approved', 'InProgress', 'Completed', 'Rejected', 'Cancelled'] as MaintenanceRequestStatus[]).map((s) => ({
                value: s,
                label: t(`maintenance.requestStatuses.${s}`)
              }))
            ]}
            style={{ minWidth: 180 }}
          />
          {canAdd && (
            <Button variant="primary" onClick={() => navigate('/maintenance/requests/new')}>
              {t('maintenance.addRequest')}
            </Button>
          )}
        </div>
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/maintenance/requests/${row.id}`)}
        exportFileName={t('maintenance.requestsTitle')}
      />
    </div>
  );
}

interface SparePartRow extends MaintenanceSparePartInput {
  key: number;
}

export function MaintenanceRequestEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const [params] = useSearchParams();
  const isNew = id === 'new';
  const requestId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: request, isLoading } = useMaintenanceRequest(requestId);
  const { data: assets } = useFixedAssets();
  const { data: categories } = useMaintenanceCategories();
  const { data: suppliers } = useSuppliersList();
  const { data: items } = useItemsList();
  const { data: warehouses } = useWarehousesList();
  const accountOptions = usePostableAccountOptions();

  const [fixedAssetId, setFixedAssetId] = useState<number | ''>(params.get('assetId') ? Number(params.get('assetId')) : '');
  const [categoryId, setCategoryId] = useState<number | ''>('');
  const [requestDate, setRequestDate] = useState(today());
  const [scheduledDate, setScheduledDate] = useState('');
  const [technicianName, setTechnicianName] = useState('');
  const [supplierId, setSupplierId] = useState<number | ''>('');
  const [externalCreditAccountId, setExternalCreditAccountId] = useState<number | ''>('');
  const [estimatedCost, setEstimatedCost] = useState('');
  const [laborCost, setLaborCost] = useState('0');
  const [notes, setNotes] = useState('');
  const [parts, setParts] = useState<SparePartRow[]>([]);
  const [completedDate, setCompletedDate] = useState(today());

  useEffect(() => {
    if (!request) return;
    setFixedAssetId(request.fixedAssetId);
    setCategoryId(request.maintenanceCategoryId);
    setRequestDate(request.requestDate);
    setScheduledDate(request.scheduledDate ?? '');
    setTechnicianName(request.technicianName ?? '');
    setSupplierId(request.supplierId ?? '');
    setExternalCreditAccountId(request.externalCreditAccountId ?? '');
    setEstimatedCost(request.estimatedCost?.toString() ?? '');
    setLaborCost(String(request.laborCost));
    setNotes(request.notes ?? '');
    setParts(
      request.spareParts.map((p, index) => ({
        key: index,
        itemId: p.itemId,
        warehouseId: p.warehouseId,
        description: p.description,
        quantity: p.quantity,
        unitCost: p.isStocked ? null : p.unitCost
      }))
    );
  }, [request]);

  const save = useSaveMaintenanceRequest(requestId);
  const act = useMaintenanceRequestAction();

  const editable = isNew || request?.status === 'Draft' || request?.status === 'Approved' || request?.status === 'InProgress';

  const handleSave = async () => {
    try {
      const data = {
        fixedAssetId: Number(fixedAssetId),
        maintenanceCategoryId: Number(categoryId),
        requestDate,
        scheduledDate: scheduledDate || null,
        technicianId: request?.technicianId ?? null,
        technicianName: technicianName || null,
        supplierId: supplierId === '' ? null : Number(supplierId),
        externalCreditAccountId: externalCreditAccountId === '' ? null : Number(externalCreditAccountId),
        estimatedCost: estimatedCost === '' ? null : Number(estimatedCost),
        laborCost: Number(laborCost || 0),
        notes: notes || null,
        spareParts: parts.map((p) => ({
          itemId: p.itemId,
          warehouseId: p.warehouseId,
          description: p.description,
          quantity: Number(p.quantity || 0),
          unitCost: p.itemId ? null : Number(p.unitCost ?? 0)
        }))
      };
      const result = await save.mutateAsync({
        issueId: params.get('issueId') ? Number(params.get('issueId')) : null,
        rowVersion: request?.rowVersion,
        data
      });
      showToast(t('maintenance.saveSuccess'), 'success');
      if (isNew) navigate(`/maintenance/requests/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const perform = async (action: 'approve' | 'start' | 'complete' | 'reject' | 'cancel') => {
    if (!requestId) return;
    try {
      await act.mutateAsync({ id: requestId, action, completedDate });
      showToast(t('maintenance.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  const serviceableAssets = (assets ?? []).filter((a) => a.status !== 'Draft' && a.status !== 'Disposed' && a.status !== 'WrittenOff');
  const stockedTotal = (request?.spareParts ?? []).filter((p) => p.isStocked).reduce((sum, p) => sum + p.totalCost, 0);
  const boughtTotal = parts.filter((p) => !p.itemId).reduce((sum, p) => sum + Number(p.quantity || 0) * Number(p.unitCost ?? 0), 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('maintenance.addRequest') : `${t('maintenance.requestsTitle')} — ${request?.requestNumber ?? ''}`}</h2>
        {request && <Badge label={t(`maintenance.requestStatuses.${request.status}`)} tone={requestTone[request.status]} />}
      </div>

      <ActionBar
        primary={editable ? { key: 'save', label: t('common.saveChanges'), onClick: handleSave } : undefined}
        secondary={[
          ...(request?.status === 'Draft' ? [{ key: 'approve', label: t('maintenance.approve'), onClick: () => perform('approve') }] : []),
          ...(request?.status === 'Draft' || request?.status === 'Approved'
            ? [{ key: 'start', label: t('maintenance.start'), onClick: () => perform('start') }]
            : []),
          ...(request?.status === 'InProgress' ? [{ key: 'complete', label: t('maintenance.complete'), onClick: () => perform('complete') }] : []),
          ...(request?.status === 'Draft' || request?.status === 'Approved'
            ? [{ key: 'reject', label: t('common.reject'), onClick: () => perform('reject') }]
            : []),
          { key: 'back', label: t('common.back'), onClick: () => navigate('/maintenance/requests') }
        ]}
        destructive={
          request && request.status !== 'Completed' && request.status !== 'Rejected' && request.status !== 'Cancelled'
            ? [{ key: 'cancel', label: t('common.cancel'), onClick: () => perform('cancel'), confirmMessage: t('maintenance.cancelConfirm') }]
            : []
        }
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('maintenance.asset')}>
              <SearchableSelect
                value={fixedAssetId}
                onChange={(v) => setFixedAssetId(v === '' ? '' : Number(v))}
                options={serviceableAssets.map((a) => ({ value: a.id, label: `${a.assetNumber} — ${a.nameAr}` }))}
                disabled={!isNew && request?.status !== 'Draft'}
                style={{ minWidth: 260 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.category')}>
              <SearchableSelect
                value={categoryId}
                onChange={(v) => setCategoryId(v === '' ? '' : Number(v))}
                options={(categories ?? []).filter((c) => c.isActive).map((c) => ({ value: c.id, label: c.nameAr }))}
                disabled={!editable}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.requestDate')}>
              <Input type="date" value={requestDate} onChange={(e) => setRequestDate(e.target.value)} disabled={!editable} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.scheduledDate')}>
              <Input type="date" value={scheduledDate} onChange={(e) => setScheduledDate(e.target.value)} disabled={!editable} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.technician')}>
              <Input value={technicianName} onChange={(e) => setTechnicianName(e.target.value)} disabled={!editable} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.supplier')}>
              <SearchableSelect
                value={supplierId}
                onChange={(v) => setSupplierId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('maintenance.none') }, ...(suppliers ?? []).map((s) => ({ value: s.id, label: s.nameAr }))]}
                disabled={!editable}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.externalCreditAccount')}>
              <SearchableSelect
                value={externalCreditAccountId}
                onChange={(v) => setExternalCreditAccountId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('maintenance.none') }, ...accountOptions]}
                disabled={!editable}
                style={{ minWidth: 280 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.estimatedCost')}>
              <Input type="number" value={estimatedCost} onChange={(e) => setEstimatedCost(e.target.value)} disabled={!editable} style={{ width: 150 }} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.laborCost')}>
              <Input type="number" value={laborCost} onChange={(e) => setLaborCost(e.target.value)} disabled={!editable} style={{ width: 150 }} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.notes')}>
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} disabled={!editable} style={{ minWidth: 300 }} />
            </FieldWrapper>
            {request?.status === 'InProgress' && (
              <FieldWrapper label={t('maintenance.completedDate')}>
                <Input type="date" value={completedDate} onChange={(e) => setCompletedDate(e.target.value)} />
              </FieldWrapper>
            )}
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ marginTop: 0 }}>{t('maintenance.spareParts')}</h3>
            {editable && (
              <Button onClick={() => setParts((prev) => [...prev, { key: Date.now(), itemId: null, warehouseId: null, description: '', quantity: 1, unitCost: 0 }])}>
                {t('maintenance.addPart')}
              </Button>
            )}
          </div>
          <p style={{ color: 'var(--color-text-muted)', marginTop: 0 }}>{t('maintenance.stockedPartCostNote')}</p>

          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('maintenance.item')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('maintenance.warehouse')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('maintenance.description')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('maintenance.quantity')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('maintenance.unitCost')}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {parts.map((part, index) => {
                const update = (patch: Partial<SparePartRow>) =>
                  setParts((prev) => prev.map((p, i) => (i === index ? { ...p, ...patch } : p)));
                const posted = request?.spareParts[index];
                return (
                  <tr key={part.key} style={{ borderTop: '1px solid var(--color-border)' }}>
                    <td style={{ padding: 6 }}>
                      <SearchableSelect
                        value={part.itemId ?? ''}
                        onChange={(v) => update({ itemId: v === '' ? null : Number(v), unitCost: v === '' ? 0 : null })}
                        options={[
                          { value: '', label: t('maintenance.boughtForTheJob') },
                          ...(items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }))
                        ]}
                        disabled={!editable}
                        style={{ minWidth: 220 }}
                      />
                    </td>
                    <td style={{ padding: 6 }}>
                      <SearchableSelect
                        value={part.warehouseId ?? ''}
                        onChange={(v) => update({ warehouseId: v === '' ? null : Number(v) })}
                        options={[{ value: '', label: '—' }, ...(warehouses ?? []).map((w) => ({ value: w.id, label: w.nameAr }))]}
                        disabled={!editable || !part.itemId}
                        style={{ minWidth: 180 }}
                      />
                    </td>
                    <td style={{ padding: 6 }}>
                      <Input value={part.description ?? ''} onChange={(e) => update({ description: e.target.value })} disabled={!editable} />
                    </td>
                    <td style={{ padding: 6 }}>
                      <Input type="number" value={part.quantity} onChange={(e) => update({ quantity: Number(e.target.value) })} disabled={!editable} style={{ width: 100 }} />
                    </td>
                    <td style={{ padding: 6 }}>
                      {part.itemId ? (
                        <Input value={posted ? money(posted.unitCost) : t('maintenance.atAverageCost')} disabled />
                      ) : (
                        <Input
                          type="number"
                          value={part.unitCost ?? 0}
                          onChange={(e) => update({ unitCost: Number(e.target.value) })}
                          disabled={!editable}
                          style={{ width: 120 }}
                        />
                      )}
                    </td>
                    <td style={{ padding: 6 }}>
                      {editable && (
                        <Button variant="ghost" onClick={() => setParts((prev) => prev.filter((_, i) => i !== index))}>
                          {t('common.remove')}
                        </Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginTop: 12 }}>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('maintenance.externalCost')}</div>
              <div>{money(Number(laborCost || 0) + boughtTotal)}</div>
            </div>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('maintenance.stockedPartsCost')}</div>
              <div>{money(stockedTotal)}</div>
            </div>
            <div>
              <div style={{ color: 'var(--color-text-muted)' }}>{t('maintenance.actualCost')}</div>
              <div>{money(request?.actualCost ?? Number(laborCost || 0) + boughtTotal)}</div>
            </div>
            {request?.journalEntryId && (
              <div>
                <div style={{ color: 'var(--color-text-muted)' }}>{t('maintenance.externalEntry')}</div>
                <div>{request.journalEntryId}</div>
              </div>
            )}
            {request?.sparePartsJournalEntryId && (
              <div>
                <div style={{ color: 'var(--color-text-muted)' }}>{t('maintenance.sparePartsEntry')}</div>
                <div>{request.sparePartsJournalEntryId}</div>
              </div>
            )}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}

// ================================================================== preventive schedules (screen #11)

/** /maintenance/schedules — the timetable the daily job raises requests from. */
export function MaintenanceSchedulesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useMaintenanceSchedules();

  const rows = toPaged(data, search, (s, q) => `${s.assetNumber} ${s.assetNameAr} ${s.categoryNameAr}`.toLowerCase().includes(q));

  const columns: DataGridColumn<MaintenanceSchedule>[] = [
    { key: 'asset', label: t('maintenance.asset'), render: (r) => `${r.assetNumber} — ${r.assetNameAr}`, exportValue: (r) => r.assetNumber },
    { key: 'category', label: t('maintenance.category'), render: (r) => r.categoryNameAr, exportValue: (r) => r.categoryNameAr },
    {
      key: 'frequency',
      label: t('maintenance.frequency'),
      render: (r) => t(`maintenance.frequencies.${r.frequency}`),
      exportValue: (r) => t(`maintenance.frequencies.${r.frequency}`)
    },
    { key: 'last', label: t('maintenance.lastExecuted'), render: (r) => r.lastExecutedDate ?? '—', exportValue: (r) => r.lastExecutedDate ?? '' },
    { key: 'next', label: t('maintenance.nextDue'), render: (r) => r.nextDueDate, exportValue: (r) => r.nextDueDate },
    { key: 'technician', label: t('maintenance.technician'), render: (r) => r.technicianName ?? '—', exportValue: (r) => r.technicianName ?? '' },
    {
      key: 'isActive',
      label: t('maintenance.isActive'),
      render: (r) => (r.isActive ? t('common.yes') : t('common.no')),
      exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no'))
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('maintenance.schedulesTitle')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/maintenance/schedules/new')}>
            {t('maintenance.addSchedule')}
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
        onRowClick={(row) => navigate(`/maintenance/schedules/${row.id}`)}
        exportFileName={t('maintenance.schedulesTitle')}
      />
    </div>
  );
}

export function MaintenanceScheduleEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const scheduleId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: schedules } = useMaintenanceSchedules();
  const schedule = schedules?.find((s) => s.id === scheduleId);
  const { data: assets } = useFixedAssets();
  const { data: categories } = useMaintenanceCategories();

  const [fixedAssetId, setFixedAssetId] = useState<number | ''>('');
  const [categoryId, setCategoryId] = useState<number | ''>('');
  const [frequency, setFrequency] = useState<MaintenanceFrequency>('Monthly');
  const [nextDueDate, setNextDueDate] = useState(today());
  const [technicianName, setTechnicianName] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (!schedule) return;
    setFixedAssetId(schedule.fixedAssetId);
    setCategoryId(schedule.maintenanceCategoryId);
    setFrequency(schedule.frequency);
    setNextDueDate(schedule.nextDueDate);
    setTechnicianName(schedule.technicianName ?? '');
    setIsActive(schedule.isActive);
    setNotes(schedule.notes ?? '');
  }, [schedule]);

  const save = useSaveMaintenanceSchedule(scheduleId);
  const remove = useDeleteMaintenanceSchedule();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        fixedAssetId: Number(fixedAssetId),
        maintenanceCategoryId: Number(categoryId),
        frequency,
        nextDueDate,
        technicianId: schedule?.technicianId ?? null,
        technicianName: technicianName || null,
        isActive,
        notes: notes || null
      });
      showToast(t('maintenance.saveSuccess'), 'success');
      if (isNew) navigate(`/maintenance/schedules/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!scheduleId) return;
    try {
      await remove.mutateAsync(scheduleId);
      showToast(t('maintenance.deleteSuccess'), 'success');
      navigate('/maintenance/schedules');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const serviceableAssets = (assets ?? []).filter((a) => a.status !== 'Draft' && a.status !== 'Disposed' && a.status !== 'WrittenOff');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 760 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('maintenance.addSchedule') : `${t('maintenance.schedulesTitle')} — ${schedule?.assetNumber ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/maintenance/schedules') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('maintenance.deleteScheduleConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('maintenance.asset')}>
              <SearchableSelect
                value={fixedAssetId}
                onChange={(v) => setFixedAssetId(v === '' ? '' : Number(v))}
                options={serviceableAssets.map((a) => ({ value: a.id, label: `${a.assetNumber} — ${a.nameAr}` }))}
                style={{ minWidth: 260 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.category')}>
              <SearchableSelect
                value={categoryId}
                onChange={(v) => setCategoryId(v === '' ? '' : Number(v))}
                options={(categories ?? []).filter((c) => c.isActive).map((c) => ({ value: c.id, label: c.nameAr }))}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.frequency')}>
              <SearchableSelect
                value={frequency}
                onChange={(v) => setFrequency(v as MaintenanceFrequency)}
                options={(['Daily', 'Weekly', 'Monthly', 'Quarterly', 'SemiAnnual', 'Annual'] as MaintenanceFrequency[]).map((f) => ({
                  value: f,
                  label: t(`maintenance.frequencies.${f}`)
                }))}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.nextDue')}>
              <Input type="date" value={nextDueDate} onChange={(e) => setNextDueDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.technician')}>
              <Input value={technicianName} onChange={(e) => setTechnicianName(e.target.value)} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.isActive')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('maintenance.notes')}>
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} style={{ minWidth: 300 }} />
            </FieldWrapper>
          </div>
          {schedule?.lastExecutedDate && (
            <p style={{ color: 'var(--color-text-muted)' }}>{t('maintenance.lastExecutedNote', { date: schedule.lastExecutedDate })}</p>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
