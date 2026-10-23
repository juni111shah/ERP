import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { API_BASE_URL } from '../../core/api/api-config';
import { asProblem } from '../../core/http/problem-details';

interface ImportDef { key: string; label: string; columns: { name: string; required: boolean; example: string; hint?: string }[]; }
interface ImportResult { total: number; created: number; errors: { row: number; message: string }[]; dryRun: boolean; }

/** Bulk-load data from CSV: pick what to import, download the template, check the file (dry run), then import. */
@Component({
  selector: 'erp-import',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzAlertModule, NzButtonModule, NzSelectModule, NzTableModule],
  template: `
    <h1 class="erp-page-title">Data import</h1>
    <div style="display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin-bottom:16px">
      <nz-select style="width:280px" nzPlaceHolder="What do you want to import?" [ngModel]="key()" (ngModelChange)="choose($event)" data-testid="import-type">
        @for (d of defs(); track d.key) { <nz-option [nzValue]="d.key" [nzLabel]="d.label"></nz-option> }
      </nz-select>
      @if (current(); as d) { <button nz-button (click)="template()" data-testid="import-template">Download CSV template</button> }
    </div>
    @if (current(); as d) {
      <nz-table [nzData]="d.columns" nzSize="small" [nzShowPagination]="false" style="max-width:760px;margin-bottom:16px">
        <thead><tr><th>Column</th><th>Required</th><th>Example</th><th>Notes</th></tr></thead>
        <tbody>@for (c of d.columns; track c.name) { <tr><td><code>{{ c.name }}</code></td><td>{{ c.required ? 'Yes' : '' }}</td><td>{{ c.example }}</td><td>{{ c.hint }}</td></tr> }</tbody>
      </nz-table>
      <div style="display:flex;gap:12px;align-items:center;flex-wrap:wrap">
        <input type="file" accept=".csv,text/csv" #file (change)="result.set(null)" data-testid="import-file" />
        <button nz-button [nzLoading]="busy()" (click)="run(file, true)" data-testid="import-check">Check file</button>
        <button nz-button nzType="primary" [nzLoading]="busy()" [disabled]="!canImport()" (click)="run(file, false)" data-testid="import-run">Import</button>
      </div>
    }
    @if (result(); as r) {
      <nz-alert style="margin-top:16px;max-width:760px" [nzType]="r.errors.length ? 'warning' : 'success'" nzShowIcon data-testid="import-result"
                [nzMessage]="r.dryRun ? (r.created + ' of ' + r.total + ' rows are ready to import' + (r.errors.length ? ', ' + r.errors.length + ' have problems' : '')) : (r.created + ' of ' + r.total + ' rows imported' + (r.errors.length ? ', ' + r.errors.length + ' skipped' : ''))"></nz-alert>
      @if (r.errors.length) {
        <nz-table [nzData]="r.errors" nzSize="small" style="max-width:760px;margin-top:8px" data-testid="import-errors">
          <thead><tr><th style="width:80px">Row</th><th>Problem</th></tr></thead>
          <tbody>@for (e of r.errors; track e.row) { <tr><td>{{ e.row }}</td><td>{{ e.message }}</td></tr> }</tbody>
        </nz-table>
      }
    }`,
})
export class ImportPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly message = inject(NzMessageService);
  readonly defs = signal<ImportDef[]>([]);
  readonly key = signal<string | null>(null);
  readonly result = signal<ImportResult | null>(null);
  readonly busy = signal(false);

  constructor() { this.http.get<ImportDef[]>(`${this.base}/imports`).subscribe((d) => this.defs.set(d)); }

  current(): ImportDef | undefined { return this.defs().find((d) => d.key === this.key()); }
  choose(key: string): void { this.key.set(key); this.result.set(null); }
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
      next: (r) => { this.busy.set(false); this.result.set(r); if (!dryRun) { this.message.success(`${r.created} row(s) imported`); input.value = ''; } },
      error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'The import failed'); },
    });
  }
}
