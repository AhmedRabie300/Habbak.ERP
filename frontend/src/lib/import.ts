import { parseCsvText, parseWorkbookBuffer, rowsToWorkbookBuffer } from './spreadsheet';
import { downloadBlob } from './export';

/** Parses an uploaded .xlsx/.csv file into an array of plain row objects keyed by header.
 * Legacy binary .xls is not supported (ExcelJS reads/writes .xlsx only) — same limitation as the
 * "Download Template" button below, which only ever produces .xlsx. */
export async function parseSpreadsheetFile(file: File): Promise<Record<string, string>[]> {
  if (file.name.toLowerCase().endsWith('.csv')) {
    return parseCsvText(await file.text());
  }
  return parseWorkbookBuffer(await file.arrayBuffer());
}

export interface ImportColumn {
  /** Exact header text the import parser reads from the uploaded file's first row. */
  header: string;
  /** One example value shown in the downloadable template's own first data row. */
  example?: string;
}

/** My Remarks/Remarks2.md, remark 3.2 — every screen with an import button gets a "Download
 * Template" button right next to it, so the header row (and casing) is never guessed by hand. */
export async function downloadImportTemplate(columns: ImportColumn[], fileName: string) {
  const buffer = await rowsToWorkbookBuffer(columns.map((c) => c.header), [columns.map((c) => c.example ?? '')], 'Template');
  downloadBlob(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), `${fileName}.xlsx`);
}
