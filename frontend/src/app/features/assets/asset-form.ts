import { Observable, catchError, map, of } from 'rxjs';
import { AccountsApi, BillsApi, BusinessUnitsApi } from '@core/api/admin.api';
import { AssetCategoryDto, CAPITALISATION_SOURCES } from '@core/api/assets.api';
import { PartiesApi } from '@core/api/parties.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { FieldSpec, SelectOption } from '@shared/components/record-drawer/record-drawer.component';

/** The drawer forms of the asset pages: the asset itself, capitalising it and disposing of it. */

/** Business units matching a typed term, as picker options (code and name). */
export function businessUnitLookup(api: BusinessUnitsApi): (term: string) => Observable<LookupOption[]> {
  return (term) => api.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((b) => ({ value: b.id, label: `${b.code} · ${b.name}` }))));
}

/** The form of an asset in the register. Useful life is optional: the category's applies when it is left empty. */
export function assetFields(entities: SelectOption[], defaultEntity: string | null, categories: AssetCategoryDto[], units: (term: string) => Observable<LookupOption[]>): FieldSpec[] {
  return [
    { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: entities, default: defaultEntity ?? entities[0]?.value ?? null, readonlyOnEdit: true },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'categoryId', label: 'Category', type: 'select', required: true, searchable: true, options: categories.filter((c) => c.isActive).map((c) => ({ value: c.id, label: `${c.code} · ${c.name}` })) },
    { key: 'acquisitionDate', label: 'Acquisition date', type: 'date', required: true },
    { key: 'cost', label: 'Cost', type: 'number', required: true, min: 0.01, default: 0 },
    { key: 'salvageValue', label: 'Salvage value', type: 'number', min: 0, default: 0, hint: 'What the asset is expected to be worth at the end of its life.' },
    { key: 'lifeMonths', label: 'Useful life (months)', type: 'number', min: 1, max: 1200, precision: 0, hint: 'Leave empty to use the life of the category.' },
    { key: 'businessUnitId', label: 'Business unit', type: 'lookup', lookup: units, currentLabel: (r) => String(r['businessUnitId']) },
    { key: 'custodian', label: 'Custodian', type: 'text', maxLength: 200, hint: 'The person or team that looks after the asset.' },
    { key: 'description', label: 'Description', type: 'textarea', maxLength: 1000 },
  ];
}

/** The APIs the capitalise form looks things up in. */
export interface CapitaliseLookups {
  accounts: AccountsApi;
  parties: PartiesApi;
  bills: BillsApi;
}

/**
 * The capitalise form: through a clearing or bank account, a new vendor bill or an existing bill (and one of its lines).
 * The bill picked is remembered by this form, so its lines are what the line picker offers.
 */
export function capitaliseFields({ accounts, parties, bills }: CapitaliseLookups): FieldSpec[] {
  let pickedBill: string | null = null;
  return [
    { key: 'source', label: 'Paid or recorded through', type: 'select', required: true, options: CAPITALISATION_SOURCES, default: 'clearing' },
    { key: 'date', label: 'Date', type: 'date', hint: 'Left empty, the acquisition date is used.' },
    { key: 'accountId', label: 'Account', type: 'lookup', lookup: accounts.lookup, hint: 'The clearing account, or the bank or cash account the asset was paid from.', hidden: (v) => v['source'] !== 'clearing' && v['source'] !== 'bank' },
    { key: 'partyId', label: 'Vendor', type: 'lookup', lookup: (term) => parties.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Vendor' } }).pipe(map((r) => r.items.map((p) => ({ value: p.id, label: p.name })))), hidden: (v) => v['source'] !== 'vendor', hint: 'A posted bill is raised for this vendor, so it is paid like any other bill.' },
    {
      key: 'billId', label: 'Bill', type: 'lookup', hidden: (v) => v['source'] !== 'bill',
      lookup: (term) => bills.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((b) => ({ value: b.id, label: `${b.number} · ${b.partyName ?? ''}` })))),
      onChange: (value) => { pickedBill = (value as string | null) ?? null; return null; },
    },
    {
      key: 'billLineId', label: 'Bill line', type: 'lookup', hidden: (v) => v['source'] !== 'bill', hint: 'Only needed when the bill has several lines.',
      lookup: () => (pickedBill ? bills.detail(pickedBill).pipe(map((b) => b.lines.filter((l) => !!l.id).map((l) => ({ value: l.id!, label: `${l.description} · ${l.quantity * l.price}` }))), catchError(() => of([]))) : of([])),
    },
    { key: 'reference', label: 'Reference', type: 'text', maxLength: 100 },
  ];
}

/** The dispose form: when, for how much, where the proceeds went and which gain or loss account takes the difference. */
export function disposeFields(accounts: AccountsApi): FieldSpec[] {
  return [
    { key: 'date', label: 'Disposal date', type: 'date', required: true },
    { key: 'proceeds', label: 'Proceeds', type: 'number', min: 0, default: 0, hint: 'What was received. Zero for scrapping.' },
    { key: 'proceedsAccountId', label: 'Proceeds received into', type: 'lookup', lookup: accounts.lookup, hint: 'Required when proceeds are above zero.', hidden: (v) => !(Number(v['proceeds']) > 0) },
    { key: 'gainLossAccountId', label: 'Gain or loss account', type: 'lookup', lookup: accounts.lookup, hint: 'Left empty, the account of the category is used.' },
    { key: 'reason', label: 'Reason', type: 'textarea', maxLength: 400 },
  ];
}
