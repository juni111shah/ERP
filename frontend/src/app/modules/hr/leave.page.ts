import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { EmployeeSummary, EmployeesApi, LEAVE_STATUS_LABELS, LeaveApi, LeaveRequestDto, LeaveRequestRow, LeaveTypeDto, LeaveTypesApi } from './hr.api';

const TONE: Record<string, string> = { draft: 'default', submitted: 'blue', approved: 'green', rejected: 'red', cancelled: 'default' };

/** Leave requests: draft, submit, then a manager approves or rejects; approved leave feeds attendance and payroll. */
@Component({
  selector: 'erp-leave',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzIconModule, NzInputModule, NzSelectModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Leave requests</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search employee or reason" emptyText="No leave requests yet" (rowClick)="open($event.id)">
      <nz-select toolbar style="width:170px" nzAllowClear nzPlaceHolder="All statuses" [ngModel]="null" (ngModelChange)="filterStatus($event)" data-testid="leave-status-filter">
        @for (s of statuses; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'hr.leave.request'" nz-button nzType="primary" (click)="create()" data-testid="leave-new"><span nz-icon nzType="plus"></span>New request</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <nz-tag [nzColor]="tone(row.status)">{{ label(row.status) }}</nz-tag> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawer()" [nzTitle]="id() ? 'Leave request' : 'New leave request'" nzWidth="520" (nzOnClose)="drawer.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <div style="display:grid;gap:12px" data-testid="leave-form">
          <nz-select nzShowSearch nzPlaceHolder="Employee" [(ngModel)]="employeeId" [nzDisabled]="!editable()" data-testid="leave-employee">@for (e of employees(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.number + ' · ' + e.fullName"></nz-option> }</nz-select>
          <nz-select nzPlaceHolder="Leave type" [(ngModel)]="typeId" [nzDisabled]="!editable()" data-testid="leave-type">@for (t of types(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + (t.isPaid ? '' : ' (unpaid)')"></nz-option> }</nz-select>
          <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px">
            <nz-date-picker [(ngModel)]="from" (ngModelChange)="preview()" [nzDisabled]="!editable()" nzPlaceHolder="From" data-testid="leave-from"></nz-date-picker>
            <nz-date-picker [(ngModel)]="to" (ngModelChange)="preview()" [nzDisabled]="!editable()" nzPlaceHolder="To" data-testid="leave-to"></nz-date-picker>
          </div>
          @if (days() !== null) { <div data-testid="leave-days"><b>{{ days() }}</b> working day(s) (weekends and public holidays are not counted)</div> }
          <textarea aria-label="Reason" nz-input rows="3" placeholder="Reason" [(ngModel)]="reason" [disabled]="!editable()" maxlength="500" data-testid="leave-reason"></textarea>
          @if (detail(); as d) {
            <div><nz-tag [nzColor]="tone(d.status)">{{ label(d.status) }}</nz-tag>
              @if (d.decisionComment) { <span>{{ d.decisionComment }}</span> }</div>
          }
          @if (canDecide()) { <input aria-label="Comment for the decision (optional)" nz-input placeholder="Comment for the decision (optional)" [(ngModel)]="comment" maxlength="500" data-testid="leave-comment" /> }
        </div>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <div style="display:flex;gap:8px;flex-wrap:wrap">
        @if (editable()) {
          <button *erpHasPermission="'hr.leave.request'" nz-button (click)="save(false)" [nzLoading]="saving()" data-testid="leave-save">Save draft</button>
          <button *erpHasPermission="'hr.leave.request'" nz-button nzType="primary" (click)="save(true)" [nzLoading]="saving()" data-testid="leave-submit">Submit</button>
        }
        @if (canDecide()) {
          <button *erpHasPermission="'hr.leave.approve'" nz-button nzType="primary" (click)="decide('approve')" [nzLoading]="saving()" data-testid="leave-approve">Approve</button>
          <button *erpHasPermission="'hr.leave.approve'" nz-button nzDanger (click)="decide('reject')" [nzLoading]="saving()" data-testid="leave-reject">Reject</button>
        }
        @if (canCancel()) { <button *erpHasPermission="'hr.leave.request'" nz-button nzDanger (click)="cancel()" [nzLoading]="saving()" data-testid="leave-cancel">Cancel request</button> }
      </div>
    </ng-template>`,
})
export class LeavePage {
  private readonly api = inject(LeaveApi);
  private readonly employeesApi = inject(EmployeesApi);
  private readonly typesApi = inject(LeaveTypesApi);
  private readonly message = inject(NzMessageService);
  readonly statuses = Object.entries(LEAVE_STATUS_LABELS).map(([value, label]) => ({ value, label }));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-fromDate' });
  readonly employees = signal<EmployeeSummary[]>([]);
  readonly types = signal<LeaveTypeDto[]>([]);
  private readonly employeeNames = computed(() => new Map(this.employees().map((e) => [e.id, e.fullName])));
  private readonly typeNames = computed(() => new Map(this.types().map((t) => [t.id, t.name])));
  readonly columns: TableColumn<LeaveRequestRow>[] = [
    { key: 'employeeName', label: 'Employee' },
    { key: 'leaveTypeId', label: 'Leave type', value: (r) => this.typeNames().get(r.leaveTypeId) ?? '', width: '150px' },
    { key: 'fromDate', label: 'From', sortField: 'fromDate', width: '120px' },
    { key: 'toDate', label: 'To', sortField: 'toDate', width: '120px' },
    { key: 'days', label: 'Days', width: '80px' },
    { key: 'status', label: 'Status', width: '120px' },
  ];

  readonly drawer = signal(false);
  readonly id = signal<string | null>(null);
  readonly detail = signal<LeaveRequestDto | null>(null);
  readonly saving = signal(false);
  readonly days = signal<number | null>(null);
  readonly editable = computed(() => !this.id() || this.detail()?.status === 'draft');
  readonly canDecide = computed(() => this.detail()?.status === 'submitted');
  readonly canCancel = computed(() => ['draft', 'submitted', 'approved'].includes(this.detail()?.status ?? ''));
  employeeId: string | null = null;
  typeId: string | null = null;
  from: Date | null = null;
  to: Date | null = null;
  reason = '';
  comment = '';

  constructor() {
    this.employeesApi.list({ page: 1, pageSize: 200 }).subscribe((r) => this.employees.set(r.items));
    this.typesApi.list({ page: 1, pageSize: 100, filters: [{ field: 'isActive', op: 'eq', value: true }] }).subscribe((r) => this.types.set(r.items));
  }

  tone(status: string): string { return TONE[status] ?? 'default'; }
  label(status: string): string { return LEAVE_STATUS_LABELS[status] ?? status; }
  filterStatus(status: string | null): void { this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []); }

  create(): void {
    this.id.set(null); this.detail.set(null); this.employeeId = null; this.typeId = this.types()[0]?.id ?? null; this.from = null; this.to = null; this.reason = ''; this.comment = ''; this.days.set(null);
    this.drawer.set(true);
  }

  open(id: string): void {
    this.api.detail(id).subscribe((d) => {
      this.id.set(d.id); this.detail.set(d); this.employeeId = d.employeeId; this.typeId = d.leaveTypeId; this.reason = d.reason ?? ''; this.comment = ''; this.days.set(d.days);
      this.from = this.parse(d.fromDate); this.to = this.parse(d.toDate);
      this.drawer.set(true);
    });
  }

  private parse(iso: string): Date { const [y, m, d] = iso.split('-').map(Number); return new Date(y, m - 1, d); }

  preview(): void {
    if (!this.from || !this.to || this.to < this.from) { this.days.set(null); return; }
    this.api.days(toIsoDate(this.from), toIsoDate(this.to)).subscribe({ next: (r) => this.days.set(r.workingDays), error: () => this.days.set(null) });
  }

  private fail(e: unknown, fallback: string): void { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? fallback); }

  save(submit: boolean): void {
    if (!this.employeeId || !this.typeId || !this.from || !this.to) { this.message.warning('Choose the employee, leave type and dates'); return; }
    const body = { employeeId: this.employeeId, leaveTypeId: this.typeId, fromDate: toIsoDate(this.from), toDate: toIsoDate(this.to), reason: this.reason || null, submit };
    this.saving.set(true);
    this.api.save(this.id(), body).subscribe({
      next: (d) => { this.saving.set(false); this.message.success(submit ? 'Request submitted' : 'Draft saved'); this.list.reload(); if (submit) this.drawer.set(false); else { this.id.set(d.id); this.detail.set(d); } },
      error: (e) => this.fail(e, 'Could not save the request'),
    });
  }

  decide(action: 'approve' | 'reject'): void {
    const id = this.id();
    if (!id) return;
    this.saving.set(true);
    this.api.action(id, action, this.comment || undefined).subscribe({
      next: () => { this.saving.set(false); this.message.success(action === 'approve' ? 'Request approved' : 'Request rejected'); this.drawer.set(false); this.list.reload(); },
      error: (e) => this.fail(e, 'Could not record the decision'),
    });
  }

  cancel(): void {
    const id = this.id();
    if (!id) return;
    this.saving.set(true);
    this.api.action(id, 'cancel').subscribe({
      next: () => { this.saving.set(false); this.message.success('Request cancelled'); this.drawer.set(false); this.list.reload(); },
      error: (e) => this.fail(e, 'Could not cancel the request'),
    });
  }
}
