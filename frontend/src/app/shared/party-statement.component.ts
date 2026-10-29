import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { API_BASE_URL } from '../core/api/api-config';
import { I18nService } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { RevealDirective } from './reveal.directive';
import { EmailDocumentDrawerComponent } from './email-document-drawer.component';
import { HasPermissionDirective } from './has-permission.directive';

export interface PartyLedgerLine { date: string; kind: 'invoice' | 'creditNote' | 'receipt' | 'bill' | 'payment' | string; documentId: string; number: string; reference: string | null; currencyCode: string; debit: number; credit: number; balance: number }
export interface PartyLedger { partyId: string; partyName: string | null; side: 'customer' | 'vendor'; from: string; to: string; openingBalance: number; lines: PartyLedgerLine[]; totalDebit: number; totalCredit: number; closingBalance: number }

const KIND_ROUTES: Record<string, string> = { invoice: '/finance/invoices', creditNote: '/finance/invoices', receipt: '/finance/receipts', bill: '/finance/bills', payment: '/finance/payments' };
const KIND_LABELS: Record<string, string> = { invoice: 'Invoice', creditNote: 'Credit note', receipt: 'Receipt', bill: 'Bill', payment: 'Payment' };

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const esc = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);

/**
 * Statement of account for one party and side (customer: invoices, credit notes, receipts; vendor: bills, payments), with a running
 * balance from GET /finance/reports/{customer|vendor}-ledger. Defaults to the start of this year up to today; prints in its own window.
 */
@Component({
  selector: 'erp-party-statement',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzIconModule, NzTableModule, TranslatePipe, RevealDirective, EmailDocumentDrawerComponent, HasPermissionDirective],
  styles: [`.bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 12px; } .num { text-align: end; white-space: nowrap; } .sum td { font-weight: 600; } .err { color: var(--erp-color-error, #cf1322); }`],
  template: `
    <div class="bar" erpReveal>
      <nz-range-picker [ngModel]="range()" (ngModelChange)="setRange($event)" [nzAllowClear]="false" data-testid="statement-range"></nz-range-picker>
      <button nz-button (click)="load()" [nzLoading]="loading()" data-testid="statement-refresh"><span nz-icon nzType="reload"></span>{{ 'statement.refresh' | translate: 'Refresh' }}</button>
      <button nz-button (click)="print()" [disabled]="!data()" data-testid="statement-print"><span nz-icon nzType="printer"></span>{{ 'statement.print' | translate: 'Print' }}</button>
      <button *erpHasPermission="'core.email.send'" nz-button (click)="emailOpen.set(true)" data-testid="statement-email"><span nz-icon nzType="mail"></span>{{ 'statement.email' | translate: 'Email' }}</button>
    </div>
    <erp-email-document-drawer [open]="emailOpen()" [path]="'parties/' + partyId() + '/statement/email'" [params]="emailParams()" (closed)="emailOpen.set(false)" />
    @if (error()) { <p class="err" data-testid="statement-error">{{ error() }}</p> }
    <nz-table #t [nzData]="data()?.lines ?? []" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzLoading]="loading()" [nzScroll]="{ x: '720px' }" data-testid="party-ledger">
      <thead><tr>
        <th>{{ 'statement.date' | translate: 'Date' }}</th><th>{{ 'statement.type' | translate: 'Type' }}</th><th>{{ 'statement.number' | translate: 'Number' }}</th><th>{{ 'statement.reference' | translate: 'Reference' }}</th>
        <th class="num">{{ 'statement.debit' | translate: 'Debit' }}</th><th class="num">{{ 'statement.credit' | translate: 'Credit' }}</th><th class="num">{{ 'statement.balance' | translate: 'Balance' }}</th>
      </tr></thead>
      <tbody>
        @if (data(); as d) {
          <tr class="sum"><td>{{ d.from | date: 'dd MMM yyyy' }}</td><td colspan="5">{{ 'statement.opening' | translate: 'Opening balance' }}</td><td class="num">{{ d.openingBalance | number: '1.2-2' }}</td></tr>
          @for (l of t.data; track l.documentId + l.kind) {
            <tr>
              <td>{{ l.date | date: 'dd MMM yyyy' }}</td>
              <td>{{ kindLabel(l.kind) }}</td>
              <td>@if (route(l.kind); as r) { <a [routerLink]="[r, l.documentId]">{{ l.number }}</a> } @else { {{ l.number }} }</td>
              <td>{{ l.reference }}</td>
              <td class="num">{{ l.debit ? (l.debit | number: '1.2-2') : '' }}</td>
              <td class="num">{{ l.credit ? (l.credit | number: '1.2-2') : '' }}</td>
              <td class="num">{{ l.balance | number: '1.2-2' }}</td>
            </tr>
          } @empty { <tr><td colspan="7" style="text-align: center; color: #999;">{{ 'statement.empty' | translate: 'No transactions in this period' }}</td></tr> }
          <tr class="sum"><td>{{ d.to | date: 'dd MMM yyyy' }}</td><td colspan="3">{{ 'statement.closing' | translate: 'Closing balance' }}</td>
            <td class="num">{{ d.totalDebit | number: '1.2-2' }}</td><td class="num">{{ d.totalCredit | number: '1.2-2' }}</td><td class="num" data-testid="statement-closing">{{ d.closingBalance | number: '1.2-2' }}</td></tr>
        }
      </tbody>
    </nz-table>
    <p style="color: #999; margin-top: 8px;">{{ hint() }}</p>
  `,
})
export class PartyStatementComponent {
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly i18n = inject(I18nService);

  readonly partyId = input.required<string>();
  readonly emailOpen = signal(false);
  readonly emailParams = computed(() => ({ asOf: iso(this.range()[1]) }));
  readonly side = input.required<'customer' | 'vendor'>();
  readonly partyName = input<string>('');

  readonly range = signal<Date[]>([new Date(new Date().getFullYear(), 0, 1), new Date()]);
  readonly data = signal<PartyLedger | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly hint = computed(() => this.side() === 'customer'
    ? this.i18n.t('statement.hintCustomer', 'Positive balance: the customer owes you. Draft and voided documents are not included.')
    : this.i18n.t('statement.hintVendor', 'Positive balance: you owe the vendor. Draft and voided documents are not included.'));

  private readonly autoLoad = effect(() => { this.partyId(); this.side(); untracked(() => this.load()); });

  kindLabel(kind: string): string { return this.i18n.t(`statement.kind.${kind}`, KIND_LABELS[kind] ?? kind); }
  route(kind: string): string | null { return KIND_ROUTES[kind] ?? null; }

  setRange(r: Date[] | null): void {
    if (!r || r.length !== 2 || !r[0] || !r[1]) return;
    this.range.set(r);
    this.load();
  }

  load(): void {
    const [from, to] = this.range();
    this.loading.set(true);
    this.error.set(null);
    this.http.get<PartyLedger>(`${this.apiBase}/finance/reports/${this.side()}-ledger`, { params: { partyId: this.partyId(), from: iso(from), to: iso(to) } }).subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: (e) => { this.data.set(null); this.loading.set(false); this.error.set(e?.error?.detail || e?.error?.title || this.i18n.t('statement.failed', 'Could not load the statement')); },
    });
  }

  /** Prints the statement on its own page (a new window), so the app shell and the page layout are not part of the print. */
  print(): void {
    const d = this.data();
    if (!d) return;
    const n = (v: number) => (v ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const day = (v: string) => new Date(v + 'T00:00:00').toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
    const t = (k: string, f: string) => esc(this.i18n.t(k, f));
    const title = `${this.side() === 'customer' ? t('statement.customerTitle', 'Customer statement') : t('statement.vendorTitle', 'Vendor statement')}`;
    const rows = d.lines.map((l) => `<tr><td>${day(l.date)}</td><td>${esc(this.kindLabel(l.kind))}</td><td>${esc(l.number)}</td><td>${esc(l.reference)}</td><td class="n">${l.debit ? n(l.debit) : ''}</td><td class="n">${l.credit ? n(l.credit) : ''}</td><td class="n">${n(l.balance)}</td></tr>`).join('');
    const dir = document.documentElement.dir || 'ltr';
    const html = `<!doctype html><html dir="${esc(dir)}"><head><meta charset="utf-8"><title>${title} · ${esc(this.partyName() || d.partyName)}</title><style>
      body { font: 12px/1.4 system-ui, sans-serif; margin: 24px; color: #000; } h1 { font-size: 18px; margin: 0 0 4px; } p { margin: 0 0 12px; }
      table { width: 100%; border-collapse: collapse; } th, td { border-bottom: 1px solid #ddd; padding: 4px 6px; text-align: start; } .n { text-align: end; white-space: nowrap; }
      tr.s td { font-weight: 600; } @page { margin: 12mm; } thead { display: table-header-group; } tr { break-inside: avoid; }
    </style></head><body>
      <h1>${title}</h1><p><b>${esc(this.partyName() || d.partyName)}</b><br>${day(d.from)} – ${day(d.to)}</p>
      <table><thead><tr><th>${t('statement.date', 'Date')}</th><th>${t('statement.type', 'Type')}</th><th>${t('statement.number', 'Number')}</th><th>${t('statement.reference', 'Reference')}</th><th class="n">${t('statement.debit', 'Debit')}</th><th class="n">${t('statement.credit', 'Credit')}</th><th class="n">${t('statement.balance', 'Balance')}</th></tr></thead>
      <tbody><tr class="s"><td>${day(d.from)}</td><td colspan="5">${t('statement.opening', 'Opening balance')}</td><td class="n">${n(d.openingBalance)}</td></tr>${rows}
      <tr class="s"><td>${day(d.to)}</td><td colspan="3">${t('statement.closing', 'Closing balance')}</td><td class="n">${n(d.totalDebit)}</td><td class="n">${n(d.totalCredit)}</td><td class="n">${n(d.closingBalance)}</td></tr></tbody></table></body></html>`;
    const w = window.open('', '_blank');
    if (!w) { this.error.set(this.i18n.t('statement.popupBlocked', 'Allow pop-ups for this site to print the statement')); return; }
    w.document.open(); w.document.write(html); w.document.close();
    w.focus();
    w.print();
  }
}
