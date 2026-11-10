import { BomComponentDto, MaterialLineInput, WorkOrderDto } from '@core/api/manufacturing.api';
import { DescriptionItem, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { FieldSpec, SelectOption } from '@shared/components/record-drawer/record-drawer.component';

/** The values a record-drawer form submits. */
export type DrawerPayload = Record<string, unknown>;

const money = (v: number) => new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);

/** The BOM's components as item options ("code · name"). */
export function componentItemOptions(components: BomComponentDto[]): SelectOption[] {
  return components.map((c) => ({ value: c.componentItemId, label: `${c.componentItemCode} · ${c.componentItemName}` }));
}

/** The items issued to the order so far, each once, as options. */
export function issuedItemOptions(order: WorkOrderDto | null): SelectOption[] {
  const seen = new Map<string, string>();
  for (const m of order?.materialIssues ?? []) seen.set(m.itemId, `${m.itemCode} · ${m.itemName}`);
  return [...seen].map(([value, label]) => ({ value, label }));
}

/** The issue-material form. */
export function issueFieldSpecs(itemOptions: SelectOption[]): FieldSpec[] {
  return [
    { key: 'lines', label: 'Material', type: 'rows', columns: [{ key: 'itemId', label: 'Item', type: 'select', options: itemOptions }, { key: 'quantity', label: 'Quantity', type: 'number', width: '110px' }], rowDefaults: { itemId: '', quantity: 1 }, hint: 'Leave empty to issue what the bill of materials still needs for the planned quantity.' },
    { key: 'date', label: 'Date', type: 'date', hint: 'Left empty, today. It cannot be in the future.' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ];
}

/** The return-material form. */
export function returnFieldSpecs(issuedOptions: SelectOption[]): FieldSpec[] {
  return [
    { key: 'lines', label: 'Material to return', type: 'rows', required: true, columns: [{ key: 'itemId', label: 'Item', type: 'select', options: issuedOptions }, { key: 'quantity', label: 'Quantity', type: 'number', width: '110px' }], rowDefaults: { itemId: '', quantity: 1 }, hint: 'Material goes back to stock at the cost it was issued at, and no more than is on the order can be returned.' },
    { key: 'date', label: 'Date', type: 'date' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ];
}

/** The complete-work-order form, proposing the planned quantity. */
export function completeFieldSpecs(order: WorkOrderDto | null): FieldSpec[] {
  return [
    { key: 'completedQuantity', label: 'Quantity produced', type: 'number', required: true, min: 0, default: order?.plannedQuantity ?? 0 },
    { key: 'scrappedQuantity', label: 'Quantity scrapped', type: 'number', min: 0, default: 0 },
    { key: 'completionDate', label: 'Completion date', type: 'date', hint: 'The day the goods came out of production. Left empty, today. It cannot be in the future.' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500 },
  ];
}

/** The KPI cards: produced so far and the cost split. */
export function workOrderKpis(o: WorkOrderDto, currency: string): KpiCard[] {
  return [
    { title: 'Produced', value: `${o.completedQuantity} of ${o.plannedQuantity}`, suffix: o.uom },
    { title: 'Material cost', value: money(o.materialCost), suffix: currency },
    { title: 'Conversion cost', value: money(o.conversionCost), suffix: currency },
    { title: 'Scrap cost', value: money(o.scrapCost), suffix: currency },
    { title: 'Estimated cost', value: money(o.estimatedTotalCost), suffix: currency },
  ];
}

/** The description list of the order. */
export function workOrderDescriptions(o: WorkOrderDto): DescriptionItem[] {
  return [
    { label: 'Bill of materials', value: o.bomCode }, { label: 'Start', value: o.startDate }, { label: 'Due', value: o.dueDate },
    { label: 'Completed on', value: o.completionDate ?? null }, { label: 'Scrapped', value: o.scrappedQuantity }, { label: 'Notes', value: o.notes ?? null, span: 2 },
  ];
}

/** The rows of a material form as lines; undefined when none were entered. */
export function materialLinesOf(p: DrawerPayload): MaterialLineInput[] | undefined {
  const rows = ((p['lines'] as Record<string, unknown>[] | null) ?? []).filter((r) => r['itemId'] && Number(r['quantity']) > 0);
  return rows.length ? rows.map((r) => ({ itemId: String(r['itemId']), quantity: Number(r['quantity']) })) : undefined;
}

/** The form's date (or completion date); null when empty, which the server reads as today. */
export function formDateOf(p: DrawerPayload): string | null {
  return (p['date'] as string) || (p['completionDate'] as string) || null;
}

/** The form's notes; null when empty. */
export function formNotesOf(p: DrawerPayload): string | null {
  return (p['notes'] as string) || null;
}
