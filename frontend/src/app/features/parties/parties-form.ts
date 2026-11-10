import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FilterCriterion } from '@core/api/api.models';
import { PartyDto } from '@core/api/parties.api';
import { FormSectionMeta } from '@core/custom-fields/custom-field.models';
import { toApiValues } from '@core/custom-fields/custom-field-form';

/** The parties list: its filters and title per role, and the party form with the body the API takes. */

export const PARTY_ROLES = ['Customer', 'Vendor', 'Employee', 'Other'];

/** "Customers", "Vendors" or "Parties" for the `?type=` of the menu entry. */
export const partiesTitle = (type: string | null): string => (type === 'customer' ? 'Customers' : type === 'vendor' ? 'Vendors' : 'Parties');

/** Parties holding a role (customer, vendor...). */
export const roleFilter = (type: string | null): FilterCriterion[] => (type ? [{ field: 'roles', op: 'has', value: type.toLowerCase() }] : []);

/** Parties in a party group. */
export const groupFilter = (id: string | null): FilterCriterion[] => (id ? [{ field: 'partyGroupId', op: 'eq', value: id }] : []);

/** Tag colour of a role. */
export function roleTagColor(role: string): string {
  switch ((role ?? '').toLowerCase()) {
    case 'customer': return 'blue';
    case 'vendor': return 'purple';
    case 'employee': return 'cyan';
    default: return 'default';
  }
}

/** The party form's raw value (numbers can arrive as '' from a cleared input). */
export interface PartyFormValue {
  name: string; kind: string; roles: string[]; email: string; phone: string; taxNumber: string; address: string; currencyCode: string;
  paymentTermsDays: number | string | null; creditLimit: number | string | null; idNumber?: string; iban?: string; partyGroupId: string | null; isActive: boolean;
  customFields?: Record<string, unknown>;
}

/**
 * The party form, filled from `p` (or empty with `defaultRole`). With `locked` (no core.sensitive.read) the ID number and IBAN
 * arrive masked, so they are read-only. Custom fields are set on it once their sections load.
 */
export function partyForm(fb: FormBuilder, p: PartyDto | null | undefined, defaultRole: string, locked: boolean): FormGroup {
  return fb.group({
    name: [p?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    kind: [p?.kind?.toLowerCase() ?? 'company', Validators.required],
    roles: [p?.roles ?? [defaultRole], Validators.required],
    email: [p?.email ?? '', Validators.email],
    phone: [p?.phone ?? ''],
    taxNumber: [p?.taxNumber ?? ''],
    address: [p?.address ?? ''],
    currencyCode: [p?.currencyCode ?? '', Validators.maxLength(3)],
    paymentTermsDays: [p?.paymentTermsDays ?? null, Validators.min(0)],
    creditLimit: [p?.creditLimit ?? null, Validators.min(0)],
    idNumber: [{ value: p?.idNumber ?? '', disabled: locked }],
    iban: [{ value: p?.iban ?? '', disabled: locked }],
    partyGroupId: [p?.partyGroupId ?? null],
    isActive: [p?.isActive ?? true],
    customFields: new FormGroup({}),
  });
}

const emptyNumber = (v: number | string | null): boolean => v === '' || v === null;

/**
 * The form as the API takes it. A cleared credit limit on a party that had one is sent with `clearCreditLimit`.
 * With `locked`, the masked ID number and IBAN are never sent back: the API keeps the stored values.
 */
export function partyBody(v: PartyFormValue, locked: boolean, previousCreditLimit: number | null, sections: FormSectionMeta[]) {
  if (locked) { delete v.idNumber; delete v.iban; }
  return {
    ...v,
    currencyCode: v.currencyCode || null,
    paymentTermsDays: emptyNumber(v.paymentTermsDays) ? null : Number(v.paymentTermsDays),
    creditLimit: emptyNumber(v.creditLimit) ? null : Number(v.creditLimit),
    clearCreditLimit: emptyNumber(v.creditLimit) && previousCreditLimit !== null,
    address: v.address || null,
    ...(locked ? {} : { idNumber: v.idNumber || null, iban: v.iban || null }),
    roles: v.roles.join(', '),
    customFields: toApiValues(sections, (v.customFields ?? {}) as Record<string, unknown>),
  };
}
