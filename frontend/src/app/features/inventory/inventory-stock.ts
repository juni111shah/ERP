import { InventoryLookups, StockAction } from '@core/api/inventory.api';
import { toIsoDate } from '@core/format/dates';
import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';

/** The stock screen's actions: the four movements plus setting a reorder level. */
export type StockFormAction = StockAction | 'policy';

/** The action buttons, in the order they are shown. */
export const STOCK_ACTIONS: { key: StockFormAction; label: string }[] = [
  { key: 'receive', label: 'Receive stock' },
  { key: 'issue', label: 'Issue stock' },
  { key: 'adjust', label: 'Adjust' },
  { key: 'transfer', label: 'Transfer' },
  { key: 'policy', label: 'Reorder level' },
];

/** The drawer fields of each stock action; dates default to `today`. */
export function stockActionFields(lookups: InventoryLookups, today: Date): Record<StockFormAction, FieldSpec[]> {
  const warehouse: FieldSpec = { key: 'warehouseId', label: 'Warehouse', type: 'lookup', lookup: lookups.warehouse, required: true };
  const item: FieldSpec = { key: 'itemId', label: 'Item', type: 'lookup', lookup: lookups.product, required: true };
  const date: FieldSpec = { key: 'date', label: 'Date', type: 'date', required: true, default: today };
  const reference: FieldSpec = { key: 'reference', label: 'Reference', type: 'text', maxLength: 100 };
  const memo: FieldSpec = { key: 'memo', label: 'Memo', type: 'text', maxLength: 500 };
  const quantity: FieldSpec = { key: 'quantity', label: 'Quantity', type: 'number', min: 0.0001, precision: 4, required: true };
  return {
    receive: [warehouse, item, quantity, { key: 'unitCost', label: 'Unit cost', type: 'number', min: 0, default: 0, required: true },
      date, { key: 'offsetAccountId', label: 'Credit account (payable / bank)', type: 'lookup', lookup: lookups.account(), required: true, hint: 'Where the other side of the entry goes.' }, reference, memo],
    issue: [warehouse, item, quantity, date,
      { key: 'expenseAccountId', label: 'Expense account', type: 'lookup', lookup: lookups.account('expense'), hint: 'Defaults to Cost of goods sold.' }, reference, memo],
    adjust: [warehouse, item, { key: 'quantityDelta', label: 'Change in quantity (+/-)', type: 'number', precision: 4, required: true }, { key: 'unitCost', label: 'Unit cost (for increases)', type: 'number', min: 0, default: 0 }, date, memo],
    transfer: [{ ...warehouse, key: 'fromWarehouseId', label: 'From warehouse' }, { ...warehouse, key: 'toWarehouseId', label: 'To warehouse' }, item, quantity, date, memo],
    policy: [item, { key: 'reorderLevel', label: 'Reorder level', type: 'number', min: 0, precision: 4, required: true, default: 0 }, { key: 'reorderQuantity', label: 'Reorder quantity', type: 'number', min: 0, precision: 4, default: 0 }],
  };
}

/** The drawer values as the API takes them: a picked date becomes its local calendar day. */
export function stockActionBody(values: Record<string, unknown>): Record<string, unknown> {
  const date = values['date'];
  return { ...values, ...(date ? { date: typeof date === 'string' ? date : toIsoDate(date as Date) } : {}) };
}

/** The toast after a stock action was saved. */
export function stockActionDone(action: StockFormAction, values: Record<string, unknown>): string {
  switch (action) {
    case 'policy': return 'Reorder policy saved';
    case 'adjust': return `Stock adjusted by ${values['quantityDelta']}`;
    case 'receive': return `${values['quantity']} received into stock`;
    case 'issue': return `${values['quantity']} issued from stock`;
    default: return `${values['quantity']} transferred between warehouses`;
  }
}
