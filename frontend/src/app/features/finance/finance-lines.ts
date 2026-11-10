import { AbstractControl, FormBuilder, Validators } from '@angular/forms';
import { TaxCodeDto } from '@core/api/admin.api';

/** The line editors of the invoice, bill and journal drawers: one form row per line, the drawer's totals and the lines as the API takes them. */

/** What an invoice line row starts from (a saved line or nothing). */
export interface InvoiceLineSeed { description?: string; quantity?: number; price?: number; taxCodeId?: string | null; }
/** What a bill line row starts from; a bill line also names the account it is booked to. */
export interface BillLineSeed extends InvoiceLineSeed { accountId?: string; }
/** What a journal line row starts from. */
export interface JournalLineSeed { accountId?: string; debit?: number; credit?: number; description?: string; }

/** An invoice line row: description (required), quantity, price and tax code. */
export function invoiceLineGroup(fb: FormBuilder, l?: InvoiceLineSeed) {
  return fb.group({ description: [l?.description ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
}

/** A bill line row: as an invoice line, plus the expense or asset account (required). */
export function billLineGroup(fb: FormBuilder, l?: BillLineSeed) {
  return fb.group({ description: [l?.description ?? '', Validators.required], accountId: [l?.accountId ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
}

/** A journal line row: account (required), debit, credit and description. */
export function journalLineGroup(fb: FormBuilder, l?: JournalLineSeed) {
  return fb.group({ accountId: [l?.accountId ?? '', Validators.required], debit: [l?.debit ?? 0], credit: [l?.credit ?? 0], description: [l?.description ?? ''] });
}

/** A priced line as the drawer's totals read it (fields can be empty while typing). */
export interface TaxedLine { quantity?: number | null; price?: number | null; taxCodeId?: string | null; }

/** Net and tax of the lines, each line rounded to cents first (as the API rounds them); a line without a tax code has no tax. */
export function taxedLineTotals(lines: TaxedLine[], taxes: TaxCodeDto[]): { net: number; tax: number } {
  const rate = (id?: string | null) => taxes.find((t) => t.id === id)?.rate ?? 0;
  const lineNet = (l: TaxedLine) => Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100;
  const net = lines.reduce((s, l) => s + lineNet(l), 0);
  const tax = lines.reduce((s, l) => s + Math.round(lineNet(l) * rate(l.taxCodeId)) / 100, 0);
  return { net, tax };
}

/** Debit and credit totals of journal lines. */
export function journalTotals(lines: { debit?: number | null; credit?: number | null }[]): { debit: number; credit: number } {
  return { debit: lines.reduce((s, l) => s + (l.debit ?? 0), 0), credit: lines.reduce((s, l) => s + (l.credit ?? 0), 0) };
}

/** Lines with a tax code as the API takes them: an empty tax code is sent as null. */
export function withTaxCodes<T extends { taxCodeId: string | null }>(lines: T[]): T[] {
  return lines.map((l) => ({ ...l, taxCodeId: l.taxCodeId || null }));
}

/** Journal lines as the API takes them: empty amounts are 0, an empty description is null. */
export function journalLinesBody<T extends { debit: number | null; credit: number | null; description: string | null }>(lines: T[]) {
  return lines.map((l) => ({ ...l, debit: l.debit ?? 0, credit: l.credit ?? 0, description: l.description || null }));
}

/** Red state of a line field the user tried to save empty; line fields sit outside nz-form-control, so they need their own. */
export function lineFieldStatus(line: AbstractControl, name: string): 'error' | '' {
  const c = line.get(name);
  return c?.invalid && c.touched ? 'error' : '';
}
