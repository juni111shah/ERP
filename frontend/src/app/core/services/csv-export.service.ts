import { Injectable } from '@angular/core';
import * as Papa from 'papaparse';

/** One exported column: the row field to read (a dot path for nested values) and its heading in the file. */
export interface CsvColumn { field: string; header: string; }

type CsvRow = Record<string, unknown>;

/** A formatMoney() display value: ISO currency code, a space, then an en-US number ("AED 6,500.00", "AED -1,234.50"). */
const MONEY = /^([A-Z]{3}) (-?\d{1,3}(?:,\d{3})*(?:\.\d+)?)$/;
const PLAIN_NUMBER = /^-?\d[\d,]*(\.\d+)?$/;
const FORMULA_START = /^[=+\-@\t\r]/;

/** Downloads rows or an on-screen table as a CSV file, safe to open in a spreadsheet. */
@Injectable({
  providedIn: 'root'
})
export class CsvExportService {
  exportToCsv<T>(data: T[], filename: string, columns?: CsvColumn[]): void {
    if (!data || data.length === 0) {
      console.warn('No data to export');
      return;
    }
    const csvData = columns && columns.length > 0 ? columnRows(data, columns) : plainRows(data);
    const csv = Papa.unparse(csvData, {
      header: true,
      skipEmptyLines: true
    });
    downloadCsv(csv, filename);
  }

  exportTableToCsv(tableId: string, filename: string): void {
    const table = document.getElementById(tableId);
    if (!table) {
      console.error(`Table with id ${tableId} not found`);
      return;
    }
    const rows: string[][] = [];
    const headers = cellTexts(table.querySelectorAll('thead th'));
    if (headers.length > 0) rows.push(headers);
    table.querySelectorAll('tbody tr').forEach((row) => {
      const rowData = cellTexts(row.querySelectorAll('td'));
      if (rowData.length > 0) rows.push(rowData);
    });
    downloadCsv(Papa.unparse(rows), filename);
  }
}

/**
 * The chosen columns of each row, under their headings. Money cells arrive display-formatted ("AED -6,500.00"); they are
 * exported as plain numbers plus one Currency column.
 */
function columnRows<T>(data: T[], columns: CsvColumn[]): CsvRow[] {
  const moneyCols = moneyColumns(data, columns);
  const addCurrency = moneyCols.size > 0 && !columns.some((col) => col.header.toLowerCase() === 'currency');
  return data.map((row) => {
    const obj: CsvRow = {};
    let currency = '';
    for (const col of columns) {
      const value = nestedValue(row, col.field);
      const money = moneyCols.has(col.field) && typeof value === 'string' ? MONEY.exec(value.trim()) : null;
      if (money) {
        currency ||= money[1];
        obj[col.header] = money[2].replace(/,/g, '');
      } else {
        obj[col.header] = safeCell(value ?? '');
      }
    }
    if (addCurrency) obj['Currency'] = currency;
    return obj;
  });
}

/** Every field of each row, as it is. */
function plainRows<T>(data: T[]): CsvRow[] {
  return data.map((row) => {
    const obj: CsvRow = {};
    Object.entries(row as CsvRow).forEach(([k, v]) => { obj[k] = safeCell(v); });
    return obj;
  });
}

/** The columns whose filled cells are all formatted money. */
function moneyColumns<T>(data: T[], columns: CsvColumn[]): Set<string> {
  return new Set(columns.filter((col) => {
    const vals = data.map((row) => nestedValue(row, col.field)).filter((v) => v !== null && v !== undefined && v !== '');
    return vals.length > 0 && vals.every((v) => typeof v === 'string' && MONEY.test(v.trim()));
  }).map((col) => col.field));
}

/** The trimmed, formula-safe text of each cell. */
function cellTexts(cells: NodeListOf<Element>): string[] {
  const texts: string[] = [];
  cells.forEach((cell) => texts.push(safeCell((cell.textContent || '').trim())));
  return texts;
}

/**
 * Guards against spreadsheet formula injection: text starting with = + - @ tab or CR gets a leading single quote.
 * Numbers, and strings that are plain numbers such as "-12.50" or "-1,234", are left as they are.
 */
function safeCell<V>(value: V): V | string {
  if (typeof value !== 'string' || value === '') return value;
  if (PLAIN_NUMBER.test(value.trim())) return value;
  return FORMULA_START.test(value) ? `'${value}` : value;
}

/** The value at a dot path ("party.name"); undefined when any step is missing. */
function nestedValue(obj: unknown, path: string): unknown {
  return path.split('.').reduce<unknown>((current, key) => (current as CsvRow | null | undefined)?.[key], obj);
}

function downloadCsv(csv: string, filename: string): void {
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const link = document.createElement('a');
  if (link.download !== undefined) {
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', `${filename}.csv`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }
}
