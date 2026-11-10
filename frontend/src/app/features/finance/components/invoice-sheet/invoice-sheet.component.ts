import { ChangeDetectionStrategy, Component, Injectable, computed, inject, input } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from '@core/api/api-config';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { displayNote } from '@core/i18n/translate-now';

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

const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid', pendingApproval: 'Pending approval' };
export function invoiceStatusLabel(s: string): string { return STATUS_LABELS[s] ?? (s ? s.charAt(0).toUpperCase() + s.slice(1) : ''); }
/** Friendly names for module source keys ("<module>.<kind>"); anything else is humanized below. */
const SOURCE_LABELS: Record<string, string> = { 'realestate.instalment': 'Rent instalment' };
export function humanizeSource(v?: string | null): string {
  if (!v || v.toLowerCase() === 'manual') return '';
  if (SOURCE_LABELS[v.toLowerCase()]) return SOURCE_LABELS[v.toLowerCase()];
  const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[._-]+/g, ' ');
  return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}
export function formatMoney(v: number | null | undefined, currency?: string | null): string {
  const n = (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${currency} ${n}` : n;
}
/** Whole days past the due date for an open invoice or bill; 0 when not overdue. A credit note is never overdue: it is money owed to the customer. */
export function daysOverdue(inv: { dueDate: string; status: string; total: number; paid: number; isCreditNote?: boolean }): number {
  if (inv.isCreditNote || !(inv.status === 'issued' || inv.status === 'partlyPaid') || inv.total - inv.paid <= 0) return 0;
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
  imports: [TranslatePipe, ErpDatePipe, StatusTagComponent, RouterLink, NzTagModule],
  styleUrl: './invoice-sheet.component.scss',
  templateUrl: './invoice-sheet.component.html',
})
export class InvoiceSheetComponent {
  readonly invoice = input<InvoiceSheetData | null>(null);
  readonly extras = input<InvoiceExtras | null>(null);
  /** tax code id → rate (%) to show the rate per line */
  readonly taxRates = input<Record<string, number>>({});
  readonly compact = input(false);
  readonly partyLink = input(true);
  readonly truthy = (x: unknown) => !!x;
  private readonly i18n = inject(I18nService);
  /** Notes as typed; the default credit-note note (stored in English) is shown in the active language. */
  noteText(note: string | null | undefined): string { return displayNote(note, this.i18n.lang()); }

  readonly country = computed(() => this.extras()?.header.countryCode ?? null);
  readonly taxName = computed(() => taxNameFor(this.country()));
  readonly taxReg = computed(() => taxRegLabelFor(this.country()));
  readonly docType = computed(() => {
    const inv = this.invoice();
    if (!inv) return '';
    return inv.isCreditNote ? 'Credit note' : this.extras()?.header.taxRegistrationNo ? 'Tax invoice' : 'Invoice';
  });
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
    // `||`: a stored line net of 0 on a priced line would print an amount that contradicts the document's own subtotal.
    const net = l.net || Math.round(l.quantity * l.price * 100) / 100;
    const tax = l.taxAmount ?? 0;
    const known = l.taxCodeId ? this.taxRates()[l.taxCodeId] : undefined;
    const rate = known !== undefined ? known : !l.taxCodeId && !tax ? 0 : net ? Math.round((tax / net) * 10000) / 100 : null;
    return { seq: l.seq ?? i + 1, description: l.description, quantity: l.quantity, price: l.price, net, tax, rate };
  }));

  money(v: number): string { return formatMoney(v); }
}
