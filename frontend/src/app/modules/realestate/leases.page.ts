import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FilterCriterion } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { LeaseDto, LeaseListItem, LeasesApi, PAYMENT_PLANS, RealEstateLookups } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';

/** Field specs of the lease form (new lease on the list, edit-draft on the detail page). */
export function leaseFields(lookups: RealEstateLookups, editing: boolean): FieldSpec[] {
  return [
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: lookups.availableUnit, required: true, hint: 'Only vacant or reserved units are listed.', currentLabel: (r) => (r as unknown as { unitNumber?: string }).unitNumber },
    { key: 'lesseePartyId', label: 'Tenant', type: 'lookup', lookup: lookups.party('customer'), required: true, currentLabel: (r) => (r as unknown as { tenantName?: string }).tenantName },
    { key: 'startDate', label: 'Start date', type: 'date', required: true },
    { key: 'endDate', label: 'End date', type: 'date', required: true },
    { key: 'annualRent', label: 'Annual rent (excl. VAT)', type: 'number', required: true, min: 0 },
    { key: 'paymentFrequency', label: 'Payment plan', type: 'select', required: true, options: PAYMENT_PLANS, default: 4 },
    { key: 'securityDepositAmount', label: 'Security deposit', type: 'number', min: 0, default: 0 },
    { key: 'graceDays', label: 'Rent-free days', type: 'number', min: 0, precision: 0, default: 0, hint: 'Billing starts after the free period.' },
    { key: 'noticePeriodDays', label: 'Notice period (days)', type: 'number', min: 0, max: 365, precision: 0, default: 90 },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
    ...(editing ? [] : [{ key: 'activateNow', label: 'Activate now', type: 'switch', default: false } as FieldSpec]),
  ];
}

@Component({
  selector: 'erp-leases',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Leases</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search lease number" emptyText="No leases yet" (rowClick)="open($event)">
      <nz-select toolbar style="width:200px" [ngModel]="view" (ngModelChange)="setView($event)" data-testid="lease-view">
        <nz-option nzValue="all" nzLabel="All leases"></nz-option>
        <nz-option nzValue="active" nzLabel="Active"></nz-option>
        <nz-option nzValue="expiring" nzLabel="Expiring in 60 days"></nz-option>
        <nz-option nzValue="draft" nzLabel="Drafts"></nz-option>
        <nz-option nzValue="past" nzLabel="Ended / terminated"></nz-option>
      </nz-select>
      <button toolbar *erpHasPermission="'realestate.leases.create'" nz-button nzType="primary" (click)="state.create()" data-testid="lease-new"><span nz-icon nzType="plus"></span>New lease</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer title="New lease" [open]="state.open()" [fields]="fields" customFieldEntityType="realestate.lease" [saving]="state.saving()" [errors]="state.errors()"
                       testId="lease-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class LeasesPage {
  private readonly api = inject(LeasesApi);
  private readonly router = inject(Router);
  view = 'all';
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-startDate' });
  readonly fields = leaseFields(inject(RealEstateLookups), false);
  readonly state = new MasterDataState<LeaseDto>({
    save: (_, body) => this.api.saveDraft(null, body), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Lease',
  });
  readonly columns: TableColumn<LeaseListItem>[] = [
    { key: 'number', label: 'Lease', sortField: 'number', width: '150px' },
    { key: 'tenantName', label: 'Tenant' },
    { key: 'propertyName', label: 'Property' },
    { key: 'unitNumber', label: 'Unit', width: '90px' },
    { key: 'startDate', label: 'Start', sortField: 'startDate' },
    { key: 'endDate', label: 'End', sortField: 'endDate' },
    { key: 'daysToExpiry', label: 'Days left', value: (l) => (l.daysToExpiry == null ? '' : l.daysToExpiry >= 0 ? `${l.daysToExpiry} days left` : `ended ${-l.daysToExpiry} days ago`) },
    { key: 'annualRent', label: 'Annual rent', sortField: 'annualRent', value: (l) => l.annualRent.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status' },
  ];

  setView(view: string): void {
    this.view = view;
    const inDays = (n: number) => { const d = new Date(); d.setDate(d.getDate() + n); return toIsoDate(d); };
    const filters: Record<string, FilterCriterion[]> = {
      all: [],
      active: [{ field: 'status', op: 'eq', value: 'active' }],
      draft: [{ field: 'status', op: 'eq', value: 'draft' }],
      past: [{ field: 'status', op: 'in', value: ['ended', 'terminated', 'renewed', 'cancelled'] }],
      expiring: [{ field: 'status', op: 'eq', value: 'active' }, { field: 'endDate', op: 'lte', value: inDays(60) }],
    };
    this.list.setFilters(filters[view] ?? []);
  }

  open(l: LeaseListItem): void { void this.router.navigate(['/real-estate/leases', l.id]); }
}
