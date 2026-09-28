import * as XLSX from 'xlsx';

/** Parses an uploaded .xlsx/.csv file into an array of plain row objects keyed by header. */
export async function parseSpreadsheetFile(file: File): Promise<Record<string, string>[]> {
  const buffer = await file.arrayBuffer();
  const workbook = XLSX.read(buffer, { type: 'array' });
  const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
  return XLSX.utils.sheet_to_json<Record<string, string>>(firstSheet, { defval: '' });
}

export interface ImportColumn {
  /** Exact header text the import parser reads from the uploaded file's first row. */
  header: string;
  /** One example value shown in the downloadable template's own first data row. */
  example?: string;
}

/** My Remarks/Remarks2.md, remark 3.2 — every screen with an import button gets a "Download
 * Template" button right next to it, so the header row (and casing) is never guessed by hand. */
export function downloadImportTemplate(columns: ImportColumn[], fileName: string) {
  const worksheet = XLSX.utils.json_to_sheet([Object.fromEntries(columns.map((c) => [c.header, c.example ?? '']))]);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Template');
  XLSX.writeFile(workbook, `${fileName}.xlsx`);
}
