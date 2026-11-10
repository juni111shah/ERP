import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { API_BASE_URL } from '@core/api/api-config';
import { problemMessage } from '@core/http/problem-details';
import { countOf } from '@features/finance/finance-kpis';

interface ImportDef { key: string; label: string; columns: { name: string; required: boolean; example: string; hint?: string }[]; }
interface ImportResult { total: number; created: number; errors: { row: number; message: string }[]; dryRun: boolean; }

/** Bulk-load data from CSV: pick what to import, download the template, check the file (dry run), then import. */
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';

@Component({
  selector: 'erp-import',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, NzIconModule, FormsModule, NzAlertModule, NzButtonModule, NzSelectModule, NzTableModule, PaginationBarComponent],
  styleUrl: './import.page.scss',
  templateUrl: './import.page.html',
})
export class ImportPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly message = inject(NzMessageService);
  readonly defs = signal<ImportDef[]>([]);
  readonly key = signal<string | null>(null);
  readonly result = signal<ImportResult | null>(null);
  readonly busy = signal(false);
  readonly errPg = new ClientPager(10);
  readonly loadFailed = signal(false);

  constructor() { this.load(); }

  load(): void {
    this.loadFailed.set(false);
    this.http.get<ImportDef[]>(`${this.base}/imports`).subscribe({ next: (d) => this.defs.set(d), error: () => this.loadFailed.set(true) });
  }

  current(): ImportDef | undefined { return this.defs().find((d) => d.key === this.key()); }
  choose(key: string): void { this.key.set(key); this.result.set(null); }
  summary(r: ImportResult): string {
    const of = `${r.created} of ${countOf(r.total, 'row')}`;
    if (r.dryRun) return `${of} ${r.created === 1 ? 'is' : 'are'} ready to import${r.errors.length ? `, ${r.errors.length} ${r.errors.length === 1 ? 'has a problem' : 'have problems'}` : ''}`;
    return `${of} imported${r.errors.length ? `, ${r.errors.length} skipped` : ''}`;
  }
  canImport(): boolean { const r = this.result(); return !!r && r.dryRun && r.created > 0; }

  template(): void {
    this.http.get(`${this.base}/imports/${this.key()}/template`, { responseType: 'text' }).subscribe((csv) => {
      const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
      const a = document.createElement('a'); a.href = url; a.download = `${this.key()}-template.csv`; a.click(); URL.revokeObjectURL(url);
    });
  }

  run(input: HTMLInputElement, dryRun: boolean): void {
    const file = input.files?.[0];
    if (!file) { this.message.warning('Choose a CSV file first'); return; }
    const form = new FormData(); form.append('file', file);
    this.busy.set(true);
    this.http.post<ImportResult>(`${this.base}/imports/${this.key()}`, form, { params: { dryRun } }).subscribe({
      next: (r) => { this.busy.set(false); this.result.set(r); if (!dryRun) { this.message.success(`${countOf(r.created, 'row')} imported`); input.value = ''; } },
      error: (e) => { this.busy.set(false); this.message.error(problemMessage(e, 'The import failed')); },
    });
  }
}
