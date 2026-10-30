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
import { SessionStore } from '../core/session/session.store';
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

  private readonly session = inject(SessionStore);

  /** Prints the statement on its own page (a new window), with brand color and clean document layout. */
  print(): void {
    const d = this.data();
    if (!d) return;
    const n = (v: number) => (v ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const day = (v: string) => new Date(v + 'T00:00:00').toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
    const t = (k: string, f: string) => esc(this.i18n.t(k, f));
    const title = `${this.side() === 'customer' ? t('statement.customerTitle', 'Customer Statement of Account') : t('statement.vendorTitle', 'Vendor Statement of Account')}`;
    const ws = this.session.me()?.workspace;
    const orgName = esc(ws?.name || 'HigherSquare 360 ERP');
    const currency = esc(ws?.baseCurrency || 'QAR');
    const rows = d.lines.map((l) => `<tr>
      <td>${day(l.date)}</td>
      <td><span class="badge">${esc(this.kindLabel(l.kind))}</span></td>
      <td><strong>${esc(l.number)}</strong></td>
      <td>${esc(l.reference || '—')}</td>
      <td class="n">${l.debit ? n(l.debit) : '—'}</td>
      <td class="n">${l.credit ? n(l.credit) : '—'}</td>
      <td class="n balance"><strong>${n(l.balance)}</strong></td>
    </tr>`).join('');
    const dir = document.documentElement.dir || 'ltr';
    const html = `<!doctype html>
<html dir="${esc(dir)}">
<head>
  <meta charset="utf-8">
  <title>${title} · ${esc(this.partyName() || d.partyName)}</title>
  <style>
    @page { size: A4 portrait; margin: 12mm 15mm; }
    * { box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; margin: 0; padding: 20px; color: #0f172a; line-height: 1.45; font-size: 12px; }
    .sheet { max-width: 800px; margin: 0 auto; border-top: 5px solid #10955d; padding-top: 14px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; padding-bottom: 14px; border-bottom: 2px solid #10955d; }
    .org-name { font-size: 20px; font-weight: 800; color: #064e3b; margin: 0 0 4px; }
    .doc-title { font-size: 18px; font-weight: 800; color: #10955d; text-transform: uppercase; letter-spacing: 1px; margin: 0 0 4px; text-align: end; }
    .doc-dates { font-size: 11px; color: #64748b; text-align: end; }
    .party-block { margin: 18px 0; padding: 12px 16px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; }
    .party-label { font-size: 10px; text-transform: uppercase; color: #64748b; font-weight: 700; letter-spacing: 0.8px; margin-bottom: 4px; }
    .party-name { font-size: 15px; font-weight: 700; color: #0f172a; }
    table { width: 100%; border-collapse: collapse; margin-top: 14px; }
    th { background: #f0fdf4; color: #064e3b; border-top: 1px solid #bbf7d0; border-bottom: 2px solid #10955d; padding: 8px 8px; font-size: 10.5px; text-transform: uppercase; text-align: start; }
    td { border-bottom: 1px solid #e2e8f0; padding: 8px 8px; text-align: start; vertical-align: middle; }
    .n { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .balance { color: #064e3b; }
    .badge { display: inline-block; padding: 1px 6px; background: #e2e8f0; border-radius: 4px; font-size: 10px; font-weight: 600; }
    tr.sum td { font-weight: 700; background: #f8fafc; }
    tr.closing td { background: #f0fdf4; border-top: 2px solid #10955d; border-bottom: 2px solid #10955d; font-size: 13px; font-weight: 800; color: #064e3b; }
    .footer { margin-top: 24px; padding-top: 12px; border-top: 1px solid #e2e8f0; font-size: 10.5px; color: #64748b; display: flex; justify-content: space-between; }
  </style>
</head>
<body>
  <div class="sheet">
    <div class="header">
      <div>
        <div class="org-name">${orgName}</div>
        <div style="font-size: 11px; color: #64748b;">Financial Statement of Account</div>
      </div>
      <div>
        <div class="doc-title">${title}</div>
        <div class="doc-dates">${t('statement.period', 'Statement Period')}: <strong>${day(d.from)} → ${day(d.to)}</strong></div>
        <div class="doc-dates">Currency: <strong>${currency}</strong></div>
      </div>
    </div>
    <div class="party-block">
      <div class="party-label">${this.side() === 'customer' ? 'Account Holder / Customer' : 'Payee / Vendor'}</div>
      <div class="party-name">${esc(this.partyName() || d.partyName || 'All Records')}</div>
    </div>
    <table>
      <thead>
        <tr>
          <th>${t('statement.date', 'Date')}</th>
          <th>${t('statement.type', 'Type')}</th>
          <th>${t('statement.number', 'Number')}</th>
          <th>${t('statement.reference', 'Reference')}</th>
          <th class="n">${t('statement.debit', 'Debit')}</th>
          <th class="n">${t('statement.credit', 'Credit')}</th>
          <th class="n">${t('statement.balance', 'Balance')}</th>
        </tr>
      </thead>
      <tbody>
        <tr class="sum">
          <td>${day(d.from)}</td>
          <td colspan="5"><strong>${t('statement.opening', 'Opening Balance')}</strong></td>
          <td class="n balance"><strong>${n(d.openingBalance)}</strong></td>
        </tr>
        ${rows}
        <tr class="closing">
          <td>${day(d.to)}</td>
          <td colspan="3"><strong>${t('statement.closing', 'Closing Balance')}</strong></td>
          <td class="n">${n(d.totalDebit)}</td>
          <td class="n">${n(d.totalCredit)}</td>
          <td class="n balance"><strong>${n(d.closingBalance)}</strong></td>
        </tr>
      </tbody>
    </table>
    <div class="footer">
      <div>This statement reflects posted transactions as of ${new Date().toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })}.</div>
      <div>${orgName} · HigherSquare 360 ERP</div>
    </div>
  </div>
</body>
</html>`;
    const w = window.open('', '_blank');
    if (!w) { this.error.set(this.i18n.t('statement.popupBlocked', 'Allow pop-ups for this site to print the statement')); return; }
    w.document.open(); w.document.write(html); w.document.close();
    w.focus();
    w.print();
  }
}

