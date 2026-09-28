import { describe, expect, it } from 'vitest';
import { parseCsvText, parseWorkbookBuffer, rowsToCsvText, rowsToWorkbookBuffer } from './spreadsheet';

/** Regression for the switch from SheetJS (`xlsx`, capped at an unpatched 0.18.5 on the npm
 * registry) to ExcelJS + a hand-rolled CSV reader/writer — proves both round-trip correctly,
 * including the special characters that make CSV quoting tricky. */
describe('spreadsheet', () => {
  it('round-trips plain rows through xlsx', async () => {
    const headers = ['Code', 'Name'];
    const rows = [
      ['A1', 'Espresso'],
      ['A2', 'Latte']
    ];

    const buffer = await rowsToWorkbookBuffer(headers, rows);
    const parsed = await parseWorkbookBuffer(buffer);

    expect(parsed).toEqual([
      { Code: 'A1', Name: 'Espresso' },
      { Code: 'A2', Name: 'Latte' }
    ]);
  });

  it('round-trips plain rows through csv', () => {
    const headers = ['Code', 'Name'];
    const rows = [
      ['A1', 'Espresso'],
      ['A2', 'Latte']
    ];

    const csv = rowsToCsvText(headers, rows);
    const parsed = parseCsvText(csv);

    expect(parsed).toEqual([
      { Code: 'A1', Name: 'Espresso' },
      { Code: 'A2', Name: 'Latte' }
    ]);
  });

  it('quotes and unquotes commas, quotes, and newlines in csv fields', () => {
    const headers = ['Note'];
    const rows = [['Contains, a comma'], ['Has "quotes" inside'], ['Multi\nline']];

    const csv = rowsToCsvText(headers, rows);
    const parsed = parseCsvText(csv);

    expect(parsed).toEqual([{ Note: 'Contains, a comma' }, { Note: 'Has "quotes" inside' }, { Note: 'Multi\nline' }]);
  });

  it('parses a leading UTF-8 BOM without keeping it in the first header', () => {
    const parsed = parseCsvText('﻿Code,Name\r\nA1,Espresso\r\n');

    expect(parsed).toEqual([{ Code: 'A1', Name: 'Espresso' }]);
  });
});
