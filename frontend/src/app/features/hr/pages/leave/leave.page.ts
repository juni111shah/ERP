import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { EmployeeSummary, EmployeesApi, LEAVE_STATUS_LABELS, LeaveApi, LeaveRequestDto, LeaveRequestInput, LeaveRequestRow, LeaveTypeDto, LeaveTypesApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { fromIsoDate } from '@core/format/from-iso-date';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CanPipe } from '@shared/pipes/can.pipe';

/** Leave requests: draft, submit, then a manager approves or rejects; approved leave feeds attendance and payroll. */
@Component({
  selector: 'erp-leave',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, HasPermissionDirective, CanPipe, StatusTagComponent, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzIconModule, NzInputModule, NzSelectModule],
  styleUrl: './leave.page.scss',
  templateUrl: './leave.page.html',
})
export class LeavePage {
  private readonly leaveApi = inject(LeaveApi);
  private readonly employeesApi = inject(EmployeesApi);
  private readonly typesApi = inject(LeaveTypesApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly statuses = Object.entries(LEAVE_STATUS_LABELS).map(([value, label]) => ({ value, label }));
  readonly list = createPagedList((q) => this.leaveApi.list(q), { sort: '-fromDate' });
  readonly employees = signal<EmployeeSummary[]>([]);
  readonly types = signal<LeaveTypeDto[]>([]);
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
  /** The fields can be changed on a new or draft request, by someone who may request leave (saving needs hr.leave.request). */
  readonly editable = computed(() => this.session.hasPermission('hr.leave.request') && (!this.id() || this.detail()?.status === 'draft'));
  readonly canDecide = computed(() => this.detail()?.status === 'submitted');
  readonly canCancel = computed(() => ['draft', 'submitted', 'approved'].includes(this.detail()?.status ?? ''));
  employeeId: string | null = null;
  typeId: string | null = null;
  startHalf = false;
  endHalf = false;
  from: Date | null = null;
  to: Date | null = null;
  reason = '';
  comment = '';

  constructor() {
    this.employeesApi.list({ page: 1, pageSize: 200 }).subscribe((r) => this.employees.set(r.items));
    this.typesApi.list({ page: 1, pageSize: 100, filters: [{ field: 'isActive', op: 'eq', value: true }] }).subscribe((r) => this.types.set(r.items));
  }

  label(status: string): string { return LEAVE_STATUS_LABELS[status] ?? status; }
  filterStatus(status: string | null): void { this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []); }

  create(): void {
    this.id.set(null); this.detail.set(null); this.employeeId = null; this.typeId = this.types()[0]?.id ?? null; this.startHalf = false; this.endHalf = false; this.from = null; this.to = null; this.reason = ''; this.comment = ''; this.days.set(null);
    this.drawer.set(true);
  }

  open(id: string): void {
    this.leaveApi.detail(id).subscribe((d) => {
      this.id.set(d.id); this.detail.set(d); this.employeeId = d.employeeId; this.typeId = d.leaveTypeId; this.startHalf = !!d.startHalfDay; this.endHalf = !!d.endHalfDay; this.reason = d.reason ?? ''; this.comment = ''; this.days.set(d.days);
      this.from = fromIsoDate(d.fromDate); this.to = fromIsoDate(d.toDate);
      this.drawer.set(true);
    });
  }

  /** The working days between the picked dates (holidays and weekly offs left out). */
  preview(): void {
    if (!this.from || !this.to || this.to < this.from) { this.days.set(null); return; }
    this.leaveApi.days(toIsoDate(this.from), toIsoDate(this.to)).subscribe({ next: (r) => this.days.set(r.workingDays), error: () => this.days.set(null) });
  }

  save(submit: boolean): void {
    if (!this.employeeId || !this.typeId || !this.from || !this.to) { this.message.warning('Choose the employee, leave type and dates'); return; }
    const body: LeaveRequestInput = { employeeId: this.employeeId, leaveTypeId: this.typeId, fromDate: toIsoDate(this.from), toDate: toIsoDate(this.to), reason: this.reason || null, submit, startHalfDay: this.startHalf, endHalfDay: this.endHalf };
    this.saving.set(true);
    this.leaveApi.save(this.id(), body).subscribe({
      next: (d) => { this.saving.set(false); this.message.success(doneText(submit ? 'submitted' : 'saved', 'Leave request', d.employeeName)); this.list.reload(); if (submit) this.drawer.set(false); else { this.id.set(d.id); this.detail.set(d); } },
      error: (e) => this.fail(e, 'Could not save the request'),
    });
  }

  decide(action: 'approve' | 'reject'): void {
    const id = this.id();
    if (!id) return;
    this.saving.set(true);
    this.leaveApi.action(id, action, this.comment || undefined).subscribe({
      next: () => { this.saving.set(false); this.message.success(doneText(action === 'approve' ? 'approved' : 'rejected', 'Leave request', this.detail()?.employeeName)); this.drawer.set(false); this.list.reload(); },
      error: (e) => this.fail(e, 'Could not record the decision'),
    });
  }

  cancel(): void {
    const id = this.id();
    if (!id) return;
    this.saving.set(true);
    this.leaveApi.action(id, 'cancel').subscribe({
      next: () => { this.saving.set(false); this.message.success(doneText('cancelled', 'Leave request', this.detail()?.employeeName)); this.drawer.set(false); this.list.reload(); },
      error: (e) => this.fail(e, 'Could not cancel the request'),
    });
  }

  private fail(e: unknown, fallback: string): void { this.saving.set(false); this.message.error(problemMessage(e, fallback)); }
}
