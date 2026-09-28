import ExcelJS from 'exceljs';

/**
 * Shared xlsx/csv read+write, used by lib/export.ts and lib/import.ts. Built on ExcelJS (npm
 * registry, actively maintained) instead of SheetJS's `xlsx` package: the `xlsx` version published
 * to npm is capped at 0.18.5 (SheetJS stopped publishing newer builds there) and carries two
 * unpatched high-severity advisories (prototype pollution, ReDoS) — real exposure since
 * parseSpreadsheetFile below feeds it user-uploaded files. The patched SheetJS builds only exist on
 * cdn.sheetjs.com, which some network policies block outright.
 *
 * CSV is handled by hand rather than through ExcelJS's own (stream/Node-oriented) csv module — a
 * small, browser-safe parser/serializer is simpler and more predictable here than fighting that API
 * in a Vite/browser bundle.
 */

export async function rowsToWorkbookBuffer(headers: string[], rows: (string | number)[][], sheetName = 'Sheet1'): Promise<ArrayBuffer> {
  const workbook = new ExcelJS.Workbook();
  const worksheet = workbook.addWorksheet(sheetName);
  worksheet.addRow(headers);
  for (const row of rows) {
    worksheet.addRow(row);
  }
  return (await workbook.xlsx.writeBuffer()) as ArrayBuffer;
}

/** First worksheet, first row as headers, everything else as header-keyed string rows (empty cell -> ''). */
export async function parseWorkbookBuffer(buffer: ArrayBuffer): Promise<Record<string, string>[]> {
  const workbook = new ExcelJS.Workbook();
  await workbook.xlsx.load(buffer);
  const worksheet = workbook.worksheets[0];
  if (!worksheet) {
    return [];
  }

  const headers: string[] = [];
  worksheet.getRow(1).eachCell({ includeEmpty: true }, (cell, colNumber) => {
    headers[colNumber - 1] = cell.value == null ? '' : String(cell.value);
  });

  const result: Record<string, string>[] = [];
  worksheet.eachRow((row, rowNumber) => {
    if (rowNumber === 1) {
      return;
    }

    const obj: Record<string, string> = {};
    headers.forEach((header, i) => {
      if (!header) {
        return;
      }
      const cell = row.getCell(i + 1);
      obj[header] = cell.value == null ? '' : String(cell.value);
    });
    result.push(obj);
  });

  return result;
}

function escapeCsvField(value: string | number): string {
  const str = String(value ?? '');
  return /[",\r\n]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
}

export function rowsToCsvText(headers: string[], rows: (string | number)[][]): string {
  const lines = [headers.map(escapeCsvField).join(',')];
  for (const row of rows) {
    lines.push(row.map(escapeCsvField).join(','));
  }
  return lines.join('\r\n');
}

/** RFC 4180-ish: quoted fields, doubled-quote escaping, CRLF/LF/CR line endings. */
function parseCsvRows(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (inQuotes) {
      if (char === '"') {
        if (text[i + 1] === '"') {
          field += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        field += char;
      }
    } else if (char === '"') {
      inQuotes = true;
    } else if (char === ',') {
      row.push(field);
      field = '';
    } else if (char === '\n' || char === '\r') {
      if (char === '\r' && text[i + 1] === '\n') {
        i++;
      }
      row.push(field);
      field = '';
      rows.push(row);
      row = [];
    } else {
      field += char;
    }
  }

  if (field.length > 0 || row.length > 0) {
    row.push(field);
    rows.push(row);
  }

  return rows.filter((r) => r.length > 1 || r[0] !== '');
}

/** First row as headers, everything else as header-keyed string rows (missing cell -> ''). */
export function parseCsvText(text: string): Record<string, string>[] {
  const table = parseCsvRows(text.replace(/^﻿/, ''));
  if (table.length === 0) {
    return [];
  }

  const [headers, ...dataRows] = table;
  return dataRows.map((row) => {
    const obj: Record<string, string> = {};
    headers.forEach((header, i) => {
      obj[header] = row[i] ?? '';
    });
    return obj;
  });
}
