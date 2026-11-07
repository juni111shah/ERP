import { Observable, map } from 'rxjs';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { BusinessUnitsApi } from '../../core/api/admin.api';
import { AssetCategoryDto } from '../../core/api/assets.api';
import { SelectOption, FieldSpec } from '../../shared/record-drawer.component';

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
