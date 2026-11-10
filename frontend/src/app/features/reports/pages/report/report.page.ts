import { HttpClient } from '@angular/common/http';
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
import { API_BASE_URL } from '@core/api/api-config';
import { toIsoDate } from '@core/format/dates';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { displayDate } from '@core/format/display';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { LEDGER_REPORT_LINKS, REPORTS, ReportColumn, ReportDef } from '../../report-registry';

/** Reports hub (no slug) and the generic report page (/reports/:slug): filters, table, summary, CSV and Print. */
import { RevealDirective } from '@shared/directives/reveal.directive';
@Component({
  selector: 'erp-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, StatCardComponent, FormsModule, RouterLink, HasPermissionDirective, NzTableModule, NzButtonModule, NzDatePickerModule, NzInputNumberModule, NzIconModule, NzSelectModule, RevealDirective],
  styleUrl: './report.page.scss',
  templateUrl: './report.page.html',
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
  readonly failed = signal<string | null>(null);
  readonly data = signal<unknown>(null);
  readonly columns = computed<ReportColumn[]>(() => this.def()?.columns(this.data()) ?? []);
  readonly rows = computed(() => (this.data() ? this.def()!.rows(this.data()) : []));
  readonly totals = computed(() => (this.data() ? this.def()?.totals?.(this.data()) ?? null : null));
  readonly details = computed(() => (this.data() ? this.def()?.details?.(this.data()) ?? [] : []));
  readonly summary = computed(() => (this.data() ? this.def()?.summary?.(this.data()) ?? [] : []));

  readonly entity = this.session.currentEntityId;

  constructor() {
    // Open with figures, not an empty page, unless the report first needs a choice from the user.
    effect(() => { this.entity(); if (this.def() && !this.def()!.filters.includes('owner')) untracked(() => this.run()); });
  }

  runAction(): void {
    const a = this.def()?.action; const entityId = this.entity();
    if (!a || !entityId || !this.to) return;
    const asOf = toIsoDate(this.to);
    this.http.post(`${this.base}/${a.path}`, { legalEntityId: entityId, asOf }).subscribe({ next: () => this.message.success(`${a.done} as of ${displayDate(asOf)}`), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not complete the action') });
  }

  has(f: string): boolean { return !!this.def()?.filters.includes(f as never); }
  isNum(c: ReportColumn): boolean { return c.type === 'money' || c.type === 'number' || c.type === 'percent'; }

  cell(row: Record<string, unknown>, c: ReportColumn): string {
    const v = row[c.key];
    if (v === null || v === undefined || v === '') return '';
    if (c.type === 'money') return Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (c.type === 'percent') return `${Number(v).toFixed(1)}%`;
    if (c.type === 'number') return Number(v).toLocaleString();
    return displayDate(v) || String(v);
  }

  readonly waitingForEntity = computed(() => !!this.def()?.needsEntity && !this.entity());

  run(): void {
    const def = this.def();
    if (!def || !this.to) return;
    if (this.waitingForEntity()) { this.data.set(null); this.failed.set(null); return; }
    const params: Record<string, string> = {};
    if (this.has('range')) { if (!this.from) return; params['from'] = toIsoDate(this.from); params['to'] = toIsoDate(this.to); }
    if (this.has('owner')) { if (!this.ownerId) { this.message.warning('Choose the property owner'); return; } params['ownerPartyId'] = this.ownerId; }
    if (this.has('asOf')) params['asOf'] = toIsoDate(this.to);
    if (this.has('weeks')) params['weeks'] = String(this.weeks);
    if (this.has('months')) params['months'] = String(this.months);
    if (this.has('days')) params['days'] = String(this.days);
    const entity = this.entity();
    if (entity) params['legalEntityId'] = entity;
    this.loading.set(true);
    this.failed.set(null);
    this.http.get(`${this.base}/${def.endpoint}`, { params }).subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.failed.set(problemMessage(e, 'The server did not return the report.')); },
    });
  }

  csv(): void {
    const cols = this.columns();
    const quote = (s: string) => `"${s.replace(/"/g, '""')}"`;
    const lines = [cols.map((c) => quote(c.label)).join(',')];
    for (const r of [...this.rows(), ...(this.totals() ? [this.totals()!] : [])]) lines.push(cols.map((c) => quote(String(r[c.key] ?? ''))).join(','));
    for (const sec of this.details()) {
      lines.push('', quote(sec.title), sec.columns.map((c) => quote(c.label)).join(','));
      for (const r of sec.rows) lines.push(sec.columns.map((c) => quote(String(r[c.key] ?? ''))).join(','));
    }
    const url = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a'); a.href = url; a.download = `${this.slug()}.csv`; a.click(); URL.revokeObjectURL(url);
  }

  print(): void { window.print(); }
}
