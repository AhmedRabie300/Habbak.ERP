import { useState, type CSSProperties } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { SectionTabs } from '../../../ui-kit/SectionTabs';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { Icon } from '../../../ui-kit/Icon';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import {
  useApprovalWorkflowsList, useAssignWorkflowToScreen, useScreensList, useUnassignWorkflowFromScreen, useWorkflowAssignments
} from './api';
import type { ApprovalWorkflowListItem } from './types';

/** /settings/approval-workflows — SETTINGS_APPROVAL_WORKFLOWS (00-Project-Overview.md §12). */
export function ApprovalWorkflowsListPage() {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<'workflows' | 'assignments'>('workflows');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('approvals.workflows.title')}</h2>

      <SectionTabs
        items={[
          { id: 'workflows', label: t('approvals.workflows.tabWorkflows') },
          { id: 'assignments', label: t('approvals.workflows.tabAssignments') }
        ]}
        activeId={activeTab}
        onChange={(id) => setActiveTab(id as typeof activeTab)}
      />

      {activeTab === 'workflows' ? <WorkflowsTab /> : <AssignmentsTab />}
    </div>
  );
}

function WorkflowsTab() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const { data, isLoading } = useApprovalWorkflowsList({ search, page, pageSize: 25 });

  const columns: DataGridColumn<ApprovalWorkflowListItem>[] = [
    { key: 'code', label: t('approvals.workflows.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('approvals.workflows.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'steps', label: t('approvals.workflows.steps'), render: (r) => r.stepCount, exportValue: (r) => r.stepCount },
    { key: 'version', label: t('approvals.workflows.version_short'), render: (r) => r.versionNumber, exportValue: (r) => r.versionNumber },
    { key: 'status', label: t('approvals.common.status'), render: (r) => <StatusBadge status={r.isActive ? 'Active' : 'Inactive'} />, exportValue: (r) => (r.isActive ? 'Active' : 'Inactive') }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {canAdd && (
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button variant="primary" onClick={() => navigate('/settings/approval-workflows/new')}>
            {t('approvals.workflows.add')}
          </Button>
        </div>
      )}
      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/settings/approval-workflows/${row.id}`)}
        exportFileName={t('approvals.workflows.title')}
      />
    </div>
  );
}

function AssignmentsTab() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: assignments, isLoading } = useWorkflowAssignments();
  const { data: screens } = useScreensList();
  const { data: workflows } = useApprovalWorkflowsList({ page: 1, pageSize: 500 });
  const assignMutation = useAssignWorkflowToScreen();
  const unassignMutation = useUnassignWorkflowFromScreen();

  const [screenId, setScreenId] = useState<number | undefined>();
  const [workflowId, setWorkflowId] = useState<number | undefined>();
  const [minAmount, setMinAmount] = useState('');

  const assignedScreenIds = new Set((assignments ?? []).map((a) => a.screenId));
  const unassignedScreens = (screens ?? []).filter((s) => !assignedScreenIds.has(s.id));

  const onAssign = async () => {
    if (!screenId || !workflowId) return;
    try {
      await assignMutation.mutateAsync({ screenId, approvalWorkflowId: workflowId, minAmount: minAmount ? Number(minAmount) : null });
      setScreenId(undefined);
      setWorkflowId(undefined);
      setMinAmount('');
      showToast(t('approvals.workflows.saveSuccess'), 'success');
    } catch (err) {
      const message = getFieldErrorMessage(err); if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <FieldWrapper label={t('approvals.assignments.screen')}>
              <SearchableSelect
                value={screenId}
                onChange={(v) => setScreenId(Number(v))}
                options={unassignedScreens.map((s) => ({ value: s.id, label: `${s.nameAr} (${s.moduleCode})` }))}
                placeholder={t('approvals.common.select')}
                style={{ width: 260 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('approvals.assignments.workflow')}>
              <SearchableSelect
                value={workflowId}
                onChange={(v) => setWorkflowId(Number(v))}
                options={(workflows?.items ?? []).filter((w) => w.isActive).map((w) => ({ value: w.id, label: w.nameAr }))}
                placeholder={t('approvals.common.select')}
                style={{ width: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('approvals.assignments.minAmount')}>
              <Input type="number" value={minAmount} onChange={(e) => setMinAmount(e.target.value)} placeholder="0" style={{ width: 140 }} />
            </FieldWrapper>
            <Button variant="primary" disabled={!screenId || !workflowId} onClick={onAssign}>
              <Icon name="plus" size={14} />
              {t('approvals.assignments.assign')}
            </Button>
          </div>
        </CardBody>
      </Card>

      <div style={{ background: 'var(--color-surface)', borderTop: '3px solid var(--color-gold-500)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)', overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid var(--color-border)' }}>
              <th style={thStyle}>{t('approvals.assignments.screen')}</th>
              <th style={thStyle}>{t('approvals.assignments.workflow')}</th>
              <th style={thStyle}>{t('approvals.assignments.minAmount')}</th>
              <th style={thStyle} />
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td style={tdStyle} colSpan={4}>{t('common.loading')}</td></tr>}
            {!isLoading && (assignments?.length ?? 0) === 0 && <tr><td style={tdStyle} colSpan={4}>{t('common.noData')}</td></tr>}
            {assignments?.map((a) => (
              <tr key={a.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                <td style={tdStyle}>{a.screenNameAr}</td>
                <td style={tdStyle}>{a.workflowNameAr}</td>
                <td style={tdStyle}>{a.minAmount ?? '—'}</td>
                <td style={tdStyle}>
                  <Button variant="ghost" onClick={() => unassignMutation.mutate(a.screenId)}>
                    <Icon name="trash" size={14} />
                    {t('approvals.assignments.unassign')}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

const thStyle: CSSProperties = { textAlign: 'start', padding: '10px 14px', fontSize: 12, color: 'var(--color-text-muted)', fontWeight: 600 };
const tdStyle: CSSProperties = { padding: '10px 14px', fontSize: 13 };
