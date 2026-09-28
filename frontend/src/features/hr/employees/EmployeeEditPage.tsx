import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { StatusBadge } from '../../../ui-kit/Badge';
import { SectionTabs, type SectionTabItem } from '../../../ui-kit/SectionTabs';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useActivateEmployee, useEmployee, useTerminateEmployee } from './api';
import { BasicInfoTab } from './tabs/BasicInfoTab';
import { PersonalInfoTab } from './tabs/PersonalInfoTab';
import { ContractsTab } from './tabs/ContractsTab';
import { DocumentsTab } from './tabs/DocumentsTab';
import { CertificationsTab } from './tabs/CertificationsTab';

type TabId = 'basic' | 'personal' | 'contracts' | 'documents' | 'certifications';

/** /hr/employees/:id — screen HR_EMPLOYEES. 5 tabs via SectionTabs (ui-kit, built in 1.5.1) — the
 * 4 follower tabs need an existing employee id (same "save first" precedent as AttachmentPanel),
 * so they're locked to "basic" while creating a new employee. */
export function EmployeeEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const employeeId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const canEdit = usePermission('edit');

  const [activeTab, setActiveTab] = useState<TabId>('basic');
  const { data: employee, isLoading } = useEmployee(employeeId);

  const activate = useActivateEmployee();
  const terminate = useTerminateEmployee();

  const handleActivate = async () => {
    if (!employeeId) return;
    try {
      await activate.mutateAsync(employeeId);
      showToast(t('hr.employees.activateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleTerminate = async () => {
    if (!employeeId) return;
    try {
      await terminate.mutateAsync(employeeId);
      showToast(t('hr.employees.terminateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const tabs: SectionTabItem[] = [
    { id: 'basic', label: t('hr.employees.tabBasic') },
    { id: 'personal', label: t('hr.employees.tabPersonal') },
    { id: 'contracts', label: t('hr.employees.tabContracts') },
    { id: 'documents', label: t('hr.employees.tabDocuments') },
    { id: 'certifications', label: t('hr.employees.tabCertifications') }
  ];

  const selectTab = (tabId: string) => {
    if (isNew && tabId !== 'basic') return;
    setActiveTab(tabId as TabId);
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h2 style={{ margin: 0 }}>
          {isNew ? t('hr.employees.add') : `${employee?.nameAr ?? ''} — ${employee?.code ?? ''}`}
        </h2>
        {employee && <StatusBadge status={employee.status} />}
      </div>

      <ActionBar
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/employees') }]}
        primary={
          canEdit && employee?.status === 'Draft'
            ? { key: 'activate', label: t('hr.employees.activate'), onClick: handleActivate }
            : undefined
        }
        destructive={
          canEdit && employee && employee.status !== 'Terminated'
            ? [{ key: 'terminate', label: t('hr.employees.terminate'), onClick: handleTerminate, confirmMessage: t('hr.employees.terminateConfirm') }]
            : []
        }
      />

      <SectionTabs items={tabs} activeId={activeTab} onChange={selectTab} />

      {activeTab === 'basic' && <BasicInfoTab employeeId={employeeId} employee={employee ?? null} isNew={isNew} />}
      {activeTab === 'personal' && employeeId && <PersonalInfoTab employeeId={employeeId} employee={employee ?? null} />}
      {activeTab === 'contracts' && employeeId && <ContractsTab employeeId={employeeId} />}
      {activeTab === 'documents' && employeeId && <DocumentsTab employeeId={employeeId} />}
      {activeTab === 'certifications' && employeeId && <CertificationsTab employeeId={employeeId} />}
    </div>
  );
}
