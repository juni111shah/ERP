import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ImportDef, ImportResult, ImportsApi } from '@core/api/imports.api';
import { problemMessage } from '@core/http/problem-details';
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { countOf } from '@features/finance/finance-kpis';
import { importSummary, saveCsv } from '../../admin-import';

/** Bulk-load data from CSV: pick what to import, download the template, check the file (dry run), then import. */
@Component({
  selector: 'erp-import',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, NzIconModule, FormsModule, NzAlertModule, NzButtonModule, NzSelectModule, NzTableModule, PaginationBarComponent],
  styleUrl: './import.page.scss',
  templateUrl: './import.page.html',
})
export class ImportPage {
  private readonly api = inject(ImportsApi);
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
    this.api.definitions().subscribe({ next: (d) => this.defs.set(d), error: () => this.loadFailed.set(true) });
  }

  current(): ImportDef | undefined { return this.defs().find((d) => d.key === this.key()); }
  choose(key: string): void { this.key.set(key); this.result.set(null); }
  summary(r: ImportResult): string { return importSummary(r); }
  canImport(): boolean { const r = this.result(); return !!r && r.dryRun && r.created > 0; }

  template(): void {
    this.api.template(this.key()!).subscribe((csv) => saveCsv(csv, `${this.key()}-template.csv`));
  }

  run(input: HTMLInputElement, dryRun: boolean): void {
    const file = input.files?.[0];
    if (!file) { this.message.warning('Choose a CSV file first'); return; }
    this.busy.set(true);
    this.api.run(this.key()!, file, dryRun).subscribe({
      next: (r) => { this.busy.set(false); this.result.set(r); if (!dryRun) { this.message.success(`${countOf(r.created, 'row')} imported`); input.value = ''; } },
      error: (e) => { this.busy.set(false); this.message.error(problemMessage(e, 'The import failed')); },
    });
  }
}
