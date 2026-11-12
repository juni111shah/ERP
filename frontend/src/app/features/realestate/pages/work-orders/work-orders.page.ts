import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { Observable, map } from 'rxjs';
import { BillsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { COST_RESPONSIBILITIES, PRIORITIES, RealEstateLookups, WorkOrderDto, WorkOrdersApi, humanize } from '@core/api/realestate.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';

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

/** The body of a status change. */
type StatusChange = { status: string; actualCost?: number | null; resolutionNotes?: string | null };

/** Maintenance work orders: the list, the lifecycle (assign, start, hold, complete, close), and who bears the cost. */
@Component({
  selector: 'erp-work-orders',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  templateUrl: './work-orders.page.html',
})
export class WorkOrdersPage {
  private readonly workOrdersApi = inject(WorkOrdersApi);
  private readonly billsApi = inject(BillsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly humanize = humanize;
  readonly statuses = Object.keys(NEXT);
  readonly list = createPagedList((q) => this.workOrdersApi.list(q), { sort: '-reportedOn' });
  readonly state = new MasterDataState<WorkOrderDto>({
    save: (id, body) => (id ? this.workOrdersApi.update(id, body) : this.workOrdersApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Work order',
  });
  readonly columns: TableColumn<WorkOrderDto>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'issue', label: 'Issue' },
    { key: 'category', label: 'Category' },
    { key: 'priority', label: 'Priority', sortField: 'priority', value: (w) => humanize(w.priority) },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: Object.keys(NEXT) },
    { key: 'reportedOn', label: 'Reported', sortField: 'reportedOn' },
    { key: 'technician', label: 'Technician' },
    { key: 'estimatedCost', label: 'Estimated', align: 'right', value: (w) => w.estimatedCost.toLocaleString() },
    { key: 'actualCost', label: 'Actual', align: 'right', value: (w) => w.actualCost?.toLocaleString() },
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
  readonly costBasisFor = signal<WorkOrderDto | null>(null);
  readonly costBasisFields: FieldSpec[] = [
    { key: 'chargeableTo', label: 'Chargeable to', type: 'select', options: COST_RESPONSIBILITIES, hint: 'Leave empty for the default: the owner of a managed property, otherwise the company.' },
    { key: 'ownerSharePercent', label: 'Owner share %', type: 'number', min: 0, max: 100, precision: 2, hint: 'Only for shared costs.', hidden: (v) => v['chargeableTo'] !== 'shared' },
    { key: 'billId', label: 'Vendor bill', type: 'lookup', hint: 'When linked, the actual cost is the net amount of the bill (split across the work orders linked to it).',
      lookup: (term: string) => this.billLookup(term), currentLabel: (r) => (r['billId'] ? 'Linked bill' : undefined) },
  ];
  readonly completing = signal<WorkOrderDto | null>(null);
  readonly moveSaving = signal(false);
  readonly moveErrors = signal<ProblemDetails | null>(null);

  openCostBasis(w: WorkOrderDto): void { this.moveErrors.set(null); this.costBasisFor.set(w); }

  saveCostBasis(values: Record<string, unknown>): void {
    const w = this.costBasisFor();
    if (!w) return;
    this.moveSaving.set(true);
    const chargeableTo = (values['chargeableTo'] as string | null) ?? null;
    this.workOrdersApi.setCostBasis(w.id, { chargeableTo, ownerSharePercent: chargeableTo === 'shared' ? (values['ownerSharePercent'] as number | null) : null, billId: (values['billId'] as string | null) ?? null }).subscribe({
      next: () => { this.moveSaving.set(false); this.costBasisFor.set(null); this.state.close(); this.message.success(`Cost basis saved for work order ${w.number ?? w.issue}`); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save the cost basis'); },
    });
  }

  nextStatuses(w: WorkOrderDto): string[] { return NEXT[w.status] ?? []; }
  permission(status: string): string { return PERMISSION[status] ?? 'realestate.workorders.edit'; }
  label(status: string): string { return STATUS_LABEL[status] ?? humanize(status); }
  filterStatus(status: string | null): void { this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []); }

  /** Moves the work order on; completing first asks for the actual cost and notes. */
  move(w: WorkOrderDto, status: string): void {
    if (status === 'completed') { this.moveErrors.set(null); this.completing.set(w); return; }
    this.changeStatus(w, { status });
  }

  complete(values: Record<string, unknown>): void {
    const w = this.completing();
    if (w) this.changeStatus(w, { status: 'completed', actualCost: values['actualCost'] as number | null, resolutionNotes: values['resolutionNotes'] as string | null });
  }

  /** Server-searched vendor bills, newest first, "BILL-001 · Vendor · 1,200 QAR (posted)". */
  private billLookup(term: string): Observable<LookupOption[]> {
    return this.billsApi.list({ page: 1, pageSize: 20, search: term, sort: '-billDate' })
      .pipe(map((r) => r.items.map((b) => ({ value: b.id, label: `${b.number} · ${b.partyName ?? ''} · ${b.total.toLocaleString()} ${b.currencyCode} (${b.status})` }))));
  }

  private changeStatus(w: WorkOrderDto, body: StatusChange): void {
    this.moveSaving.set(true);
    this.workOrdersApi.changeStatus(w.id, body).subscribe({
      next: () => { this.moveSaving.set(false); this.completing.set(null); this.state.close(); this.message.success(doneText('updated', 'Work order', w.number ?? w.issue)); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not update the work order'); },
    });
  }
}
