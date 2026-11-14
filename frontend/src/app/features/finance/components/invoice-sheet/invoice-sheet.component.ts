import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { InvoiceExtras } from '@core/api/finance-documents.api';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { displayNote } from '@core/i18n/translate-now';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { CanPipe } from '@shared/pipes/can.pipe';
import { daysOverdue, formatMoney, humanizeSource, invoiceStatusLabel, taxNameFor, taxRegLabelFor } from '../../finance-format';

/** The invoice fields the sheet renders (a subset of InvoiceDto, so the list drawer and the detail page can share it). */
export interface InvoiceSheetData {
  id: string; number: string; isCreditNote: boolean; partyId: string; partyName?: string | null; invoiceDate: string; dueDate: string; currencyCode: string;
  net: number; tax: number; total: number; paid: number; status: string; sourceType?: string | null; sourceId?: string | null; creditNoteForId?: string | null; notes?: string | null;
  lines: { id?: string; seq?: number; description: string; quantity: number; price: number; taxCodeId?: string | null; taxAmount?: number; net?: number }[];
}

/** One printed line: taxable amount, tax and the rate (known from the tax code, else derived from the amounts). */
interface SheetRow { seq: number; description: string; quantity: number; price: number; net: number; tax: number; rate: number | null; }

/** Read-only, document-style view of an invoice: header, issuer and Bill To, meta grid, lines with taxable amount / tax % / tax, totals and notes. */
@Component({
  selector: 'erp-invoice-sheet',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, ErpDatePipe, CanPipe, StatusTagComponent, RouterLink, NzTagModule],
  styleUrl: './invoice-sheet.component.scss',
  templateUrl: './invoice-sheet.component.html',
})
export class InvoiceSheetComponent {
  private readonly i18n = inject(I18nService);
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
  readonly rows = computed(() => (this.invoice()?.lines ?? []).map((l, i) => sheetRow(l, i, this.taxRates())));

  /** Notes as typed; the default credit-note note (stored in English) is shown in the active language. */
  noteText(note: string | null | undefined): string { return displayNote(note, this.i18n.lang()); }

  money(v: number): string { return formatMoney(v); }
}

function sheetRow(l: InvoiceSheetData['lines'][number], index: number, taxRates: Record<string, number>): SheetRow {
  // `||`: a stored line net of 0 on a priced line would print an amount that contradicts the document's own subtotal.
  const net = l.net || Math.round(l.quantity * l.price * 100) / 100;
  const tax = l.taxAmount ?? 0;
  const known = l.taxCodeId ? taxRates[l.taxCodeId] : undefined;
  const rate = known !== undefined ? known : !l.taxCodeId && !tax ? 0 : net ? Math.round((tax / net) * 10000) / 100 : null;
  return { seq: l.seq ?? index + 1, description: l.description, quantity: l.quantity, price: l.price, net, tax, rate };
}
