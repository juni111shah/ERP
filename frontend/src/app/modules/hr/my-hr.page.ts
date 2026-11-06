import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '../../core/format/done-text';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { MyEmployee, MyHrApi, MyPayslip } from './hr-people.api';
import { LEAVE_STATUS_LABELS, LeaveApi, LeaveBalanceRow, LeaveRequestRow, MONTHS } from './hr.api';
import { ErpDatePipe } from '../../shared/display.pipes';

/** Self-service: the signed-in person's own profile, leave balances and requests, payslips, and (for line managers) the team's leave to decide. */
@Component({
  selector: 'erp-my-hr',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, RecordDrawerComponent, StatePanelComponent, StatusTagComponent, NzButtonModule, NzIconModule, NzSkeletonModule, NzTableModule, NzTabsModule, NzTagModule],
  styles: [`.num { text-align: end; white-space: nowrap; } .muted { color: var(--erp-color-text-secondary); } .gap { margin-inline-end: 10px; }`],
  template: `
    <h1 class="erp-page-title">My HR</h1>
    @if (notLinked()) {
      <div data-testid="my-not-linked">
        <erp-state-panel title="Your sign-in is not linked to an employee record" icon="user"
                         [hint]="canOpenEmployees ? 'My HR shows your own leave, payslips and profile, so it needs an employee record. Open your employee record and choose your sign-in under Sign-in account.' : 'My HR shows your own leave, payslips and profile, so it needs an employee record. Ask HR to choose your sign-in under Sign-in account on your employee record.'"
                         [actionLabel]="canOpenEmployees ? 'Open employees' : null" (action)="openEmployees()" />
      </div>
    } @else if (failed()) {
      <erp-state-panel kind="error" title="My HR could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="start()" />
    } @else if (me(); as m) {
      <p><b>{{ m.fullName }}</b> · {{ m.designation }} @if (m.department) { · {{ m.department }} } · {{ m.number }}
        @if (m.onLeaveToday) { <nz-tag nzColor="blue">On leave today</nz-tag> }</p>
      <p class="muted">Joined {{ m.joinDate | erpDate }} · {{ m.serviceYears | number: '1.1-1' }} years @if (m.managerName) { · Manager: {{ m.managerName }} }</p>
      <nz-tabs>
        <nz-tab nzTitle="Leave balances">
          <nz-table [nzData]="balances()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No leave balances for this year yet" data-testid="my-balances">
            <thead><tr><th>Type</th><th class="num">Entitled</th><th class="num">Carried</th><th class="num">Taken</th><th class="num">Pending</th><th class="num">Remaining</th></tr></thead>
            <tbody>@for (b of balances(); track b.leaveTypeId) { <tr><td>{{ b.leaveTypeName }}</td><td class="num">{{ b.entitled }}</td><td class="num">{{ b.carried }}</td><td class="num">{{ b.taken }}</td><td class="num">{{ b.pending }}</td><td class="num"><b>{{ b.remaining }}</b></td></tr> }</tbody>
          </nz-table>
        </nz-tab>
        <nz-tab nzTitle="My leave">
          <button nz-button nzType="primary" (click)="requesting.set(true)" data-testid="my-leave-new"><span nz-icon nzType="plus"></span>Request leave</button>
          <nz-table [nzData]="leave()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No leave requests yet" style="margin-top:12px" data-testid="my-leave">
            <thead><tr><th>From</th><th>To</th><th class="num">Days</th><th>Status</th><th>Reason</th><th></th></tr></thead>
            <tbody>
              @for (r of leave(); track r.id) {
                <tr>
                  <td>{{ r.fromDate | erpDate }}</td><td>{{ r.toDate | erpDate }}</td><td class="num">{{ r.days }}</td>
                  <td><erp-status [value]="r.status" [label]="statusLabel(r.status)" /></td><td>{{ r.reason }}</td>
                  <td>
                    @if (r.status === 'draft') { <a class="gap" (click)="act(r, 'submit')">Submit</a> }
                    @if (r.status === 'draft' || r.status === 'submitted' || r.status === 'approved') { <a (click)="act(r, 'cancel')">Cancel</a> }
                  </td>
                </tr>
              }
            </tbody>
          </nz-table>
        </nz-tab>
        <nz-tab nzTitle="Payslips">
          <nz-table [nzData]="payslips()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No payslips yet" data-testid="my-payslips">
            <thead><tr><th>Month</th><th class="num">Gross ({{ currency() }})</th><th class="num">Deductions ({{ currency() }})</th><th class="num">Net pay ({{ currency() }})</th><th>Status</th></tr></thead>
            <tbody>@for (p of payslips(); track p.lineId) { <tr><td>{{ month(p.month) }} {{ p.year }}</td><td class="num">{{ p.grossEarnings | number: '1.2-2' }}</td><td class="num">{{ p.deductions | number: '1.2-2' }}</td><td class="num"><b>{{ p.netPay | number: '1.2-2' }}</b></td><td><erp-status [value]="p.status" /></td></tr> }
            </tbody>
          </nz-table>
        </nz-tab>
        @if (team().length || isManager()) {
          <nz-tab nzTitle="Team approvals">
            <nz-table [nzData]="team()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="Nothing waiting for your decision" data-testid="my-team">
              <thead><tr><th>Employee</th><th>From</th><th>To</th><th class="num">Days</th><th>Reason</th><th></th></tr></thead>
              <tbody>
                @for (r of team(); track r.id) {
                  <tr>
                    <td>{{ r.employeeName }}</td><td>{{ r.fromDate | erpDate }}</td><td>{{ r.toDate | erpDate }}</td><td class="num">{{ r.days }}</td><td>{{ r.reason }}</td>
                    <td><a class="gap" (click)="decide(r, 'approve')" data-testid="team-approve">Approve</a><a (click)="decide(r, 'reject')" data-testid="team-reject">Reject</a></td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </nz-tab>
        }
      </nz-tabs>
      <erp-record-drawer title="Request leave" [open]="requesting()" [fields]="fields()" [saving]="saving()" [errors]="errors()" [width]="440" testId="my-leave-form" submitLabel="Send request"
                         (submitted)="request($event)" (closed)="requesting.set(false)" />
    } @else { <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 6 }"></nz-skeleton> }`,
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
