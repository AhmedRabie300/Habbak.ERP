import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './Button';
import { Card, CardBody } from './Card';
import { parseSpreadsheetFile, downloadImportTemplate, type ImportColumn } from '../lib/import';

export interface ImportResult {
  successCount: number;
  errors: string[];
}

interface ImportPanelProps {
  columns: ImportColumn[];
  templateFileName: string;
  /** Receives every parsed row (header-keyed) and imports them one at a time — callers already
   * have a per-entity Create mutation to call per row; this component only owns the file
   * picker/template/preview/confirm shell around that, matching the pattern first built for
   * ChartOfAccountsPage's own import flow. */
  onImportRows: (rows: Record<string, string>[]) => Promise<ImportResult>;
}

/** My Remarks/Remarks2.md, remark 3.2 — a standard "Import" panel any List screen can drop in:
 * choose file → Download Template sits right next to it → preview row count → confirm → per-row
 * success/error summary. */
export function ImportPanel({ columns, templateFileName, onImportRows }: ImportPanelProps) {
  const { t } = useTranslation();
  const [preview, setPreview] = useState<Record<string, string>[] | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [isImporting, setIsImporting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFile = async (file: File) => {
    const rows = await parseSpreadsheetFile(file);
    setPreview(rows);
    setResult(null);
  };

  const handleConfirm = async () => {
    if (!preview) return;
    setIsImporting(true);
    try {
      const res = await onImportRows(preview);
      setResult(res);
      setPreview(null);
    } finally {
      setIsImporting(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  return (
    <Card>
      <CardBody>
        <p style={{ fontSize: 12, color: 'var(--color-text-muted)', marginTop: 0 }}>
          {columns.map((c) => c.header).join(', ')}
        </p>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
          <input
            ref={fileInputRef}
            type="file"
            accept=".xlsx,.xls,.csv"
            onChange={(e) => e.target.files?.[0] && handleFile(e.target.files[0])}
          />
          <Button type="button" variant="secondary" onClick={() => downloadImportTemplate(columns, templateFileName)}>
            {t('common.downloadTemplate')}
          </Button>
        </div>

        {preview && (
          <div style={{ marginTop: 12, display: 'flex', gap: 12, alignItems: 'center' }}>
            <p style={{ fontSize: 13, margin: 0 }}>{preview.length} {t('common.rowsFound')}</p>
            <Button type="button" variant="primary" onClick={handleConfirm} disabled={isImporting}>
              {t('common.confirmImport')}
            </Button>
          </div>
        )}

        {result && (
          <div style={{ marginTop: 12 }}>
            <p style={{ fontSize: 13 }}>{t('common.importSuccessCount', { count: result.successCount, total: result.successCount + result.errors.length })}</p>
            {result.errors.length > 0 && (
              <div>
                <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--color-error)' }}>{t('common.importErrors')}</p>
                {result.errors.map((err, i) => (
                  <p key={i} style={{ fontSize: 12.5, color: 'var(--color-error)', margin: '2px 0' }}>{err}</p>
                ))}
              </div>
            )}
          </div>
        )}
      </CardBody>
    </Card>
  );
}
