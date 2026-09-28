import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './Button';
import { exportToCsv, exportToExcel, exportToPdf, printTable, type ExportColumn } from '../lib/export';

interface ExportMenuProps<T> {
  rows: T[];
  columns: ExportColumn<T>[];
  fileName: string;
  title: string;
}

/**
 * Standard export action for any DataGrid/report screen (00-Project-Overview.md, section 8.5):
 * Excel/CSV/PDF/Print, scoped to the rows currently displayed (after filters/search) only.
 * Rows over 10,000 require explicit confirmation before exporting (section 8.5).
 */
export function ExportMenu<T>({ rows, columns, fileName, title }: ExportMenuProps<T>) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  const confirmLargeExport = () => {
    if (rows.length > 10000) {
      return window.confirm(`${rows.length} ${t('common.rowsFound')} — ${t('common.export')}?`);
    }
    return true;
  };

  const handle = (action: () => void) => {
    if (confirmLargeExport()) action();
    setOpen(false);
  };

  return (
    <div style={{ position: 'relative' }}>
      <Button variant="secondary" onClick={() => setOpen((o) => !o)}>
        {t('common.export')} ▾
      </Button>
      {open && (
        <div
          style={{
            position: 'absolute', insetInlineEnd: 0, top: '110%', background: '#fff', borderRadius: 8,
            boxShadow: 'var(--shadow-2)', border: '1px solid var(--color-border)', minWidth: 160, zIndex: 20
          }}
        >
          <MenuItem label={t('common.exportExcel')} onClick={() => handle(() => exportToExcel(rows, columns, fileName))} />
          <MenuItem label={t('common.exportCsv')} onClick={() => handle(() => exportToCsv(rows, columns, fileName))} />
          <MenuItem label={t('common.exportPdf')} onClick={() => handle(() => exportToPdf(rows, columns, fileName, title))} />
          <MenuItem label={t('common.print')} onClick={() => handle(() => printTable(rows, columns, title))} />
        </div>
      )}
    </div>
  );
}

function MenuItem({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <div
      onClick={onClick}
      style={{ padding: '8px 12px', fontSize: 13, cursor: 'pointer' }}
      onMouseEnter={(e) => (e.currentTarget.style.background = '#f4f5f7')}
      onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
    >
      {label}
    </div>
  );
}
