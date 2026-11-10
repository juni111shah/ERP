import { ImportResult } from '@core/api/imports.api';
import { countOf } from '@features/finance/finance-kpis';

/** What a check or an import did, in one line: "3 of 4 rows are ready to import, 1 has a problem". */
export function importSummary(r: ImportResult): string {
  const of = `${r.created} of ${countOf(r.total, 'row')}`;
  if (r.dryRun) return `${of} ${r.created === 1 ? 'is' : 'are'} ready to import${r.errors.length ? `, ${r.errors.length} ${r.errors.length === 1 ? 'has a problem' : 'have problems'}` : ''}`;
  return `${of} imported${r.errors.length ? `, ${r.errors.length} skipped` : ''}`;
}

/** Hands CSV text to the browser as a file download. */
export function saveCsv(csv: string, fileName: string): void {
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
  const a = document.createElement('a'); a.href = url; a.download = fileName; a.click(); URL.revokeObjectURL(url);
}
