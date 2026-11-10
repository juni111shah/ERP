import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '@core/format/done-text';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '@core/api/paged-list';
import { ProblemDetails } from '@core/api/api.models';
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RealEstateLookups, USAGES, UnitDto, UnitListItem, UnitsApi, humanize } from '@core/api/realestate.api';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

const STATUSES = ['vacant', 'reserved', 'occupied', 'unavailable'];

import { DecimalPipe } from '@angular/common';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';

@Component({
  selector: 'erp-units',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, StatCardComponent, TranslatePipe, NzToolTipModule, RevealDirective, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  templateUrl: './units.page.html',
})
export class UnitsPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(UnitsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly humanize = humanize;
  readonly statuses = STATUSES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'unitNumber' });

  readonly stats = computed(() => {
    const items = this.list.items();
    const occupied = items.filter((u) => u.status === 'occupied').length;
    const vacant = items.filter((u) => u.status === 'vacant').length;
    const potentialRent = items.reduce((s, u) => s + (u.askingRent || 0), 0);
    return {
      occupied,
      vacant,
      potentialRent,
      count: items.length,
      partial: this.list.total() > items.length,
    };
  });
  readonly state = new MasterDataState<UnitDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Unit',
  });

  readonly columns: TableColumn<UnitListItem>[] = [
    { key: 'propertyName', label: 'Property' },
    { key: 'unitNumber', label: 'Unit', sortField: 'unitNumber', width: '100px' },
    { key: 'unitTypeName', label: 'Type' },
    { key: 'usage', label: 'Usage', sortField: 'usage', value: (u) => humanize(u.usage) },
    { key: 'floor', label: 'Floor' },
    { key: 'areaSqft', label: 'Area (sq ft)', sortField: 'areaSqft', align: 'right' },
    { key: 'askingRent', label: 'Asking rent', sortField: 'askingRent', align: 'right', value: (u) => u.askingRent.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: STATUSES },
    { key: 'tenantName', label: 'Tenant' },
    { key: 'currentRent', label: 'Current rent', align: 'right', value: (u) => u.currentRent?.toLocaleString() },
    { key: 'leaseEnds', label: 'Lease ends' },
    { key: 'daysVacant', label: 'Days vacant', align: 'right' },
  ];

  readonly fields: FieldSpec[] = [
    { key: 'propertyId', label: 'Property', type: 'lookup', lookup: this.lookups.property, required: true, readonlyOnEdit: true, currentLabel: (r) => (r as unknown as UnitDto & { propertyName?: string }).propertyName },
    { key: 'unitNumber', label: 'Unit number', type: 'text', required: true, maxLength: 30 },
    { key: 'unitTypeId', label: 'Unit type', type: 'lookup', lookup: this.lookups.unitType, required: true },
    { key: 'usage', label: 'Usage', type: 'select', options: USAGES, hint: 'Defaults from the unit type; drives the VAT treatment of leases.' },
    { key: 'floor', label: 'Floor', type: 'text', maxLength: 10 },
    { key: 'bedrooms', label: 'Bedrooms', type: 'number', min: 0, max: 20, precision: 0 },
    { key: 'areaSqft', label: 'Area (sq ft)', type: 'number', min: 0, default: 0 },
    { key: 'askingRent', label: 'Annual asking rent', type: 'number', min: 0, default: 0 },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
  ];

  readonly bulkFields: FieldSpec[] = [
    { key: 'propertyId', label: 'Property', type: 'lookup', lookup: this.lookups.property, required: true },
    { key: 'unitTypeId', label: 'Unit type', type: 'lookup', lookup: this.lookups.unitType, required: true },
    { key: 'fromFloor', label: 'From floor', type: 'number', required: true, min: 0, precision: 0, default: 1 },
    { key: 'toFloor', label: 'To floor', type: 'number', required: true, min: 0, precision: 0, default: 1 },
    { key: 'unitsPerFloor', label: 'Units per floor', type: 'number', required: true, min: 1, max: 99, precision: 0, default: 4, hint: 'Numbers are floor + position: floor 14, unit 6 = 1406.' },
    { key: 'prefix', label: 'Number prefix', type: 'text', maxLength: 10 },
    { key: 'areaSqft', label: 'Area (sq ft)', type: 'number', min: 0, default: 0 },
    { key: 'askingRent', label: 'Annual asking rent', type: 'number', min: 0, default: 0 },
  ];
  readonly noFields: FieldSpec[] = [];
  readonly bulkOpen = signal(false);
  readonly bulkSaving = signal(false);
  readonly bulkErrors = signal<ProblemDetails | null>(null);

  readonly action = signal<{ title: string; kind: string; unit: UnitDto; fields: FieldSpec[] } | null>(null);
  readonly actionSaving = signal(false);
  readonly actionErrors = signal<ProblemDetails | null>(null);

  filterStatus(status: string | null): void {
    this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []);
  }

  open(row: UnitListItem): void {
    this.api.detail(row.id).subscribe((unit) => this.state.edit({ ...unit, propertyName: row.propertyName } as UnitDto));
  }

  bulkCreate(payload: Record<string, unknown>): void {
    this.bulkSaving.set(true);
    this.bulkErrors.set(null);
    this.api.bulk(payload).subscribe({
      next: (units) => { this.bulkSaving.set(false); this.bulkOpen.set(false); this.message.success(`${units.length} ${this.i18n.t('ui.msg.unitsCreated', 'units created')}`); this.list.reload(); },
      error: (e) => {
        this.bulkSaving.set(false);
        const problem = asProblem(e);
        this.bulkErrors.set(problem);
        if (!problem?.errors) this.message.error(problem?.detail ?? 'Could not create the units');
      },
    });
  }

  /** Lifecycle actions: reserve / release / make (un)available (occupancy itself is set only by leases). */
  startAction(kind: 'reserve' | 'releaseReservation' | 'makeUnavailable' | 'makeAvailable', unit: UnitDto): void {
    if (kind === 'reserve') this.action.set({ title: 'Reserve unit', kind, unit, fields: [{ key: 'until', label: 'Reserved until', type: 'date' }] });
    else if (kind === 'makeUnavailable') this.action.set({ title: 'Make unavailable', kind, unit, fields: [{ key: 'reason', label: 'Reason', type: 'text', required: true, maxLength: 200 }] });
    else this.runAction({}, { kind, unit });
  }

  runAction(values: Record<string, unknown>, direct?: { kind: string; unit: UnitDto }): void {
    const current = direct ?? this.action();
    if (!current) return;
    this.actionSaving.set(true);
    this.actionErrors.set(null);
    this.api.changeStatus(current.unit.id, { action: current.kind, until: (values['until'] as string) ?? null, reason: (values['reason'] as string) ?? null }).subscribe({
      next: () => { this.actionSaving.set(false); this.action.set(null); this.state.close(); this.message.success(doneText('updated', 'Unit', current.unit.unitNumber)); this.list.reload(); },
      error: (e) => { this.actionSaving.set(false); const p = asProblem(e); this.actionErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not update the unit'); },
    });
  }
}
