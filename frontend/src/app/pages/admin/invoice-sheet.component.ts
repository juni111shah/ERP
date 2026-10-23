import { ChangeDetectionStrategy, Component, Injectable, computed, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';

/** The invoice fields the sheet renders (a subset of InvoiceDto, so the list drawer and the detail page can share it). */
export interface InvoiceSheetData {
  id: string; number: string; isCreditNote: boolean; partyId: string; partyName?: string | null; invoiceDate: string; dueDate: string; currencyCode: string;
  net: number; tax: number; total: number; paid: number; status: string; sourceType?: string | null; sourceId?: string | null; creditNoteForId?: string | null; notes?: string | null;
  lines: { id?: string; seq?: number; description: string; quantity: number; price: number; taxCodeId?: string | null; taxAmount?: number; net?: number }[];
}

/** Issuer and party details for an invoice, read from the print endpoint (same permission as reading the invoice). */
export interface InvoiceExtras {
  header: { organizationName: string; legalEntityName: string; legalEntityAddress?: string | null; taxRegistrationNo?: string | null; phone?: string | null; email?: string | null; countryCode: string };
  partyAddress?: string | null; partyTaxNumber?: string | null; partyPhone?: string | null; partyEmail?: string | null;
  /** Set on a credit note linked to an invoice (SourceType "invoice"). */
  originalInvoiceId?: string | null; originalInvoiceNumber?: string | null;
}

/** Tax label per country, same table as the print layout; anything else is plain "Tax". */
const TAX_NAME: Record<string, string> = { AE: 'VAT', SA: 'VAT', GB: 'VAT', IN: 'GST', PK: 'Sales tax', US: 'Sales tax' };
export function taxNameFor(country?: string | null): string { return TAX_NAME[(country ?? '').toUpperCase()] ?? 'Tax'; }
export function taxRegLabelFor(country?: string | null): string {
  const c = (country ?? '').toUpperCase();
  return c === 'AE' || c === 'SA' ? 'TRN' : c === 'IN' ? 'GSTIN' : c === 'PK' ? 'NTN' : c === 'GB' ? 'VAT no.' : 'Tax reg. no.';
}

export const INVOICE_STATUS_COLORS: Record<string, string> = { draft: 'default', pendingApproval: 'orange', issued: 'blue', partlyPaid: 'gold', paid: 'green', voided: 'red' };
const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid', pendingApproval: 'Pending approval' };
export function invoiceStatusLabel(s: string): string { return STATUS_LABELS[s] ?? (s ? s.charAt(0).toUpperCase() + s.slice(1) : ''); }
export function humanizeSource(v?: string | null): string {
  if (!v || v.toLowerCase() === 'manual') return '';
  const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ');
  return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}
export function formatMoney(v: number | null | undefined, currency?: string | null): string {
  const n = (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${currency} ${n}` : n;
}
/** Whole days past the due date for an open invoice; 0 when not overdue. */
export function daysOverdue(inv: { dueDate: string; status: string; total: number; paid: number }): number {
  if (!(inv.status === 'issued' || inv.status === 'partlyPaid') || inv.total - inv.paid <= 0) return 0;
  const due = new Date(inv.dueDate + 'T00:00:00');
  const today = new Date(); today.setHours(0, 0, 0, 0);
  const diff = Math.floor((today.getTime() - due.getTime()) / 86400000);
  return diff > 0 ? diff : 0;
}

@Injectable({ providedIn: 'root' })
export class InvoiceExtrasService {
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);
  /** Never errors: a failure just means the sheet shows the party name without the extra details. */
  load(id: string): Observable<InvoiceExtras | null> {
    return this.http.get<InvoiceExtras>(`${this.apiBase}/print/invoice/${id}`).pipe(map((r) => r ?? null), catchError(() => of(null)));
  }
}

/** Read-only, document-style view of an invoice: header, issuer and Bill To, meta grid, lines with taxable amount / tax % / tax, totals and notes. */
@Component({
  selector: 'erp-invoice-sheet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, NzTagModule],
  styles: [`
    :host { display:block; --gold:#b89b5e; --muted:#8c8c8c; }
    .sheet { background:#fff; border:1px solid #f0f0f0; border-top:4px solid var(--gold); border-radius:6px; padding:20px 24px; }
    .head { display:flex; justify-content:space-between; align-items:flex-start; gap:16px; flex-wrap:wrap; }
    .doc-type { font-size:12px; letter-spacing:.12em; text-transform:uppercase; color:var(--gold); font-weight:600; }
    .doc-no { font-size:22px; font-weight:600; margin:2px 0 4px; }
    .issuer { text-align:right; font-size:12px; color:var(--muted); white-space:pre-line; max-width:320px; }
    .issuer b { color:#262626; font-size:14px; }
    .grid2 { display:grid; grid-template-columns: 1fr 1fr; gap:16px; margin-top:16px; }
    .compact .grid2 { grid-template-columns: 1fr; }
    .block { border:1px solid #f0f0f0; border-radius:6px; padding:12px 14px; }
    .block h5 { margin:0 0 6px; font-size:11px; letter-spacing:.1em; text-transform:uppercase; color:var(--gold); font-weight:600; }
    .party-name { font-weight:600; font-size:15px; }
    .muted { color:var(--muted); white-space:pre-line; }
    .meta { display:grid; grid-template-columns: max-content 1fr; gap:4px 16px; font-size:13px; }
    .meta span:nth-child(odd) { color:var(--muted); }
    .overdue { color:#cf1322; font-weight:600; }
    .lines-wrap { width:100%; overflow-x:auto; } table.lines td.r, table.lines th.r { white-space:nowrap; } table.lines td { word-break:break-word; }
    table.lines { width:100%; min-width:640px; border-collapse:collapse; margin-top:16px; font-size:13px; }
    table.lines th { background:#faf7f0; color:#595959; font-weight:600; text-align:left; padding:8px; border-bottom:2px solid var(--gold); white-space:nowrap; }
    table.lines td { padding:8px; border-bottom:1px solid #f0f0f0; vertical-align:top; }
    .r { text-align:right !important; white-space:nowrap; }
    .totals { margin-left:auto; margin-top:12px; width:320px; max-width:100%; font-size:13px; }
    .totals div { display:flex; justify-content:space-between; padding:4px 0; }
    .totals .grand { border-top:2px solid var(--gold); margin-top:4px; padding-top:8px; font-size:15px; font-weight:600; }
    .totals .due { background:#faf7f0; padding:6px 8px; margin-top:4px; border-radius:4px; font-weight:600; }
    .notes { margin-top:16px; font-size:13px; }
  `],
  template: `
    @if (invoice(); as inv) {
      <div class="sheet" [class.compact]="compact()" data-testid="invoice-sheet">
        <div class="head">
          <div>
            <div class="doc-type">{{ docType() }}</div>
            <div class="doc-no" data-testid="sheet-number">{{ inv.number || 'Draft' }}</div>
            <nz-tag [nzColor]="statusColor()" data-testid="sheet-status">{{ statusLabel() }}</nz-tag>
            @if (overdueDays() > 0) { <nz-tag nzColor="red">Overdue {{ overdueDays() }}d</nz-tag> }
          </div>
          @if (extras()?.header; as h) {
            <div class="issuer">
              <b>{{ h.legalEntityName || h.organizationName }}</b>
              @if (h.legalEntityAddress) { <div>{{ h.legalEntityAddress }}</div> }
              @if (h.taxRegistrationNo) { <div>{{ taxReg() }}: {{ h.taxRegistrationNo }}</div> }
              @if (h.phone || h.email) { <div>{{ [h.phone, h.email].filter(truthy).join(' · ') }}</div> }
            </div>
          }
        </div>

        <div class="grid2">
          <div class="block" data-testid="sheet-bill-to">
            <h5>Bill to</h5>
            <div class="party-name">
              @if (partyLink()) { <a [routerLink]="['/parties', inv.partyId]">{{ partyName() }}</a> } @else { {{ partyName() }} }
            </div>
            @if (extras()?.partyAddress) { <div class="muted">{{ extras()!.partyAddress }}</div> }
            @if (extras()?.partyPhone || extras()?.partyEmail) { <div class="muted">{{ [extras()!.partyPhone, extras()!.partyEmail].filter(truthy).join(' · ') }}</div> }
            @if (extras()?.partyTaxNumber) { <div>{{ taxReg() }}: <b>{{ extras()!.partyTaxNumber }}</b></div> }
          </div>
          <div class="block" data-testid="sheet-meta">
            <h5>Details</h5>
            <div class="meta">
              <span>{{ inv.isCreditNote ? 'Credit note date' : 'Invoice date' }}</span><span>{{ inv.invoiceDate | date: 'dd MMM yyyy' }}</span>
              <span>Due date</span><span [class.overdue]="overdueDays() > 0">{{ inv.dueDate | date: 'dd MMM yyyy' }}@if (overdueDays() > 0) { ({{ overdueDays() }} days overdue) }</span>
              <span>Currency</span><span>{{ inv.currencyCode }}</span>
              @if (source()) { <span>Source</span><span>{{ source() }}</span> }
              @if (originalInvoiceId(); as oid) { <span>Credits</span><span><a [routerLink]="['/finance/invoices', oid]">{{ extras()?.originalInvoiceNumber ? 'Invoice ' + extras()!.originalInvoiceNumber : 'Original invoice' }}</a></span> }
            </div>
          </div>
        </div>

        <div class="lines-wrap"><table class="lines" data-testid="sheet-lines">
          <thead><tr>
            <th style="width:32px">#</th><th>Description</th><th class="r">Qty</th><th class="r">Price</th>
            <th class="r">Taxable</th><th class="r">{{ taxName() }} %</th><th class="r">{{ taxName() }}</th><th class="r">Amount</th>
          </tr></thead>
          <tbody>
            @for (l of rows(); track $index) {
              <tr>
                <td>{{ l.seq }}</td><td>{{ l.description }}</td><td class="r">{{ l.quantity }}</td><td class="r">{{ money(l.price) }}</td>
                <td class="r">{{ money(l.net) }}</td><td class="r">{{ l.rate === null ? '—' : l.rate + '%' }}</td><td class="r">{{ money(l.tax) }}</td><td class="r">{{ money(l.net + l.tax) }}</td>
              </tr>
            } @empty { <tr><td colspan="8" class="muted">No lines</td></tr> }
          </tbody>
        </table></div>

        <div class="totals" data-testid="sheet-totals">
          <div><span>Subtotal (taxable)</span><span>{{ money(inv.net) }}</span></div>
          <div><span>{{ taxName() }}</span><span>{{ money(inv.tax) }}</span></div>
          <div class="grand"><span>Total {{ inv.currencyCode }}</span><span>{{ money(inv.total) }}</span></div>
          <div><span>{{ inv.isCreditNote ? 'Applied' : 'Paid' }}</span><span>{{ money(inv.paid) }}</span></div>
          <div class="due"><span>{{ inv.isCreditNote ? 'Unapplied' : 'Balance due' }}</span><span>{{ money(inv.total - inv.paid) }}</span></div>
        </div>

        @if (inv.notes) { <div class="notes"><h5 style="margin:0 0 4px;font-size:11px;letter-spacing:.1em;text-transform:uppercase;color:#b89b5e">Notes</h5><div class="muted">{{ inv.notes }}</div></div> }
      </div>
    }
  `,
})
export class InvoiceSheetComponent {
  readonly invoice = input<InvoiceSheetData | null>(null);
  readonly extras = input<InvoiceExtras | null>(null);
  /** tax code id → rate (%) to show the rate per line */
  readonly taxRates = input<Record<string, number>>({});
  readonly compact = input(false);
  readonly partyLink = input(true);
  readonly truthy = (x: unknown) => !!x;

  readonly country = computed(() => this.extras()?.header.countryCode ?? null);
  readonly taxName = computed(() => taxNameFor(this.country()));
  readonly taxReg = computed(() => taxRegLabelFor(this.country()));
  readonly docType = computed(() => {
    const inv = this.invoice();
    if (!inv) return '';
    return inv.isCreditNote ? 'Credit note' : this.extras()?.header.taxRegistrationNo ? 'Tax invoice' : 'Invoice';
  });
  readonly statusColor = computed(() => INVOICE_STATUS_COLORS[this.invoice()?.status ?? ''] ?? 'default');
  readonly statusLabel = computed(() => invoiceStatusLabel(this.invoice()?.status ?? ''));
  readonly overdueDays = computed(() => { const inv = this.invoice(); return inv ? daysOverdue(inv) : 0; });
  readonly source = computed(() => humanizeSource(this.invoice()?.sourceType));
  /** Original invoice of a credit note: legacy creditNoteForId, the print endpoint's link, or SourceType "invoice" + SourceId. */
  readonly originalInvoiceId = computed(() => {
    const inv = this.invoice();
    if (!inv) return null;
    return inv.creditNoteForId ?? this.extras()?.originalInvoiceId ?? (inv.isCreditNote && inv.sourceType === 'invoice' ? inv.sourceId ?? null : null);
  });
  // B-4: fall back when the stored party name is empty.
  readonly partyName = computed(() => this.invoice()?.partyName || 'Customer');
  readonly rows = computed(() => (this.invoice()?.lines ?? []).map((l, i) => {
    const net = l.net ?? Math.round(l.quantity * l.price * 100) / 100;
    const tax = l.taxAmount ?? 0;
    const known = l.taxCodeId ? this.taxRates()[l.taxCodeId] : undefined;
    const rate = known !== undefined ? known : !l.taxCodeId && !tax ? 0 : net ? Math.round((tax / net) * 10000) / 100 : null;
    return { seq: l.seq ?? i + 1, description: l.description, quantity: l.quantity, price: l.price, net, tax, rate };
  }));

  money(v: number): string { return formatMoney(v); }
}
