import { PermissionDefinition } from '@core/api/admin.api';
import { displayLabel } from '@core/format/display';

/** The permission that may decide an approval unless a rule names another. */
export const DEFAULT_APPROVER = 'core.approvals.decide';

/** The documents an approval rule can hold back. */
export const APPROVAL_DOCUMENT_TYPES = [
  { value: 'bill', label: 'Vendor bill' }, { value: 'journal', label: 'Manual journal' }, { value: 'payment', label: 'Payment' },
  { value: 'purchase_order', label: 'Purchase order' }, { value: 'purchase_requisition', label: 'Purchase requisition' },
];

/** A document type in words. */
export function approvalDocumentLabel(type: string): string {
  return APPROVAL_DOCUMENT_TYPES.find((t) => t.value === type)?.label ?? displayLabel(type);
}

/** The permissions a rule can require of its approver: the default one plus every "approve" or "decide" permission, sorted. */
export function approverPermissionKeys(all: PermissionDefinition[]): string[] {
  return [...new Set([DEFAULT_APPROVER, ...all.map((p) => p.key).filter((k) => /\.(approve|decide)$/.test(k))])].sort();
}
