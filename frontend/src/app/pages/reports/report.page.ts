import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { StatCardComponent } from '../../shared/stat-card.component';
import { LEDGER_REPORT_LINKS, REPORTS, ReportColumn, ReportDef } from './report-registry';

/** Reports hub (no slug) and the generic report page (/reports/:slug): filters, table, summary, CSV and Print. */
import { RevealDirective } from '../../shared/reveal.directive';
@Component({
  selector: 'erp-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, FormsModule, RouterLink, HasPermissionDirective, NzTableModule, NzButtonModule, NzDatePickerModule, NzInputNumberModule, NzIconModule, NzEmptyModule, NzSelectModule, RevealDirective],
  styles: [`
    .hub { display:grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap:16px } .card { display:block; padding:14px 16px; border:1px solid var(--erp-color-border, #e5e8ec); border-radius:8px; background:var(--erp-color-surface, #fff); color:inherit } .card b { display:block; margin-bottom:4px } .card span { font-size:12px; color: var(--erp-color-text-secondary, #667) }
    .bar { display:flex; gap:12px; flex-wrap:wrap; align-items:center; margin:12px 0 } .spacer { flex:1 } .num { text-align:right } .total td { font-weight:600 }
    .kpis { display:flex; gap:24px; flex-wrap:wrap; margin-bottom:12px } .kpi { padding:8px 14px; border:1px solid var(--erp-color-border, #e5e8ec); border-radius:8px } .kpi b { display:block; font-size:18px }
    @media print { .bar button, .bar nz-date-picker, .bar nz-input-number { display:none } }
  `],
  template: `
    @if (!def()) {
      <h1 class="erp-page-title">Reports</h1>
      <div class="hub" data-testid="reports-hub">
        @for (r of available(); track r.slug) { <a class="card" [routerLink]="['/reports', r.slug]" [attr.data-testid]="'report-' + r.slug"><b>{{ r.title }}</b><span>{{ r.subtitle }}</span></a> }
        @for (l of ledgerLinks; track l.title) { <a class="card" [routerLink]="l.link" [queryParams]="{ tab: l.tab }"><b>{{ l.title }}</b><span>{{ l.note }}</span></a> }
      </div>
    } @else {
      <h1 class="erp-page-title">{{ def()!.title }}</h1>
      <p>{{ def()!.subtitle }}</p>
      <div class="bar" erpReveal>
        @if (has('owner')) {
          <nz-select style="width:260px" nzShowSearch nzServerSearch nzPlaceHolder="Property owner" [(ngModel)]="ownerId" (nzOnSearch)="searchOwners($event)" data-testid="report-owner">@for (o of owners(); track o.id) { <nz-option [nzValue]="o.id" [nzLabel]="o.name"></nz-option> }</nz-select>
        }
        @if (has('range')) { <nz-date-picker [(ngModel)]="from" nzPlaceHolder="From"></nz-date-picker><nz-date-picker [(ngModel)]="to" nzPlaceHolder="To"></nz-date-picker> }
        @if (has('asOf')) { <nz-date-picker [(ngModel)]="to" nzPlaceHolder="As of"></nz-date-picker> }
        @if (has('weeks')) { <nz-input-number [(ngModel)]="weeks" [nzMin]="1" [nzMax]="52" [nzPrecision]="0"></nz-input-number> <span>weeks</span> }
        @if (has('days')) { <nz-input-number [(ngModel)]="days" [nzMin]="1" [nzMax]="3650" [nzPrecision]="0"></nz-input-number> <span>days idle</span> }
        @if (has('months')) { <nz-input-number [(ngModel)]="months" [nzMin]="1" [nzMax]="36" [nzPrecision]="0"></nz-input-number> <span>months</span> }
        <button nz-button nzType="primary" [nzLoading]="loading()" (click)="run()" data-testid="report-run">Run</button>
        <span class="spacer"></span>
        @if (def()!.action; as a) { <button *erpHasPermission="a.permission" nz-button [disabled]="!data() || !entity()" (click)="runAction()" data-testid="report-action">{{ a.label }}</button> }
        <button nz-button (click)="csv()" [disabled]="!data()" data-testid="report-csv"><span nz-icon nzType="download"></span>CSV</button>
        <button nz-button (click)="print()" [disabled]="!data()"><span nz-icon nzType="printer"></span>Print</button>
      </div>
      @if (loading() && !data()) { <div class="erp-stat-grid erp-stat-grid--auto" data-testid="report-loading">@for (i of [0, 1, 2, 3]; track i) { <erp-stat-card label="" [loading]="true" [tone]="i" /> }</div> }
      @if (data(); as d) {
        @if (summary().length) { <div class="erp-stat-grid erp-stat-grid--auto" data-testid="report-summary">@for (s of summary(); track s.label; let i = $index) { <erp-stat-card [label]="s.label" [value]="s.value" [tone]="i" /> }</div> }
        <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="report-table">
          <thead><tr>@for (c of columns(); track c.key) { <th [class.num]="isNum(c)">{{ c.label }}</th> }</tr></thead>
          <tbody>
            @for (r of rows(); track $index) { <tr>@for (c of columns(); track c.key) { <td [class.num]="isNum(c)">{{ cell(r, c) }}</td> }</tr> }
            @if (totals(); as t) { <tr class="total">@for (c of columns(); track c.key) { <td [class.num]="isNum(c)">{{ cell(t, c) }}</td> }</tr> }
          </tbody>
        </nz-table>
      } @else if (!loading()) { <nz-empty nzNotFoundContent="Choose the filters and run the report."></nz-empty> }
    }`,
})
export class ReportPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly slug = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('slug'))), { initialValue: null });
  readonly def = computed<ReportDef | null>(() => REPORTS.find((r) => r.slug === this.slug()) ?? null);
  readonly available = computed(() => REPORTS.filter((r) => !r.module || this.session.isModuleEnabled(r.module)));
  readonly ledgerLinks = LEDGER_REPORT_LINKS;
  from: Date | null = new Date(new Date().getFullYear(), 0, 1);
  to: Date | null = new Date();
  weeks = 13;
  days = 90;
  ownerId: string | null = null;
  readonly owners = signal<{ id: string; name: string }[]>([]);
  searchOwners(term: string): void { this.http.get<{ items: { id: string; name: string }[] }>(`${this.base}/parties`, { params: { search: term, pageSize: 20 } }).subscribe((r) => this.owners.set(r.items)); }
  months = 12;
  readonly loading = signal(false);
  readonly data = signal<unknown>(null);
  readonly columns = computed<ReportColumn[]>(() => this.def()?.columns(this.data()) ?? []);
  readonly rows = computed(() => (this.data() ? this.def()!.rows(this.data()) : []));
  readonly totals = computed(() => (this.data() ? this.def()?.totals?.(this.data()) ?? null : null));
  readonly summary = computed(() => (this.data() ? this.def()?.summary?.(this.data()) ?? [] : []));

  readonly entity = this.session.currentEntityId;

  runAction(): void {
    const a = this.def()?.action; const entityId = this.entity();
    if (!a || !entityId || !this.to) return;
    this.http.post(`${this.base}/${a.path}`, { legalEntityId: entityId, asOf: toIsoDate(this.to) }).subscribe({ next: () => this.message.success(a.done), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not complete the action') });
  }

  has(f: string): boolean { return !!this.def()?.filters.includes(f as never); }
  isNum(c: ReportColumn): boolean { return c.type === 'money' || c.type === 'number' || c.type === 'percent'; }

  cell(row: Record<string, unknown>, c: ReportColumn): string {
    const v = row[c.key];
    if (v === null || v === undefined || v === '') return '';
    if (c.type === 'money') return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (c.type === 'percent') return `${Number(v).toFixed(1)}%`;
    if (c.type === 'number') return Number(v).toLocaleString();
    return String(v);
  }

  run(): void {
    const def = this.def();
    if (!def || !this.to) return;
    const params: Record<string, string> = {};
    if (this.has('range')) { if (!this.from) return; params['from'] = toIsoDate(this.from); params['to'] = toIsoDate(this.to); }
    if (this.has('owner')) { if (!this.ownerId) { this.message.warning('Choose the property owner'); return; } params['ownerPartyId'] = this.ownerId; }
    if (this.has('asOf')) params['asOf'] = toIsoDate(this.to);
    if (this.has('weeks')) params['weeks'] = String(this.weeks);
    if (this.has('months')) params['months'] = String(this.months);
    if (this.has('days')) params['days'] = String(this.days);
    this.loading.set(true);
    this.http.get(`${this.base}/${def.endpoint}`, { params }).subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.message.error(asProblem(e)?.detail ?? 'Could not run the report'); },
    });
  }

  csv(): void {
    const cols = this.columns();
    const quote = (s: string) => `"${s.replace(/"/g, '""')}"`;
    const lines = [cols.map((c) => quote(c.label)).join(',')];
    for (const r of [...this.rows(), ...(this.totals() ? [this.totals()!] : [])]) lines.push(cols.map((c) => quote(String(r[c.key] ?? ''))).join(','));
    const url = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a'); a.href = url; a.download = `${this.slug()}.csv`; a.click(); URL.revokeObjectURL(url);
  }

  print(): void { window.print(); }
}
