import jsPDF from 'jspdf';
import html2canvas from 'html2canvas';
import { rowsToWorkbookBuffer, rowsToCsvText } from './spreadsheet';

export interface ExportColumn<T> {
  header: string;
  value: (row: T) => string | number;
}

/**
 * Export policy (00-Project-Overview.md, section 8.5): Excel as the primary format for
 * lists/reports, PDF for official documents, CSV for re-import elsewhere, plus direct print.
 * Exports only the rows currently displayed (after filters/search) — never the whole table.
 * Switched from SheetJS (`xlsx`) to ExcelJS: the `xlsx` package on the npm registry is capped at
 * 0.18.5 (SheetJS stopped publishing newer builds there), which carries two unpatched high-severity
 * advisories (prototype pollution, ReDoS) — real exposure here since parseSpreadsheetFile in
 * lib/import.ts feeds it user-uploaded files. The patched SheetJS builds only exist on
 * cdn.sheetjs.com, which some network policies block outright (the reason for this switch).
 */
export async function exportToExcel<T>(rows: T[], columns: ExportColumn<T>[], fileName: string) {
  const headers = columns.map((c) => c.header);
  const data = rows.map((row) => columns.map((c) => c.value(row)));
  const buffer = await rowsToWorkbookBuffer(headers, data);
  downloadBlob(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), `${fileName}.xlsx`);
}

export function exportToCsv<T>(rows: T[], columns: ExportColumn<T>[], fileName: string) {
  const headers = columns.map((c) => c.header);
  const data = rows.map((row) => columns.map((c) => c.value(row)));
  const csv = rowsToCsvText(headers, data);
  const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8;' });
  downloadBlob(blob, `${fileName}.csv`);
}

/**
 * PDF export renders the table as HTML and rasterizes it (html2canvas) rather than drawing text
 * directly with jsPDF's own fonts: jsPDF's built-in fonts (Helvetica/Times/Courier) only cover
 * WinAnsi encoding, so Arabic text fed straight to `doc.text()` gets silently reinterpreted as
 * WinAnsi codepoints — the exact "þ°þÛþ®..." mojibake from My Remarks/Remarks2.md, remark 5.1.
 * Rasterizing through the browser's own text engine sidesteps that entirely (correct Arabic
 * shaping and RTL layout for free, same as `printTable` below), at the cost of the PDF being an
 * image rather than selectable text — an acceptable trade for a "printable report" export.
 */
export async function exportToPdf<T>(rows: T[], columns: ExportColumn<T>[], fileName: string, title: string) {
  const dir = document.documentElement.dir || 'rtl';
  const container = document.createElement('div');
  container.dir = dir;
  container.style.position = 'fixed';
  container.style.top = '0';
  container.style.insetInlineStart = '-99999px';
  container.style.width = '1000px';
  container.style.background = '#ffffff';
  container.style.padding = '24px';
  container.style.fontFamily = "'Segoe UI', Tahoma, Arial, sans-serif";

  const headerRow = columns.map((c) => `<th style="border:1px solid #ccc;padding:6px 10px;text-align:start;background:#f4f5f7;">${escapeHtml(c.header)}</th>`).join('');
  const bodyRows = rows
    .map((row) => `<tr>${columns.map((c) => `<td style="border:1px solid #ccc;padding:6px 10px;text-align:start;">${escapeHtml(String(c.value(row)))}</td>`).join('')}</tr>`)
    .join('');
  container.innerHTML = `
    <h2 style="margin:0 0 16px;font-size:18px;">${escapeHtml(title)}</h2>
    <table style="width:100%;border-collapse:collapse;font-size:13px;">
      <thead><tr>${headerRow}</tr></thead>
      <tbody>${bodyRows}</tbody>
    </table>
  `;
  document.body.appendChild(container);

  try {
    const canvas = await html2canvas(container, { scale: 2, backgroundColor: '#ffffff' });

    const pdf = new jsPDF({ unit: 'pt', format: 'a4' });
    const pageWidth = pdf.internal.pageSize.getWidth();
    const pageHeight = pdf.internal.pageSize.getHeight();
    const imgWidth = pageWidth;
    const imgHeight = (canvas.height * imgWidth) / canvas.width;
    const imgData = canvas.toDataURL('image/png');

    let heightLeft = imgHeight;
    let position = 0;
    pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
    heightLeft -= pageHeight;

    while (heightLeft > 0) {
      position -= pageHeight;
      pdf.addPage();
      pdf.addImage(imgData, 'PNG', 0, position, imgWidth, imgHeight);
      heightLeft -= pageHeight;
    }

    pdf.save(`${fileName}.pdf`);
  } finally {
    document.body.removeChild(container);
  }
}

export function printTable<T>(rows: T[], columns: ExportColumn<T>[], title: string) {
  const printWindow = window.open('', '_blank');
  if (!printWindow) return;
  const dir = document.documentElement.dir || 'rtl';

  const headerRow = columns.map((c) => `<th>${escapeHtml(c.header)}</th>`).join('');
  const bodyRows = rows
    .map((row) => `<tr>${columns.map((c) => `<td>${escapeHtml(String(c.value(row)))}</td>`).join('')}</tr>`)
    .join('');

  printWindow.document.write(`
    <html dir="${dir}">
      <head>
        <title>${escapeHtml(title)}</title>
        <style>
          body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; padding: 24px; }
          table { width: 100%; border-collapse: collapse; font-size: 13px; }
          th, td { border: 1px solid #ccc; padding: 6px 10px; text-align: start; }
          th { background: #f4f5f7; }
          h2 { margin-top: 0; }
        </style>
      </head>
      <body>
        <h2>${escapeHtml(title)}</h2>
        <table><thead><tr>${headerRow}</tr></thead><tbody>${bodyRows}</tbody></table>
        <script>window.onload = () => window.print();</script>
      </body>
    </html>
  `);
  printWindow.document.close();
}

/**
 * My Remarks/Remarks2.md, remark 5.2 — the unified "طباعة" button every Edit/List screen needs.
 * Used by ActionBar (prints the screen's own content region, everything except whatever it tags
 * `data-no-print`, e.g. itself). Clones the live node into the print window's document so its
 * exact current on-screen state prints — not the raw source markup — and copies every stylesheet
 * rule (rather than linking the original files) so it renders identically regardless of dev vs.
 * built asset paths, with no network fetch for the print window to wait on.
 */
export function printElement(source: HTMLElement, title: string) {
  const printWindow = window.open('', '_blank');
  if (!printWindow) return;
  const dir = document.documentElement.dir || 'rtl';

  printWindow.document.write(`
    <html dir="${dir}">
      <head>
        <title>${escapeHtml(title)}</title>
        <style>${collectStylesheetText()}</style>
        <style>
          body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; padding: 24px; background: #fff; }
          .print-doc-title { margin: 0 0 16px; font-size: 18px; }
        </style>
      </head>
      <body><h2 class="print-doc-title">${escapeHtml(title)}</h2></body>
    </html>
  `);
  printWindow.document.close();

  const clone = printWindow.document.importNode(source, true) as HTMLElement;
  clone.querySelectorAll('[data-no-print]').forEach((el) => el.remove());
  printWindow.document.body.appendChild(clone);

  printWindow.focus();
  printWindow.print();
}

/** Every current stylesheet's rules, inlined as plain CSS text — same-origin only (this app has
 * no cross-origin stylesheets), so a `<style>`/`<link>` this fails on is skipped, not fatal. */
function collectStylesheetText(): string {
  const chunks: string[] = [];
  for (const sheet of Array.from(document.styleSheets)) {
    try {
      const rules = sheet.cssRules;
      if (!rules) continue;
      chunks.push(Array.from(rules).map((r) => r.cssText).join('\n'));
    } catch {
      // cross-origin stylesheet — inaccessible by design, nothing to do
    }
  }
  return chunks.join('\n');
}

export function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function escapeHtml(value: string): string {
  const div = document.createElement('div');
  div.textContent = value;
  return div.innerHTML;
}
