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
      csvData = data.map(row => {
        const obj: any = {};
        columns.forEach(col => {
          const value = this.getNestedProperty(row, col.field);
          obj[col.header] = this.safeCell(value ?? '');
        });
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
