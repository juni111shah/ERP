import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '../../core/api/paged-list';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { RealEstateLookups, USAGES, UnitDto, UnitListItem, UnitsApi, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';
import { I18nService } from '../../core/i18n/i18n.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const STATUSES = ['vacant', 'reserved', 'occupied', 'unavailable'];

@Component({
  selector: 'erp-units',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Units</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search unit number" emptyText="No units yet" (rowClick)="open($event)">
      <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All statuses" [ngModel]="null" (ngModelChange)="filterStatus($event)" data-testid="unit-status-filter">
        @for (s of statuses; track s) { <nz-option [nzValue]="s" [nzLabel]="humanize(s)"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'realestate.units.manage'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.addMany' | translate: 'Add many'" nz-tooltip [nzTooltipTitle]="'ui.add.addMany' | translate: 'Add many'" (click)="bulkOpen.set(true)" data-testid="unit-bulk"><span nz-icon nzType="appstore-add"></span>Add many</button>
      <button toolbar *erpHasPermission="'realestate.units.manage'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newUnit' | translate: 'New unit'" nz-tooltip [nzTooltipTitle]="'ui.add.newUnit' | translate: 'New unit'" nzType="primary" (click)="state.create()" data-testid="unit-new"><span nz-icon nzType="plus"></span>New unit</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>

    <erp-record-drawer [title]="state.editing() ? 'Unit ' + state.editing()!.unitNumber : 'New unit'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       customFieldEntityType="realestate.unit" [saving]="state.saving()" [errors]="state.errors()" testId="unit-form"
                       (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as u) {
        <div drawerActions style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:16px" data-testid="unit-actions">
          <erp-status-tag [status]="u.status" />
          @if (u.status === 'unavailable' && u.unavailableReason) { <small>{{ u.unavailableReason }}</small> }
          @if (u.status === 'vacant') {
            <button *erpHasPermission="'realestate.units.reserve'" nz-button nzSize="small" (click)="startAction('reserve', u)" data-testid="unit-reserve">Reserve</button>
          }
          @if (u.status === 'reserved') {
            <button *erpHasPermission="'realestate.units.reserve'" nz-button nzSize="small" (click)="startAction('releaseReservation', u)" data-testid="unit-release">Release</button>
          }
          @if (u.status === 'vacant' || u.status === 'reserved') {
            <button *erpHasPermission="'realestate.units.manage'" nz-button nzSize="small" (click)="startAction('makeUnavailable', u)" data-testid="unit-unavailable">Make unavailable</button>
          }
          @if (u.status === 'unavailable') {
            <button *erpHasPermission="'realestate.units.manage'" nz-button nzSize="small" (click)="startAction('makeAvailable', u)" data-testid="unit-available">Make available</button>
          }
        </div>
      }
    </erp-record-drawer>
    <erp-record-drawer title="Add many units" [open]="bulkOpen()" [fields]="bulkFields" submitLabel="Create units" [saving]="bulkSaving()" [errors]="bulkErrors()" testId="unit-bulk-form"
                       (submitted)="bulkCreate($event)" (closed)="bulkOpen.set(false)" />
    <erp-record-drawer [title]="action()?.title ?? ''" [open]="!!action()" [fields]="action()?.fields ?? noFields" [saving]="actionSaving()" [errors]="actionErrors()" testId="unit-action-form"
                       [width]="420" (submitted)="runAction($event)" (closed)="action.set(null)" />`,
})
export class UnitsPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(UnitsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly humanize = humanize;
  readonly statuses = STATUSES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'unitNumber' });
  readonly state = new MasterDataState<UnitDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Unit',
  });

  readonly columns: TableColumn<UnitListItem>[] = [
    { key: 'propertyName', label: 'Property' },
    { key: 'unitNumber', label: 'Unit', sortField: 'unitNumber', width: '100px' },
    { key: 'unitTypeName', label: 'Type' },
    { key: 'usage', label: 'Usage', sortField: 'usage', value: (u) => humanize(u.usage) },
    { key: 'floor', label: 'Floor' },
    { key: 'areaSqft', label: 'Area (sq ft)', sortField: 'areaSqft' },
    { key: 'askingRent', label: 'Asking rent', sortField: 'askingRent', value: (u) => u.askingRent.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: STATUSES },
    { key: 'tenantName', label: 'Tenant' },
    { key: 'currentRent', label: 'Current rent', value: (u) => u.currentRent?.toLocaleString() },
    { key: 'leaseEnds', label: 'Lease ends' },
    { key: 'daysVacant', label: 'Days vacant' },
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
      next: () => { this.actionSaving.set(false); this.action.set(null); this.state.close(); this.message.success('Unit updated'); this.list.reload(); },
      error: (e) => { this.actionSaving.set(false); const p = asProblem(e); this.actionErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not update the unit'); },
    });
  }
}
