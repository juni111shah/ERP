import { Observable } from 'rxjs';
import { IndustryRecordDto, RecordFieldDef, RecordInput, RecordLineInput, RecordTypeView } from '../../core/api/industry.api';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { displayMoney } from '../../core/format/display';
import { FieldSpec, RowColumn, SelectOption } from '../../shared/record-drawer.component';

/** What the form needs from outside the definition: who can be picked, and which lists the workspace keeps. */
export interface RecordFormContext {
  legalEntities: SelectOption[];
  defaultLegalEntityId?: string | null;
  businessUnits: SelectOption[];
  items: SelectOption[];
  units: string[];
  currency: string;
  partyLookup: (term: string) => Observable<LookupOption[]>;
  parentLookup: (term: string) => Observable<LookupOption[]>;
}

/** Form control name of a field the definition declares; the record keeps these under `data`. */
export const dataKey = (key: string): string => 'd_' + key;

const today = (): Date => { const n = new Date(); return new Date(n.getFullYear(), n.getMonth(), n.getDate()); };

/** One definition field as a drawer field: its type decides the control. */
export function fieldSpecFor(def: RecordFieldDef): FieldSpec {
  const base = { key: dataKey(def.key), label: def.label, required: def.required, hint: def.help ?? undefined } as const;
  switch (def.type) {
    case 'textarea': return { ...base, type: 'textarea', maxLength: 2000 };
    case 'number': return { ...base, type: 'number', precision: 4 };
    case 'money': return { ...base, type: 'number', min: 0, precision: 2 };
    case 'date': return { ...base, type: 'date' };
    case 'bool': return { ...base, type: 'switch', required: false, default: false };
    case 'select': return { ...base, type: 'select', options: (def.options ?? []).map((o) => ({ value: o, label: o })) };
    default: return { ...base, type: 'text', maxLength: 2000 };
  }
}

/** The line total shown under the grid: quantity times unit price, summed. */
export function linesTotal(rows: Record<string, unknown>[]): number {
  return Math.round(rows.reduce((sum, r) => sum + (Number(r['quantity']) || 0) * (Number(r['unitPrice']) || 0), 0) * 100) / 100;
}

function lineColumns(type: RecordTypeView, ctx: RecordFormContext): RowColumn[] {
  const columns: RowColumn[] = [
    { key: 'description', label: 'Description', type: 'text' },
    { key: 'code', label: 'Code', type: 'text', width: '80px' },
    { key: 'itemId', label: 'Item', type: 'select', options: ctx.items, width: '120px' },
    ctx.units.length > 0
      ? { key: 'unitOfMeasure', label: 'Unit', type: 'select', options: ctx.units.map((u) => ({ value: u, label: u })), width: '80px' }
      : { key: 'unitOfMeasure', label: 'Unit', type: 'text', width: '70px' },
    { key: 'quantity', label: 'Quantity', type: 'number', width: '80px' },
    { key: 'unitPrice', label: 'Unit price', type: 'number', width: '90px' },
    { key: 'dueDate', label: 'Due date', type: 'date', width: '140px' },
  ];
  if (type.tracksProgress) columns.push({ key: 'completedQuantity', label: 'Completed', type: 'number', width: '90px' });
  return columns;
}

/**
 * The form of any record type, built only from its definition: the name, the party and parent pickers when the type has them, its own
 * fields by type, the legal entity and business unit, the dates, the amount and the line grid.
 */
export function recordFieldSpecs(type: RecordTypeView, ctx: RecordFormContext): FieldSpec[] {
  const fields: FieldSpec[] = [{ key: 'title', label: type.titleLabel || 'Name', type: 'text', required: true, maxLength: 300 }];
  if (type.partyRole) {
    fields.push({ key: 'partyId', label: type.partyLabel || 'Party', type: 'lookup', lookup: ctx.partyLookup, currentLabel: (r) => String(r['partyName'] ?? r['partyId']) });
  }
  if (type.parentType) {
    fields.push({ key: 'parentId', label: type.parentLabel || 'Belongs to', type: 'lookup', lookup: ctx.parentLookup, currentLabel: (r) => String(r['parentTitle'] ?? r['parentId']) });
  }
  fields.push(...type.fields.map(fieldSpecFor));
  fields.push({ key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: ctx.legalEntities, default: ctx.defaultLegalEntityId ?? ctx.legalEntities[0]?.value ?? null, readonlyOnEdit: true });
  if (ctx.businessUnits.length > 0) fields.push({ key: 'businessUnitId', label: 'Business unit', type: 'select', options: ctx.businessUnits });
  fields.push({ key: 'recordDate', label: 'Date', type: 'date', required: true, default: today() });
  if (type.hasDueDate) fields.push({ key: 'dueDate', label: 'Due date', type: 'date' });
  if (type.hasAmount) {
    fields.push({ key: 'amount', label: 'Amount', type: 'number', min: 0, precision: 2, hint: type.hasLines ? 'Used when there are no lines; otherwise the lines decide the total.' : undefined });
  }
  fields.push({ key: 'notes', label: 'Notes', type: 'textarea', maxLength: 4000 });
  if (type.hasLines) {
    fields.push({
      key: 'lines', label: type.linesLabel || 'Lines', type: 'rows', columns: lineColumns(type, ctx), rowDefaults: { quantity: 1 }, default: [],
      summary: (rows) => `Total ${displayMoney(linesTotal(rows), ctx.currency)}`,
    });
  }
  return fields;
}

const blank = (v: unknown): boolean => v === null || v === undefined || v === '';

/** The drawer's values as the API body. Empty fields are left out, and the declared fields go under `data`. */
export function toRecordInput(payload: Record<string, unknown>, type: RecordTypeView): RecordInput {
  const data: Record<string, unknown> = {};
  for (const f of type.fields) {
    const v = payload[dataKey(f.key)];
    if (!blank(v)) data[f.key] = v;
  }
  const input: RecordInput = {
    legalEntityId: String(payload['legalEntityId'] ?? ''), title: String(payload['title'] ?? ''), recordDate: String(payload['recordDate'] ?? ''),
    data, partyId: (payload['partyId'] as string | null) ?? null, parentId: (payload['parentId'] as string | null) ?? null,
    businessUnitId: (payload['businessUnitId'] as string | null) ?? null, dueDate: type.hasDueDate ? ((payload['dueDate'] as string | null) ?? null) : null,
    amount: type.hasAmount && !blank(payload['amount']) ? Number(payload['amount']) : null, notes: (payload['notes'] as string | null) ?? null,
  };
  if (type.hasLines) {
    const rows = Array.isArray(payload['lines']) ? (payload['lines'] as Record<string, unknown>[]) : [];
    input.lines = rows.map((r): RecordLineInput => ({
      description: String(r['description'] ?? ''), quantity: Number(r['quantity']) || 0, unitPrice: Number(r['unitPrice']) || 0,
      code: (r['code'] as string | undefined) ?? null, itemId: (r['itemId'] as string | undefined) ?? null, unitOfMeasure: (r['unitOfMeasure'] as string | undefined) ?? null,
      dueDate: (r['dueDate'] as string | undefined) ?? null, completedQuantity: type.tracksProgress && !blank(r['completedQuantity']) ? Number(r['completedQuantity']) : null,
    }));
  }
  return input;
}

/** A saved record as the drawer's values, for editing. */
export function toFormValue(record: IndustryRecordDto, type: RecordTypeView): Record<string, unknown> {
  const value: Record<string, unknown> = {
    id: record.id, title: record.title, legalEntityId: record.legalEntityId, businessUnitId: record.businessUnitId, partyId: record.partyId, partyName: record.partyName,
    parentId: record.parentId, parentTitle: record.parentTitle, recordDate: record.recordDate, dueDate: record.dueDate, amount: record.amount, notes: record.notes,
  };
  for (const f of type.fields) value[dataKey(f.key)] = record.data?.[f.key] ?? null;
  value['lines'] = (record.lines ?? []).map((l) => ({
    description: l.description, code: l.code ?? undefined, itemId: l.itemId ?? undefined, unitOfMeasure: l.unitOfMeasure ?? undefined, quantity: l.quantity,
    unitPrice: l.unitPrice, dueDate: l.dueDate ?? undefined, completedQuantity: l.completedQuantity,
  }));
  return value;
}
