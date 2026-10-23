import { Injectable } from '@angular/core';
import * as Papa from 'papaparse';

@Injectable({
  providedIn: 'root'
})
export class CsvExportService {
  exportToCsv<T>(data: T[], filename: string, columns?: { field: string; header: string }[]): void {
    if (!data || data.length === 0) {
      console.warn('No data to export');
      return;
    }

    let csvData: any[];
    
    if (columns && columns.length > 0) {
      // Money cells arrive display-formatted ("AED -6,500.00"); export them as plain numbers plus one Currency column.
      const moneyCols = new Set(columns.filter(col => {
        const vals = data.map(row => this.getNestedProperty(row, col.field)).filter(v => v !== null && v !== undefined && v !== '');
        return vals.length > 0 && vals.every(v => typeof v === 'string' && CsvExportService.MONEY.test(v.trim()));
      }).map(col => col.field));
      const addCurrency = moneyCols.size > 0 && !columns.some(col => col.header.toLowerCase() === 'currency');
      csvData = data.map(row => {
        const obj: any = {};
        let currency = '';
        columns.forEach(col => {
          const value = this.getNestedProperty(row, col.field);
          const money = moneyCols.has(col.field) && typeof value === 'string' ? CsvExportService.MONEY.exec(value.trim()) : null;
          if (money) {
            currency ||= money[1];
            obj[col.header] = money[2].replace(/,/g, '');
          } else {
            obj[col.header] = this.safeCell(value ?? '');
          }
        });
        if (addCurrency) obj['Currency'] = currency;
        return obj;
      });
    } else {
      csvData = data.map(row => {
        const obj: any = {};
        Object.entries(row as any).forEach(([k, v]) => { obj[k] = this.safeCell(v); });
        return obj;
      });
    }

    const csv = Papa.unparse(csvData, {
      header: true,
      skipEmptyLines: true
    });

    this.downloadCsv(csv, filename);
  }

  /** A formatMoney() display value: ISO currency code, a space, then an en-US number ("AED 6,500.00", "AED -1,234.50"). */
  private static readonly MONEY = /^([A-Z]{3}) (-?\d{1,3}(?:,\d{3})*(?:\.\d+)?)$/;

  /**
   * Guards against spreadsheet formula injection: text starting with = + - @ tab or CR gets a leading single quote.
   * Numbers, and strings that are plain numbers such as "-12.50" or "-1,234", are left as they are.
   */
  private safeCell(value: any): any {
    if (typeof value !== 'string' || value === '') return value;
    if (/^-?\d[\d,]*(\.\d+)?$/.test(value.trim())) return value;
    return /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
  }

  private getNestedProperty(obj: any, path: string): any {
    return path.split('.').reduce((current, key) => current?.[key], obj);
  }

  private downloadCsv(csv: string, filename: string): void {
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

  exportTableToCsv(tableId: string, filename: string): void {
    const table = document.getElementById(tableId);
    if (!table) {
      console.error(`Table with id ${tableId} not found`);
      return;
    }

    const rows: string[][] = [];
    const headerCells = table.querySelectorAll('thead th');
    const headers: string[] = [];
    
    headerCells.forEach(cell => {
      headers.push(this.safeCell((cell.textContent || '').trim()));
    });
    
    if (headers.length > 0) {
      rows.push(headers);
    }

    const bodyRows = table.querySelectorAll('tbody tr');
    bodyRows.forEach(row => {
      const cells = row.querySelectorAll('td');
      const rowData: string[] = [];
      cells.forEach(cell => {
        rowData.push(this.safeCell((cell.textContent || '').trim()));
      });
      if (rowData.length > 0) {
        rows.push(rowData);
      }
    });

    const csv = Papa.unparse(rows);
    this.downloadCsv(csv, filename);
  }
}
