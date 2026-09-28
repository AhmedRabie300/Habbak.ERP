import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useJournalEntriesList } from './api';
import type { JournalEntryListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/journal-entries — List screen (00-Frontend-Specs.md, section 5). */
export function JournalEntriesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useJournalEntriesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('ACCOUNTING_JOURNAL_ENTRIES');

  const columns: DataGridColumn<JournalEntryListItem>[] = [
    { key: 'entryNumber', label: label('entryNumber', t('journalEntries.entryNumber')), render: (r) => r.entryNumber, exportValue: (r) => r.entryNumber },
    { key: 'entryDate', label: label('date', t('journalEntries.date')), render: (r) => r.entryDate, exportValue: (r) => r.entryDate },
    { key: 'description', label: label('description', t('journalEntries.description')), render: (r) => r.description, exportValue: (r) => r.description },
    { key: 'sourceModule', label: label('source', t('journalEntries.source')), render: (r) => r.sourceModule, exportValue: (r) => r.sourceModule },
    { key: 'totalDebit', label: label('debit', t('journalEntries.debit')), render: (r) => r.totalDebit.toLocaleString(), exportValue: (r) => r.totalDebit },
    { key: 'totalCredit', label: label('credit', t('journalEntries.credit')), render: (r) => r.totalCredit.toLocaleString(), exportValue: (r) => r.totalCredit },
    { key: 'status', label: label('status', t('journalEntries.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('journalEntries.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/journal-entries/new')}>{t('journalEntries.newManual')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/accounting/journal-entries/${row.id}`)}
        exportFileName={t('journalEntries.title')}
      />
    </div>
  );
}
