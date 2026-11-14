import { inject } from '@angular/core';
import { OWNERSHIPS, PAYMENT_PLANS, PROPERTY_TYPES, RealEstateLookups } from '@core/api/realestate.api';
import { toIsoDate } from '@core/format/dates';
import { I18nService } from '@core/i18n/i18n.service';
import { SessionStore } from '@core/session/session.store';
import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';

/** Field specs for the property form (shared by the list and the detail page). */
export function propertyFields(lookups: RealEstateLookups): FieldSpec[] {
  /** The owner fields only apply to a property managed for an owner. */
  const notManagedForOwner = (v: Record<string, unknown>): boolean => v['ownershipType'] !== 'managedForOwner';
  return [
    { key: 'legalEntityId', label: 'Owning legal entity', type: 'select', required: true, options: lookups.legalEntities(), readonlyOnEdit: false },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'propertyType', label: 'Type', type: 'select', required: true, options: PROPERTY_TYPES, default: 'residential' },
    { key: 'ownershipType', label: 'Ownership', type: 'select', required: true, options: OWNERSHIPS, default: 'owned' },
    { key: 'managementFeePercent', label: 'Management fee %', type: 'number', min: 0, max: 100, default: 0, hidden: notManagedForOwner, hint: 'Charged on rent collected; used in the owner statement.' },
    { key: 'ownerPartyId', label: 'Owner', type: 'lookup', lookup: lookups.party('other'), hidden: notManagedForOwner, required: true },
    { key: 'creditRentToOwner', label: 'Rent is credited to', type: 'select', options: [{ value: true, label: 'The owner (owner funds payable)' }, { value: false, label: 'Company income' }], hidden: notManagedForOwner, hint: 'Left empty, rent of a property managed for an owner is credited to the owner and paid out from the owner payouts screen. Choose company income to keep it as revenue of the company.' },
    { key: 'region', label: 'Region / emirate', type: 'text', maxLength: 100 },
    { key: 'community', label: 'Community / area', type: 'text', maxLength: 100 },
    { key: 'plotNumber', label: 'Plot number', type: 'text', maxLength: 50 },
    { key: 'yearBuilt', label: 'Year built', type: 'number', min: 1800, max: 2200, precision: 0 },
    { key: 'totalUnits', label: 'Total lettable units', type: 'number', min: 0, precision: 0, default: 0, hint: 'Never below the number of registered units.' },
    { key: 'indicativeValue', label: 'Indicative value', type: 'number', min: 0, default: 0 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
  ];
}

/**
 * Mirrors the backend rule Lease.DefaultEndDate: a one-year term, start + 1 year - 1 day. Like .NET AddYears, 29 Feb moves to
 * 28 Feb before the day is taken off.
 */
export function defaultLeaseEnd(start: Date): Date {
  const year = start.getFullYear() + 1, month = start.getMonth();
  return new Date(year, month, Math.min(start.getDate(), new Date(year, month + 1, 0).getDate()) - 1);
}

const isDate = (v: unknown): v is Date => v instanceof Date && !isNaN(v.getTime());

/** The lookup label of a record field the list rows carry ("unitNumber", "tenantName"). */
const recordText = (key: string) => (r: Record<string, unknown>): string | undefined => r[key] as string | undefined;

/**
 * Field specs of the lease form (new lease on the list, edit-draft on the detail page). Call in an injection context.
 * The end date follows the start date (one-year term) until the user picks another one; clearing it brings the rule back.
 */
export function leaseFields(lookups: RealEstateLookups, editing: boolean): FieldSpec[] {
  const i18n = inject(I18nService);
  const session = inject(SessionStore);
  return [
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: lookups.availableUnit, required: true, hint: 'Only vacant or reserved units are listed.', currentLabel: recordText('unitNumber') },
    { key: 'lesseePartyId', label: 'Tenant', type: 'lookup', lookup: lookups.party('customer'), required: true, currentLabel: recordText('tenantName') },
    { key: 'startDate', label: 'Start date', type: 'date', required: true, onChange: (start, _form, previous) => {
      if (!isDate(start)) return null;
      const end = previous['endDate'], oldStart = previous['startDate'];
      const auto = !isDate(end) || (isDate(oldStart) && toIsoDate(end) === toIsoDate(defaultLeaseEnd(oldStart)));
      return auto ? { endDate: defaultLeaseEnd(start) } : null;
    } },
    { key: 'endDate', label: 'End date', type: 'date', hint: i18n.t('ui.re.endDateHint', 'Set to one year from the start date unless you pick another date.'),
      onChange: (end, form) => (!isDate(end) && isDate(form['startDate']) ? { endDate: defaultLeaseEnd(form['startDate']) } : null) },
    { key: 'annualRent', label: 'Annual rent (excl. VAT)', type: 'number', required: true, min: 0 },
    { key: 'paymentFrequency', label: 'Payment plan', type: 'select', required: true, options: PAYMENT_PLANS, default: 4 },
    { key: 'securityDepositAmount', label: 'Security deposit', type: 'number', min: 0, default: 0 },
    { key: 'graceDays', label: 'Rent-free days', type: 'number', min: 0, precision: 0, default: 0, hint: 'Billing starts after the free period.' },
    { key: 'noticePeriodDays', label: 'Notice period (days)', type: 'number', min: 0, max: 365, precision: 0, default: 90 },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
    // A lease activated on creation needs realestate.leases.activate (CreateLeaseCommand), so only those users see the switch.
    ...(editing ? [] : [{ key: 'activateNow', label: 'Activate now', type: 'switch', default: false, hidden: () => !session.hasPermission('realestate.leases.activate') } as FieldSpec]),
  ];
}
