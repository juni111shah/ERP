import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { AccountDto, LegalEntityDto } from '@core/api/admin.api';
import { FilterCriterion, ListQuery } from '@core/api/api.models';
import { displayDate, displayLabel } from '@core/format/display';
import { ActionButton, DescriptionItem, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { formatMoney } from './finance-format';
import { mainBankAccount } from './finance-lookups';

/** Shared logic of receipts (money in) and payments (money out): allocating to open documents, form defaults and the detail page. */

/** The open documents of a party a receipt, payment or cheque can settle, oldest due first. */
export function openDocumentsQuery(partyId: string, filters: FilterCriterion[]): ListQuery {
  return { page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'partyId', op: 'eq', value: partyId }, ...filters] };
}

/** The documents that got an amount, as `[documentId, amount]` pairs. */
export function allocatedAmounts(amounts: Record<string, number>): [string, number][] {
  return Object.entries(amounts).filter(([, a]) => a > 0);
}

/**
 * Arriving from an invoice or bill opens the form before the legal entities and bank accounts have loaded;
 * fills the only legal entity and the main bank (else the first) account once they arrive, unless already chosen.
 */
export function fillLateDefaults(c: { legalEntityId: FormControl<string | null>; bankAccountId: FormControl<string | null> }, entities: LegalEntityDto[], banks: AccountDto[]): void {
  if (entities.length === 1 && !c.legalEntityId.value) c.legalEntityId.setValue(entities[0].id);
  const bank = mainBankAccount(banks) ?? banks[0];
  if (bank && !c.bankAccountId.value) c.bankAccountId.setValue(bank.id);
}

/** A receipt or payment as its detail page reads it. */
export interface SettlementView {
  id: string; number: string; partyId: string; partyName: string; date: string; method: string; currencyCode: string; amount: number; unallocated: number;
  reference: string | null; status: string; journalEntryId: string | null; allocations: { amount: number }[];
}

/** What differs between the receipt and the payment detail page. */
export interface SettlementPageSpec {
  /** list page holding the allocate form, e.g. '/finance/receipts' */
  listRoute: string;
  /** write permission, e.g. 'finance.receipts.write' */
  permission: string;
  /** print layout, e.g. 'receipt' */
  printKind: string;
  /** last KPI card, e.g. 'Invoices Applied' */
  appliedTitle: string;
  /** party label, e.g. 'Customer' */
  partyLabel: string;
}

/** Allocate, Void, View Journal, Print and Email. */
export function settlementActions(doc: SettlementView, spec: SettlementPageSpec, router: Router, onVoid: () => void, onEmail: () => void): ActionButton[] {
  return [
    { label: 'Allocate', icon: 'link', type: 'primary', permission: spec.permission, visible: doc.status === 'issued' && doc.unallocated > 0, handler: () => router.navigate([spec.listRoute], { queryParams: { open: doc.id } }) },
    { label: 'Void', icon: 'stop', danger: true, permission: spec.permission, visible: doc.status === 'issued', handler: onVoid },
    { label: 'View Journal', icon: 'book', permission: 'finance.journals.read', visible: !!doc.journalEntryId, handler: () => router.navigate(['/finance/journals', doc.journalEntryId]) },
    { label: 'Print', icon: 'printer', handler: () => window.open(`/print/${spec.printKind}/${doc.id}`, '_blank') },
    { label: 'Email', icon: 'mail', permission: 'core.email.send', visible: doc.status === 'issued', handler: onEmail },
  ];
}

/** Total, allocated, unallocated and how many documents it settles. */
export function settlementKpis(doc: SettlementView, spec: SettlementPageSpec): KpiCard[] {
  return [
    { title: 'Total Amount', value: formatMoney(doc.amount), suffix: doc.currencyCode },
    { title: 'Allocated', value: formatMoney(doc.allocations.reduce((s, a) => s + a.amount, 0)), suffix: doc.currencyCode },
    { title: 'Unallocated', value: formatMoney(doc.unallocated), suffix: doc.currencyCode, valueStyle: { color: doc.unallocated > 0 ? '#fa8c16' : '#52c41a' } },
    { title: spec.appliedTitle, value: doc.allocations.length },
  ];
}

/** Party, date, method, currency and reference; the party links to its page only when the user may read parties. */
export function settlementDescriptions(doc: SettlementView, spec: SettlementPageSpec, partyLink = true): DescriptionItem[] {
  return [
    { label: spec.partyLabel, value: doc.partyName, routerLink: partyLink ? ['/parties', doc.partyId] : undefined },
    { label: 'Date', value: displayDate(doc.date) },
    { label: 'Payment Method', value: displayLabel(doc.method) },
    { label: 'Currency', value: doc.currencyCode },
    { label: 'Reference', value: doc.reference, span: 2 },
  ];
}

/** "R-0001, 10 Nov 2026, Bank transfer": the facts of the success message shown right after recording. */
export function settlementFacts(doc: SettlementView): string {
  return `${doc.number}, ${displayDate(doc.date)}, ${displayLabel(doc.method)}`;
}
