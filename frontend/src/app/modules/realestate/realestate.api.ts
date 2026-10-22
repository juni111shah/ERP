import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudApiService } from '../../core/api/crud-api.service';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { SessionStore } from '../../core/session/session.store';
import { SelectOption } from '../../shared/record-drawer.component';

type CustomFields = Record<string, unknown>;

// ---- Models (mirror ERP.Modules.RealEstate.Features DTOs; enums are camelCase strings) --------------------------------

export interface UnitTypeDto { id: string; code: string; name: string; defaultUsage: string; defaultBedrooms?: number; isActive: boolean; }

export interface PropertyDto {
  id: string; code: string; name: string; legalEntityId: string; propertyType: string; ownershipType: string; ownerPartyId?: string; region?: string;
  community?: string; plotNumber?: string; yearBuilt?: number; totalUnits: number; indicativeValue: number; isActive: boolean; notes?: string; customFields: CustomFields;
}
export interface PropertySummary {
  property: PropertyDto; registeredUnits: number; occupiedUnits: number; occupancyPercent: number; contractedAnnualRent: number;
  grossYieldPercent: number; unitsNotFullyRegistered: boolean;
}

export interface UnitListItem {
  id: string; propertyId: string; propertyName: string; unitNumber: string; unitTypeName: string; usage: string; floor?: string; areaSqft: number;
  askingRent: number; status: string; tenantName?: string; currentRent?: number; leaseEnds?: string; daysVacant?: number;
}
export interface UnitDto {
  id: string; propertyId: string; legalEntityId: string; unitNumber: string; unitTypeId: string; usage: string; floor?: string; bedrooms?: number;
  areaSqft: number; askingRent: number; status: string; vacantSince?: string; daysVacant?: number; reservedUntil?: string; unavailableReason?: string;
  notes?: string; customFields: CustomFields;
}

export interface InstalmentDto { id: string; sequence: number; periodFrom: string; periodTo: string; dueDate: string; amount: number; status: string; invoiceId?: string; }
export interface LeaseListItem {
  id: string; number?: string; unitId: string; unitNumber: string; propertyId: string; propertyName: string; lesseePartyId: string; tenantName: string;
  startDate: string; endDate: string; daysToExpiry?: number; annualRent: number; paymentFrequency: number; securityDepositAmount: number; status: string;
}
export interface LeaseDto {
  id: string; number?: string; unitId: string; unitNumber: string; propertyId: string; legalEntityId: string; lesseePartyId: string; tenantName: string; startDate: string; endDate: string;
  annualRent: number; currencyCode: string; paymentFrequency: number; securityDepositAmount: number; taxCodeId?: string; suggestedVat: string; status: string;
  activatedAt?: string; terminationDate?: string; terminationReason?: string; renewalOfLeaseId?: string; renewedAsLeaseId?: string; graceDays: number;
  noticePeriodDays: number; holdingOverFlag: boolean; notes?: string; rentForTerm: number; daysToExpiry?: number; customFields: CustomFields; schedule: InstalmentDto[];
}
export interface TerminationResult { lease: LeaseDto; cancelledInstalments: number; instalmentsNeedingCreditNote: InstalmentDto[]; }

export interface WorkOrderDto {
  id: string; number?: string; propertyId: string; unitId?: string; leaseId?: string; legalEntityId: string; issue: string; category?: string; priority: string;
  status: string; reportedOn: string; reportedBy?: string; vendorPartyId?: string; technician?: string; scheduledFor?: string; completedOn?: string;
  estimatedCost: number; actualCost?: number; details?: string; resolutionNotes?: string; customFields: CustomFields;
}
export interface DealDto {
  id: string; number?: string; legalEntityId: string; dealType: string; propertyId?: string; unitId?: string; unitReference?: string; clientPartyId?: string;
  clientName: string; agentPartyId?: string; externalAgentName?: string; teamLeadPartyId?: string; dealDate: string; stage: string; dealValue: number;
  currencyCode: string; commissionRatePercent: number; agentSharePercent: number; teamLeadSharePercent: number; grossCommission: number; agentCommission: number;
  teamLeadCommission: number; lostReason?: string; notes?: string; customFields: CustomFields;
}

// ---- Option lists ------------------------------------------------------------------------------------------------

export const USAGES: SelectOption[] = [{ value: 'residential', label: 'Residential' }, { value: 'commercial', label: 'Commercial' }];
export const PROPERTY_TYPES: SelectOption[] = [
  { value: 'residential', label: 'Residential building' }, { value: 'villa', label: 'Villa compound' }, { value: 'commercial', label: 'Commercial building' },
  { value: 'mixedUse', label: 'Mixed-use' }, { value: 'retail', label: 'Retail centre' }, { value: 'industrial', label: 'Warehouse / industrial' }, { value: 'land', label: 'Land' },
];
export const OWNERSHIPS: SelectOption[] = [{ value: 'owned', label: 'Owned' }, { value: 'managedForOwner', label: 'Managed for an owner' }];
export const PAYMENT_PLANS: SelectOption[] = [
  { value: 1, label: 'Annual (1)' }, { value: 2, label: 'Semi-annual (2)' }, { value: 4, label: 'Quarterly (4)' }, { value: 6, label: 'Bi-monthly (6)' }, { value: 12, label: 'Monthly (12)' },
];
export const PRIORITIES: SelectOption[] = [{ value: 'low', label: 'Low' }, { value: 'normal', label: 'Normal' }, { value: 'high', label: 'High' }, { value: 'critical', label: 'Critical' }];
export const DEAL_TYPES: SelectOption[] = [{ value: 'sale', label: 'Sale' }, { value: 'lease', label: 'Lease' }];

/** Responsive column counts for <nz-descriptions> (all breakpoints are required). */
export const DESCRIPTION_COLUMNS = { xxl: 2, xl: 2, lg: 2, md: 2, sm: 1, xs: 1 };

/** "mixedUse" -> "Mixed use": readable text for camelCase enum values. */
export function humanize(value: string | null | undefined): string {
  return value ? value.replace(/([A-Z])/g, ' $1').replace(/^./, (c) => c.toUpperCase()) : '';
}

export const STATUS_COLORS: Record<string, string> = {
  vacant: 'green', reserved: 'gold', occupied: 'blue', unavailable: 'default',
  draft: 'default', active: 'green', ended: 'default', renewed: 'cyan', terminated: 'red', holdingOver: 'orange', cancelled: 'default',
  scheduled: 'blue', invoiced: 'green',
  new: 'blue', assigned: 'cyan', inProgress: 'processing', onHold: 'orange', completed: 'green', closed: 'default',
  lead: 'default', viewing: 'blue', negotiation: 'gold', agreed: 'green', lost: 'red',
};

// ---- API clients (one line per resource) -------------------------------------------------------------------------

const BASE = 'm/realestate';

@Injectable({ providedIn: 'root' })
export class UnitTypesApi extends CrudApiService<UnitTypeDto, unknown> { protected readonly resource = `${BASE}/unit-types`; }

@Injectable({ providedIn: 'root' })
export class PropertiesApi extends CrudApiService<PropertyDto, unknown> {
  protected readonly resource = `${BASE}/properties`;
  summary(id: string): Observable<PropertySummary> { return this.http.get<PropertySummary>(`${this.url}/${id}`); }
}

@Injectable({ providedIn: 'root' })
export class UnitsApi extends CrudApiService<UnitListItem, unknown> {
  protected readonly resource = `${BASE}/units`;
  detail(id: string): Observable<UnitDto> { return this.http.get<UnitDto>(`${this.url}/${id}`); }
  bulk(body: unknown): Observable<UnitDto[]> { return this.http.post<UnitDto[]>(`${this.url}/bulk`, body); }
  changeStatus(id: string, body: { action: string; until?: string | null; reason?: string | null }): Observable<UnitDto> { return this.http.post<UnitDto>(`${this.url}/${id}/status`, body); }
}

@Injectable({ providedIn: 'root' })
export class LeasesApi extends CrudApiService<LeaseListItem, unknown> {
  protected readonly resource = `${BASE}/leases`;
  detail(id: string): Observable<LeaseDto> { return this.http.get<LeaseDto>(`${this.url}/${id}`); }
  saveDraft(id: string | null, body: unknown): Observable<LeaseDto> { return id ? this.http.put<LeaseDto>(`${this.url}/${id}`, body) : this.http.post<LeaseDto>(this.url, body); }
  activate(id: string): Observable<LeaseDto> { return this.http.post<LeaseDto>(`${this.url}/${id}/activate`, {}); }
  renew(id: string, body: unknown): Observable<LeaseDto> { return this.http.post<LeaseDto>(`${this.url}/${id}/renew`, body); }
  terminate(id: string, body: { date: string; reason: string }): Observable<TerminationResult> { return this.http.post<TerminationResult>(`${this.url}/${id}/terminate`, body); }
  cancel(id: string): Observable<LeaseDto> { return this.http.post<LeaseDto>(`${this.url}/${id}/cancel`, {}); }
  holdingOver(id: string, holdingOver: boolean): Observable<LeaseDto> { return this.http.post<LeaseDto>(`${this.url}/${id}/holding-over`, { holdingOver }); }
  moveOut(id: string): Observable<LeaseDto> { return this.http.post<LeaseDto>(`${this.url}/${id}/move-out`, {}); }
  processExpiries(): Observable<{ ended: number; heldOver: number; reservationsReleased: number }> { return this.http.post<{ ended: number; heldOver: number; reservationsReleased: number }>(`${this.url}/process-expiries`, {}); }
}

@Injectable({ providedIn: 'root' })
export class WorkOrdersApi extends CrudApiService<WorkOrderDto, unknown> {
  protected readonly resource = `${BASE}/work-orders`;
  changeStatus(id: string, body: { status: string; actualCost?: number | null; resolutionNotes?: string | null }): Observable<WorkOrderDto> { return this.http.post<WorkOrderDto>(`${this.url}/${id}/status`, body); }
}

@Injectable({ providedIn: 'root' })
export class DealsApi extends CrudApiService<DealDto, unknown> {
  protected readonly resource = `${BASE}/deals`;
  moveStage(id: string, body: { stage: string; lostReason?: string | null }): Observable<DealDto> { return this.http.post<DealDto>(`${this.url}/${id}/stage`, body); }
}

@Injectable({ providedIn: 'root' })
export class PartiesLookupApi extends CrudApiService<{ id: string; code: string; name: string }, unknown> { protected readonly resource = 'parties'; }

/** Server-searched pick-lists for form lookups (id + label), built on the same list endpoints the tables use. */
@Injectable({ providedIn: 'root' })
export class RealEstateLookups {
  private readonly properties = inject(PropertiesApi);
  private readonly units = inject(UnitsApi);
  private readonly unitTypes = inject(UnitTypesApi);
  private readonly session = inject(SessionStore);
  private readonly parties = inject(PartiesLookupApi);

  property = (term: string): Observable<LookupOption[]> =>
    this.properties.list({ page: 1, pageSize: 20, search: term, sort: 'name' }).pipe(map((r) => r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));
  unitType = (term: string): Observable<LookupOption[]> =>
    this.unitTypes.list({ page: 1, pageSize: 20, search: term, filters: [{ field: 'isActive', op: 'eq', value: true }] }).pipe(map((r) => r.items.map((t) => ({ value: t.id, label: `${t.code} · ${t.name}` }))));
  /** Units that can be let (vacant or reserved). */
  availableUnit = (term: string): Observable<LookupOption[]> =>
    this.units.list({ page: 1, pageSize: 20, search: term, filters: [{ field: 'status', op: 'in', value: ['vacant', 'reserved'] }] })
      .pipe(map((r) => r.items.map((u) => ({ value: u.id, label: `${u.propertyName} · ${u.unitNumber}` }))));
  unit = (term: string): Observable<LookupOption[]> =>
    this.units.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((u) => ({ value: u.id, label: `${u.propertyName} · ${u.unitNumber}` }))));
  party = (role: string) => (term: string): Observable<LookupOption[]> =>
    this.parties.list({ page: 1, pageSize: 20, search: term, extra: { role } }).pipe(map((r) => r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));

  /** Legal entities the signed-in user may work in. */
  legalEntities(): SelectOption[] {
    return (this.session.me()?.legalEntities ?? []).map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` }));
  }
}
