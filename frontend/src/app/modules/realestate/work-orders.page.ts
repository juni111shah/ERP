import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { PRIORITIES, RealEstateLookups, WorkOrderDto, WorkOrdersApi, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';

/** Next statuses per status (mirrors the backend lifecycle) and the permission each target needs. */
const NEXT: Record<string, string[]> = {
  new: ['assigned', 'inProgress', 'cancelled'],
  assigned: ['inProgress', 'onHold', 'cancelled'],
  inProgress: ['onHold', 'completed', 'cancelled'],
  onHold: ['inProgress', 'assigned', 'cancelled'],
  completed: ['closed', 'inProgress'],
  closed: [],
  cancelled: [],
};
const PERMISSION: Record<string, string> = {
  assigned: 'realestate.workorders.assign', completed: 'realestate.workorders.complete', closed: 'realestate.workorders.close', cancelled: 'realestate.workorders.cancel',
};
const STATUS_LABEL: Record<string, string> = { assigned: 'Assign', inProgress: 'Start work', onHold: 'Put on hold', completed: 'Complete', closed: 'Close', cancelled: 'Cancel' };

@Component({
  selector: 'erp-work-orders',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Work orders</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search issue or category" emptyText="No work orders yet" (rowClick)="state.edit($event)">
      <nz-select toolbar style="width:170px" nzAllowClear nzPlaceHolder="All statuses" [ngModel]="null" (ngModelChange)="filterStatus($event)" data-testid="wo-status-filter">
        @for (s of statuses; track s) { <nz-option [nzValue]="s" [nzLabel]="humanize(s)"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'realestate.workorders.create'" nz-button nzType="primary" (click)="state.create()" data-testid="wo-new"><span nz-icon nzType="plus"></span>New work order</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>

    <erp-record-drawer [title]="state.editing() ? (state.editing()!.number ?? 'Work order') : 'New work order'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       customFieldEntityType="realestate.workorder" [saving]="state.saving()" [errors]="state.errors()" testId="wo-form"
                       (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as w) {
        <div drawerActions style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:16px" data-testid="wo-actions">
          <erp-status-tag [status]="w.status" />
          @for (next of nextStatuses(w); track next) {
            <button *erpHasPermission="permission(next)" nz-button nzSize="small" [nzDanger]="next === 'cancelled'" (click)="move(w, next)" [attr.data-testid]="'wo-' + next">{{ label(next) }}</button>
          }
        </div>
      }
    </erp-record-drawer>
    <erp-record-drawer title="Complete work order" [open]="completing() !== null" [fields]="completeFields" [saving]="moveSaving()" [errors]="moveErrors()" [width]="420" testId="wo-complete-form"
                       submitLabel="Complete" (submitted)="complete($event)" (closed)="completing.set(null)" />`,
})
export class WorkOrdersPage {
  private readonly api = inject(WorkOrdersApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly humanize = humanize;
  readonly statuses = Object.keys(NEXT);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-reportedOn' });
  readonly state = new MasterDataState<WorkOrderDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Work order',
  });
  readonly columns: TableColumn<WorkOrderDto>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'issue', label: 'Issue' },
    { key: 'category', label: 'Category', sortField: 'category' },
    { key: 'priority', label: 'Priority', sortField: 'priority', value: (w) => humanize(w.priority) },
    { key: 'status', label: 'Status', sortField: 'status' },
    { key: 'reportedOn', label: 'Reported', sortField: 'reportedOn' },
    { key: 'technician', label: 'Technician' },
    { key: 'estimatedCost', label: 'Estimated', value: (w) => w.estimatedCost.toLocaleString() },
    { key: 'actualCost', label: 'Actual', value: (w) => w.actualCost?.toLocaleString() },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'propertyId', label: 'Property', type: 'lookup', lookup: this.lookups.property, required: true, readonlyOnEdit: true },
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: this.lookups.unit, hint: 'Leave blank for a common area.' },
    { key: 'issue', label: 'Issue', type: 'text', required: true, maxLength: 300 },
    { key: 'category', label: 'Category', type: 'text', maxLength: 50, hint: 'For example HVAC, Plumbing, Elevators.' },
    { key: 'priority', label: 'Priority', type: 'select', required: true, options: PRIORITIES, default: 'normal' },
    { key: 'reportedOn', label: 'Reported on', type: 'date' },
    { key: 'reportedBy', label: 'Reported by', type: 'text', maxLength: 100 },
    { key: 'vendorPartyId', label: 'Vendor', type: 'lookup', lookup: this.lookups.party('vendor') },
    { key: 'technician', label: 'Technician', type: 'text', maxLength: 100 },
    { key: 'scheduledFor', label: 'Scheduled for', type: 'date' },
    { key: 'estimatedCost', label: 'Estimated cost', type: 'number', min: 0, default: 0 },
    { key: 'details', label: 'Details', type: 'textarea', maxLength: 4000 },
  ];
  readonly completeFields: FieldSpec[] = [
    { key: 'actualCost', label: 'Actual cost', type: 'number', min: 0 },
    { key: 'resolutionNotes', label: 'Resolution notes', type: 'textarea', maxLength: 4000 },
  ];
  readonly completing = signal<WorkOrderDto | null>(null);
  readonly moveSaving = signal(false);
  readonly moveErrors = signal<ProblemDetails | null>(null);

  nextStatuses(w: WorkOrderDto): string[] { return NEXT[w.status] ?? []; }
  permission(status: string): string { return PERMISSION[status] ?? 'realestate.workorders.edit'; }
  label(status: string): string { return STATUS_LABEL[status] ?? humanize(status); }
  filterStatus(status: string | null): void { this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []); }

  move(w: WorkOrderDto, status: string): void {
    if (status === 'completed') { this.moveErrors.set(null); this.completing.set(w); return; }
    this.changeStatus(w, { status });
  }

  complete(values: Record<string, unknown>): void {
    const w = this.completing();
    if (w) this.changeStatus(w, { status: 'completed', actualCost: values['actualCost'] as number | null, resolutionNotes: values['resolutionNotes'] as string | null });
  }

  private changeStatus(w: WorkOrderDto, body: { status: string; actualCost?: number | null; resolutionNotes?: string | null }): void {
    this.moveSaving.set(true);
    this.api.changeStatus(w.id, body).subscribe({
      next: () => { this.moveSaving.set(false); this.completing.set(null); this.state.close(); this.message.success('Work order updated'); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not update the work order'); },
    });
  }
}
