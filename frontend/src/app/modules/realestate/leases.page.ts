import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
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
import { I18nService } from '../../core/i18n/i18n.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * Mirrors the backend rule Lease.DefaultEndDate: a one-year term, start + 1 year - 1 day. Like .NET AddYears, 29 Feb moves to
 * 28 Feb before the day is taken off.
 */
export function defaultLeaseEnd(start: Date): Date {
  const year = start.getFullYear() + 1, month = start.getMonth();
  return new Date(year, month, Math.min(start.getDate(), new Date(year, month + 1, 0).getDate()) - 1);
}

const isDate = (v: unknown): v is Date => v instanceof Date && !isNaN(v.getTime());

/**
 * Field specs of the lease form (new lease on the list, edit-draft on the detail page). Call in an injection context.
 * The end date follows the start date (one-year term) until the user picks another one; clearing it brings the rule back.
 */
export function leaseFields(lookups: RealEstateLookups, editing: boolean): FieldSpec[] {
  const i18n = inject(I18nService);
  return [
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: lookups.availableUnit, required: true, hint: 'Only vacant or reserved units are listed.', currentLabel: (r) => (r as unknown as { unitNumber?: string }).unitNumber },
    { key: 'lesseePartyId', label: 'Tenant', type: 'lookup', lookup: lookups.party('customer'), required: true, currentLabel: (r) => (r as unknown as { tenantName?: string }).tenantName },
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
    ...(editing ? [] : [{ key: 'activateNow', label: 'Activate now', type: 'switch', default: false } as FieldSpec]),
  ];
}

import { DecimalPipe } from '@angular/common';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-leases',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzTagModule, StatCardComponent, TranslatePipe, NzToolTipModule, RevealDirective, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Leases</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="lease-stats">
      <erp-stat-card label="Total leases"
                     [value]="list.total()"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     [caption]="stats().count + ' ' + ('table.onThisPage' | translate: 'on this page')"
                     [tone]="0" />
      <erp-stat-card label="Active tenancy"
                     [value]="stats().active"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="active contracts"
                     [tone]="1" />
      <erp-stat-card label="Expiring in 60d"
                     [value]="stats().expiringSoon"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     [caption]="stats().expiringSoon > 0 ? 'renewal action due' : 'no urgent expiries'"
                     [tone]="stats().expiringSoon > 0 ? 2 : 'neutral'" />
      <erp-stat-card label="Drafts"
                     [value]="stats().drafts"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="in preparation"
                     [tone]="3" />
    </div>

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
        @switch (column.key) {
          @case ('number') {
            <span class="code-pill">{{ row.number }}</span>
          }
          @case ('tenantName') {
            <span class="name-cell">{{ row.tenantName }}</span>
          }
          @case ('unitNumber') {
            <span class="code-pill">{{ row.unitNumber }}</span>
          }
          @case ('status') {
            <erp-status-tag [status]="row.status" />
          }
          @case ('daysToExpiry') {
            @if (row.daysToExpiry != null) {
              @if (row.daysToExpiry <= 60 && row.daysToExpiry >= 0) {
                <nz-tag nzColor="orange">{{ value }}</nz-tag>
              } @else if (row.daysToExpiry < 0) {
                <nz-tag nzColor="default">{{ value }}</nz-tag>
              } @else {
                <span>{{ value }}</span>
              }
            }
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer title="New lease" [open]="state.open()" [fields]="fields" customFieldEntityType="realestate.lease" [saving]="state.saving()" [errors]="state.errors()"
                       testId="lease-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class LeasesPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(LeasesApi);
  private readonly router = inject(Router);
  view = 'all';
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-startDate' });

  readonly stats = computed(() => {
    const items = this.list.items();
    const active = items.filter((l) => l.status === 'active').length;
    const expiringSoon = items.filter((l) => l.daysToExpiry != null && l.daysToExpiry <= 60 && l.daysToExpiry >= 0).length;
    const drafts = items.filter((l) => l.status === 'draft').length;
    return {
      active,
      expiringSoon,
      drafts,
      count: items.length,
    };
  });
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
    { key: 'daysToExpiry', label: 'Days left', value: (l) => (l.daysToExpiry == null ? '' : l.daysToExpiry >= 0 ? `${l.daysToExpiry} ${this.i18n.t('ui.re.daysLeft', 'days left')}` : `${this.i18n.t('ui.re.endedAgo', 'ended')} ${-l.daysToExpiry} ${this.i18n.t('ui.re.daysAgo', 'days ago')}`) },
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
