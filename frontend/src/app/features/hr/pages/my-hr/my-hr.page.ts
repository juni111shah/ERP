import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '@core/format/done-text';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '@core/api/api.models';
import { asProblem } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { MyEmployee, MyHrApi, MyPayslip } from '@core/api/hr-people.api';
import { LEAVE_STATUS_LABELS, LeaveApi, LeaveBalanceRow, LeaveRequestRow, MONTHS } from '@core/api/hr.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

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
  private readonly api = inject(MyHrApi);
  private readonly leaveApi = inject(LeaveApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly canOpenEmployees = inject(SessionStore).hasPermission('hr.employees.manage');
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
    this.api.me().subscribe({
      next: (m) => { this.me.set(m); this.loadAll(); },
      error: (e: { status?: number }) => { if (e.status === 404) this.notLinked.set(true); else this.failed.set(true); },
    });
  }

  openEmployees(): void { void this.router.navigateByUrl('/hr/employees'); }
  statusLabel(s: string): string { return LEAVE_STATUS_LABELS[s] ?? s; }
  month(m: number): string { return MONTHS[m - 1]; }

  request(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    this.api.requestLeave({ leaveTypeId: v['leaveTypeId'] as string, fromDate: String(v['fromDate']).slice(0, 10), toDate: String(v['toDate']).slice(0, 10), reason: (v['reason'] as string) || null, submit: true }).subscribe({
      next: () => { this.saving.set(false); this.requesting.set(false); this.message.success(`Leave request sent for ${String(v['fromDate']).slice(0, 10)} to ${String(v['toDate']).slice(0, 10)}`); this.loadAll(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not send the request'); },
    });
  }

  act(r: LeaveRequestRow, what: 'submit' | 'cancel'): void {
    (what === 'submit' ? this.api.submitLeave(r.id) : this.api.cancelLeave(r.id)).subscribe({
      next: () => { this.message.success(doneText(what === 'submit' ? 'submitted' : 'cancelled', 'Leave request', `${r.fromDate.slice(0, 10)} to ${r.toDate.slice(0, 10)}`)); this.loadAll(); },
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
    this.api.balances().subscribe((b) => this.balances.set(b));
    this.api.leave().subscribe((p) => this.leave.set(p.items));
    this.api.payslips().subscribe({ next: (p) => this.payslips.set(p), error: () => this.payslips.set([]) });
    this.api.teamLeave('submitted').subscribe({ next: (p) => { this.team.set(p.items); this.isManager.set(true); }, error: () => this.isManager.set(false) });
  }
}
