import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { map } from 'rxjs';
import { ReportsApi } from '@core/api/reports.api';
import { toIsoDate } from '@core/format/dates';
import { displayDate } from '@core/format/display';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { LEDGER_REPORT_LINKS, REPORTS, ReportColumn, ReportDef, ReportRow, reportPermission } from '../../report-registry';

/** A cell as the table shows it: money and numbers formatted, dates in the display format. */
function cellText(v: unknown, c: ReportColumn): string {
  if (v === null || v === undefined || v === '') return '';
  if (c.type === 'money') return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  if (c.type === 'percent') return `${Number(v).toFixed(1)}%`;
  if (c.type === 'number') return Number(v).toLocaleString();
  return displayDate(v) || String(v);
}

/** The report as CSV: header, rows, the totals row, then each detail table under its title. */
function reportCsv(cols: ReportColumn[], rows: ReportRow[], details: { title: string; columns: ReportColumn[]; rows: ReportRow[] }[]): string {
  const quote = (s: string) => `"${s.replace(/"/g, '""')}"`;
  const lines = [cols.map((c) => quote(c.label)).join(',')];
  for (const r of rows) lines.push(cols.map((c) => quote(String(r[c.key] ?? ''))).join(','));
  for (const sec of details) {
    lines.push('', quote(sec.title), sec.columns.map((c) => quote(c.label)).join(','));
    for (const r of sec.rows) lines.push(sec.columns.map((c) => quote(String(r[c.key] ?? ''))).join(','));
  }
  return lines.join('\n');
}

/** Saves text as a file in the browser. */
function download(text: string, fileName: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a'); a.href = url; a.download = fileName; a.click(); URL.revokeObjectURL(url);
}

/** Reports hub (no slug) and the generic report page (/reports/:slug): filters, table, summary, CSV and Print. */
@Component({
  selector: 'erp-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputNumberModule, NzSelectModule, NzTableModule, StatCardComponent, StatePanelComponent, HasPermissionDirective, RevealDirective],
  styleUrl: './report.page.scss',
  templateUrl: './report.page.html',
})
export class ReportPage {
  private readonly api = inject(ReportsApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly slug = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('slug'))), { initialValue: null });
  readonly def = computed<ReportDef | null>(() => REPORTS.find((r) => r.slug === this.slug()) ?? null);
  /** Reports of enabled modules whose endpoint the user may read. */
  readonly available = computed(() => REPORTS.filter((r) => (!r.module || this.session.isModuleEnabled(r.module)) && this.session.hasPermission(reportPermission(r))));
  /** The open report's read permission (gates Run). */
  readonly runPermission = computed(() => { const d = this.def(); return d ? reportPermission(d) : null; });
  readonly ledgerLinks = LEDGER_REPORT_LINKS;
  from: Date | null = new Date(new Date().getFullYear(), 0, 1);
  to: Date | null = new Date();
  weeks = 13;
  days = 90;
  ownerId: string | null = null;
  readonly owners = signal<{ id: string; name: string }[]>([]);
  months = 12;
  readonly loading = signal(false);
  readonly failed = signal<string | null>(null);
  readonly data = signal<unknown>(null);
  readonly columns = computed<ReportColumn[]>(() => this.def()?.columns(this.data()) ?? []);
  readonly rows = computed(() => (this.data() ? this.def()!.rows(this.data()) : []));
  readonly totals = computed(() => (this.data() ? this.def()?.totals?.(this.data()) ?? null : null));
  readonly details = computed(() => (this.data() ? this.def()?.details?.(this.data()) ?? [] : []));
  readonly summary = computed(() => (this.data() ? this.def()?.summary?.(this.data()) ?? [] : []));

  readonly entity = this.session.currentEntityId;
  readonly waitingForEntity = computed(() => !!this.def()?.needsEntity && !this.entity());

  constructor() {
    // Open with figures, not an empty page, unless the report first needs a choice from the user.
    effect(() => { this.entity(); if (this.def() && !this.def()!.filters.includes('owner') && this.session.hasPermission(reportPermission(this.def()!))) untracked(() => this.run()); });
  }

  searchOwners(term: string): void { this.api.searchParties(term).subscribe((r) => this.owners.set(r.items)); }

  runAction(): void {
    const a = this.def()?.action; const entityId = this.entity();
    if (!a || !entityId || !this.to) return;
    const asOf = toIsoDate(this.to);
    this.api.action(a.path, entityId, asOf).subscribe({ next: () => this.message.success(`${a.done} as of ${displayDate(asOf)}`), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not complete the action') });
  }

  has(f: string): boolean { return !!this.def()?.filters.includes(f as never); }
  isNum(c: ReportColumn): boolean { return c.type === 'money' || c.type === 'number' || c.type === 'percent'; }
  cell(row: Record<string, unknown>, c: ReportColumn): string { return cellText(row[c.key], c); }

  run(): void {
    const def = this.def();
    if (!def || !this.to) return;
    if (this.waitingForEntity()) { this.data.set(null); this.failed.set(null); return; }
    const params = this.filterParams();
    if (!params) return;
    this.loading.set(true);
    this.failed.set(null);
    this.api.run(def.endpoint, params).subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.failed.set(problemMessage(e, 'The server did not return the report.')); },
    });
  }

  csv(): void {
    const totals = this.totals();
    download(reportCsv(this.columns(), [...this.rows(), ...(totals ? [totals] : [])], this.details()), `${this.slug()}.csv`);
  }

  print(): void { window.print(); }

  /** The query of the report's filters, or null when one it needs is missing (a warning says which). */
  private filterParams(): Record<string, string> | null {
    const params: Record<string, string> = {};
    if (this.has('range')) { if (!this.from) return null; params['from'] = toIsoDate(this.from); params['to'] = toIsoDate(this.to!); }
    if (this.has('owner')) { if (!this.ownerId) { this.message.warning('Choose the property owner'); return null; } params['ownerPartyId'] = this.ownerId; }
    if (this.has('asOf')) params['asOf'] = toIsoDate(this.to!);
    if (this.has('weeks')) params['weeks'] = String(this.weeks);
    if (this.has('months')) params['months'] = String(this.months);
    if (this.has('days')) params['days'] = String(this.days);
    const entity = this.entity();
    if (entity) params['legalEntityId'] = entity;
    return params;
  }
}
