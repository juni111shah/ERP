import { Injectable, effect, signal } from '@angular/core';

export type TableDensity = 'compact' | 'default' | 'comfortable';

/** The three row heights a table can use, in the order the toggle lists them. */
export const TABLE_DENSITIES: readonly { key: TableDensity; labelKey: string; label: string; height: number }[] = [
  { key: 'compact', labelKey: 'table.densityCompact', label: 'Compact', height: 40 },
  { key: 'default', labelKey: 'table.densityDefault', label: 'Default', height: 48 },
  { key: 'comfortable', labelKey: 'table.densityComfortable', label: 'Comfortable', height: 56 },
];

const STORAGE_KEY = 'erp.table.density';

function saved(): TableDensity {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return TABLE_DENSITIES.some((d) => d.key === value) ? (value as TableDensity) : 'default';
  } catch { return 'default'; }
}

/** The row height the user chose. It applies to every table through `data-table-density` on the html element, and is kept in this browser. */
@Injectable({ providedIn: 'root' })
export class TableDensityService {
  readonly mode = signal<TableDensity>(saved());

  constructor() {
    effect(() => {
      if (typeof document !== 'undefined') document.documentElement.setAttribute('data-table-density', this.mode());
    });
  }

  set(mode: TableDensity): void {
    this.mode.set(mode);
    try { localStorage.setItem(STORAGE_KEY, mode); } catch { /* storage unavailable */ }
  }
}
