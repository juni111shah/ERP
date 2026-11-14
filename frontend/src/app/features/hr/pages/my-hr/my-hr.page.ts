import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '@core/api/api.models';
import { MyEmployee, MyHrApi, MyPayslip } from '@core/api/hr-people.api';
import { LEAVE_STATUS_LABELS, LeaveApi, LeaveBalanceRow, LeaveRequestRow } from '@core/api/hr.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { dayText, monthName } from '../../hr-format';

/** Self-service: the signed-in person's own profile, leave balances and requests, payslips, and (for line managers) the team's leave to decide. */
@Component({
  selector: 'erp-my-hr',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, RecordDrawerComponent, StatePanelComponent, StatusTagComponent, NzButtonModule, NzIconModule, NzSkeletonModule, NzTableModule, NzTabsModule, NzTagModule],
  styleUrl: './my-hr.page.scss',
  templateUrl: './my-hr.page.html',
})
export class MyHrPage {
  private readonly myHrApi = inject(MyHrApi);
  private readonly leaveApi = inject(LeaveApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly session = inject(SessionStore);
  readonly canOpenEmployees = computed(() => this.session.hasPermission('hr.employees.manage'));
  readonly me = signal<MyEmployee | null>(null);
  readonly notLinked = signal(false);
  readonly failed = signal(false);
  readonly balances = signal<LeaveBalanceRow[]>([]);
  readonly leave = signal<LeaveRequestRow[]>([]);
  readonly payslips = signal<MyPayslip[]>([]);
  readonly team = signal<LeaveRequestRow[]>([]);
  readonly isManager = signal(false);
  readonly requesting = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'leaveTypeId', label: 'Leave type', type: 'select', required: true, options: this.balances().map((b) => ({ value: b.leaveTypeId, label: `${b.leaveTypeName} (${b.remaining} left)` })) },
    { key: 'fromDate', label: 'From', type: 'date', required: true, default: new Date() },
    { key: 'toDate', label: 'To', type: 'date', required: true, default: new Date() },
    { key: 'reason', label: 'Reason', type: 'text', maxLength: 500 },
  ]);

  constructor() { this.start(); }

  /** The API answers 404 on purpose when the sign-in has no employee record. Anything else is a failed load. */
  start(): void {
    this.failed.set(false);
    this.myHrApi.me().subscribe({
      next: (m) => { this.me.set(m); this.loadAll(); },
      error: (e: { status?: number }) => { if (e.status === 404) this.notLinked.set(true); else this.failed.set(true); },
    });
  }

  openEmployees(): void { void this.router.navigateByUrl('/hr/employees'); }
  statusLabel(s: string): string { return LEAVE_STATUS_LABELS[s] ?? s; }
  month(m: number): string { return monthName(m); }

  request(v: Record<string, unknown>): void {
    const fromDate = dayText(v['fromDate']); const toDate = dayText(v['toDate']);
    this.saving.set(true); this.errors.set(null);
    this.myHrApi.requestLeave({ leaveTypeId: v['leaveTypeId'] as string, fromDate, toDate, reason: (v['reason'] as string) || null, submit: true }).subscribe({
      next: () => { this.saving.set(false); this.requesting.set(false); this.message.success(`Leave request sent for ${fromDate} to ${toDate}`); this.loadAll(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not send the request'); },
    });
  }

  act(r: LeaveRequestRow, what: 'submit' | 'cancel'): void {
    (what === 'submit' ? this.myHrApi.submitLeave(r.id) : this.myHrApi.cancelLeave(r.id)).subscribe({
      next: () => { this.message.success(doneText(what === 'submit' ? 'submitted' : 'cancelled', 'Leave request', `${dayText(r.fromDate)} to ${dayText(r.toDate)}`)); this.loadAll(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Action failed'),
    });
  }

  decide(r: LeaveRequestRow, what: 'approve' | 'reject'): void {
    this.leaveApi.action(r.id, what).subscribe({
      next: () => { this.message.success(doneText(what === 'approve' ? 'approved' : 'rejected', 'Leave request', r.employeeName)); this.loadAll(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Action failed'),
    });
  }

  private loadAll(): void {
    this.myHrApi.balances().subscribe((b) => this.balances.set(b));
    this.myHrApi.leave().subscribe((p) => this.leave.set(p.items));
    this.myHrApi.payslips().subscribe({ next: (p) => this.payslips.set(p), error: () => this.payslips.set([]) });
    this.myHrApi.teamLeave('submitted').subscribe({ next: (p) => { this.team.set(p.items); this.isManager.set(true); }, error: () => this.isManager.set(false) });
  }
}
