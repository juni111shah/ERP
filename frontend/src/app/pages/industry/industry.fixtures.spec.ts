import { IndustryRecordDto, PackView, RecordTypeView } from '../../core/api/industry.api';
import { MeResponse } from '../../core/session/session.store';

/** A record type that uses every kind of field and option, for the specs of the generic record screens. */
export const SHIPMENTS: RecordTypeView = {
  key: 'shipments', label: 'Shipments', singular: 'Shipment', icon: 'send', titleLabel: 'Consignment',
  fields: [
    { key: 'origin', label: 'Origin', type: 'text', required: true, inList: true, sensitive: false },
    { key: 'notes2', label: 'Handling notes', type: 'textarea', required: false, inList: false, sensitive: false },
    { key: 'weight', label: 'Weight', type: 'number', required: false, inList: true, sensitive: false },
    { key: 'cost', label: 'Declared value', type: 'money', required: false, inList: false, sensitive: false },
    { key: 'pickup', label: 'Pickup', type: 'date', required: false, inList: false, sensitive: false },
    { key: 'fragile', label: 'Fragile', type: 'bool', required: false, inList: true, sensitive: false },
    { key: 'mode', label: 'Mode', type: 'select', required: true, inList: true, sensitive: false, options: ['Road', 'Air'] },
  ],
  statuses: [
    { key: 'draft', label: 'Draft', tone: 'default' }, { key: 'picked', label: 'Picked up', tone: 'processing' },
    { key: 'delivered', label: 'Delivered', tone: 'success' }, { key: 'cancelled', label: 'Cancelled', tone: 'error' },
  ],
  partyRole: 'Customer', partyLabel: 'Customer', parentType: 'vehicles', parentLabel: 'Vehicle',
  hasLines: true, linesLabel: 'Charges', hasAmount: true, hasDueDate: true, canInvoice: true, canBill: false, perLineBilling: false, billableStatuses: ['delivered'],
  consumesStock: false, tracksProgress: false, sensitive: true,
};

export const PACK: PackView = { key: 'logistics', name: 'Logistics', description: 'Moves and deliveries', types: [SHIPMENTS] };

export const RECORD: IndustryRecordDto = {
  id: 'r1', moduleKey: 'logistics', recordType: 'shipments', legalEntityId: 'le1', number: 'SHP-000001', title: '20 pallets', status: 'draft',
  partyId: 'p1', partyName: 'Gulf Traders', parentId: null, parentTitle: null, businessUnitId: null, recordDate: '2026-03-01', dueDate: '2026-03-10', amount: 1500,
  data: { origin: 'Doha', mode: 'Road', fragile: true }, notes: null, invoiceId: null, billId: null, consumedOn: null, createdAt: '2026-03-01T08:00:00Z',
  lines: [{ id: 'l1', seq: 1, code: null, description: 'Road freight', itemId: null, unitOfMeasure: null, quantity: 1, unitPrice: 1500, amount: 1500, completedQuantity: 0, dueDate: null, invoiceId: null }],
  events: [{ id: 'e1', fromStatus: null, toStatus: 'draft', occurredAt: '2026-03-01T08:00:00Z', userName: 'admin@example.test', reason: null }],
  nextSteps: [
    { from: 'draft', to: 'picked', label: 'Mark picked up', reasonRequired: false, approval: false },
    { from: 'draft', to: 'cancelled', label: 'Cancel shipment', reasonRequired: true, approval: false },
  ],
};

export function meWith(permissions: string[], modules: string[] = ['logistics']): MeResponse {
  return {
    user: { id: 'u1', email: 'u@example.test', fullName: 'User', mustChangePassword: false },
    workspace: { id: 'w1', code: 'w1', name: 'Workspace', countryCode: 'AE', baseCurrency: 'AED', timeZone: 'UTC', defaultLocale: 'en' },
    workspaces: [], permissions, legalEntities: [{ id: 'le1', code: 'MAIN', name: 'Main company', baseCurrency: 'AED' }], allLegalEntities: true,
    enabledModules: modules.map((key) => ({ key, uiRoute: '/' + key })),
  };
}
