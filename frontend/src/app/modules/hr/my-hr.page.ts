import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { MyEmployee, MyHrApi, MyPayslip } from './hr-people.api';
import { LEAVE_STATUS_LABELS, LeaveApi, LeaveBalanceRow, LeaveRequestRow, MONTHS } from './hr.api';

const STATUS_COLORS: Record<string, string> = { draft: 'default', submitted: 'blue', approved: 'green', rejected: 'red', cancelled: 'default' };

/** Self-service: the signed-in person's own profile, leave balances and requests, payslips, and (for line managers) the team's leave to decide. */
@Component({
  selector: 'erp-my-hr',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RecordDrawerComponent, NzButtonModule, NzIconModule, NzTableModule, NzTabsModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">My HR</h1>
    @if (notLinked()) {
      <p data-testid="my-not-linked">Your sign-in is not linked to an employee record yet. Ask HR to link it on your employee profile.</p>
    } @else if (me(); as m) {
      <p><b>{{ m.fullName }}</b> · {{ m.designation }} @if (m.department) { · {{ m.department }} } · {{ m.number }}
        @if (m.onLeaveToday) { <nz-tag nzColor="blue">On leave today</nz-tag> }</p>
      <p style="color:var(--erp-color-text-secondary)">Joined {{ m.joinDate }} · {{ m.serviceYears | number: '1.1-1' }} years @if (m.managerName) { · Manager: {{ m.managerName }} }</p>
      <nz-tabs>
        <nz-tab nzTitle="Leave balances">
          <nz-table [nzData]="balances()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="my-balances">
            <thead><tr><th>Type</th><th>Entitled</th><th>Carried</th><th>Taken</th><th>Pending</th><th>Remaining</th></tr></thead>
            <tbody>@for (b of balances(); track b.leaveTypeId) { <tr><td>{{ b.leaveTypeName }}</td><td>{{ b.entitled }}</td><td>{{ b.carried }}</td><td>{{ b.taken }}</td><td>{{ b.pending }}</td><td><b>{{ b.remaining }}</b></td></tr> }</tbody>
          </nz-table>
        </nz-tab>
        <nz-tab nzTitle="My leave">
          <button nz-button nzType="primary" (click)="requesting.set(true)" data-testid="my-leave-new"><span nz-icon nzType="plus"></span>Request leave</button>
          <nz-table [nzData]="leave()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" style="margin-top:12px" data-testid="my-leave">
            <thead><tr><th>From</th><th>To</th><th>Days</th><th>Status</th><th>Reason</th><th></th></tr></thead>
            <tbody>
              @for (r of leave(); track r.id) {
                <tr>
                  <td>{{ r.fromDate }}</td><td>{{ r.toDate }}</td><td>{{ r.days }}</td>
                  <td><nz-tag [nzColor]="color(r.status)">{{ statusLabel(r.status) }}</nz-tag></td><td>{{ r.reason }}</td>
                  <td>
                    @if (r.status === 'draft') { <a (click)="act(r, 'submit')" style="margin-inline-end:10px">Submit</a> }
                    @if (r.status === 'draft' || r.status === 'submitted' || r.status === 'approved') { <a (click)="act(r, 'cancel')">Cancel</a> }
                  </td>
                </tr>
              } @empty { <tr><td colspan="6" style="text-align:center;color:var(--erp-color-text-secondary)">No leave requests yet</td></tr> }
            </tbody>
          </nz-table>
        </nz-tab>
        <nz-tab nzTitle="Payslips">
          <nz-table [nzData]="payslips()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="my-payslips">
            <thead><tr><th>Month</th><th>Gross</th><th>Deductions</th><th>Net pay</th><th>Status</th></tr></thead>
            <tbody>@for (p of payslips(); track p.lineId) { <tr><td>{{ month(p.month) }} {{ p.year }}</td><td>{{ p.grossEarnings | number: '1.2-2' }}</td><td>{{ p.deductions | number: '1.2-2' }}</td><td><b>{{ p.netPay | number: '1.2-2' }}</b></td><td>{{ p.status }}</td></tr> }
              @empty { <tr><td colspan="5" style="text-align:center;color:var(--erp-color-text-secondary)">No payslips yet</td></tr> }
            </tbody>
          </nz-table>
        </nz-tab>
        @if (team().length || isManager()) {
          <nz-tab nzTitle="Team approvals">
            <nz-table [nzData]="team()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="my-team">
              <thead><tr><th>Employee</th><th>From</th><th>To</th><th>Days</th><th>Reason</th><th></th></tr></thead>
              <tbody>
                @for (r of team(); track r.id) {
                  <tr>
                    <td>{{ r.employeeName }}</td><td>{{ r.fromDate }}</td><td>{{ r.toDate }}</td><td>{{ r.days }}</td><td>{{ r.reason }}</td>
                    <td><a (click)="decide(r, 'approve')" style="margin-inline-end:10px" data-testid="team-approve">Approve</a><a (click)="decide(r, 'reject')" data-testid="team-reject">Reject</a></td>
                  </tr>
                } @empty { <tr><td colspan="6" style="text-align:center;color:var(--erp-color-text-secondary)">Nothing waiting for your decision</td></tr> }
              </tbody>
            </nz-table>
          </nz-tab>
        }
      </nz-tabs>
      <erp-record-drawer title="Request leave" [open]="requesting()" [fields]="fields()" [saving]="saving()" [errors]="errors()" [width]="440" testId="my-leave-form" submitLabel="Send request"
                         (submitted)="request($event)" (closed)="requesting.set(false)" />
    } @else { <p>Loading…</p> }`,
})
export class MyHrPage {
  private readonly api = inject(MyHrApi);
  private readonly leaveApi = inject(LeaveApi);
  private readonly message = inject(NzMessageService);
  readonly me = signal<MyEmployee | null>(null);
  readonly notLinked = signal(false);
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

  constructor() {
    this.api.me().subscribe({
      next: (m) => { this.me.set(m); this.loadAll(); },
      error: () => this.notLinked.set(true),
    });
  }

  color(s: string): string { return STATUS_COLORS[s] ?? 'default'; }
  statusLabel(s: string): string { return LEAVE_STATUS_LABELS[s] ?? s; }
  month(m: number): string { return MONTHS[m - 1]; }

  request(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    this.api.requestLeave({ leaveTypeId: v['leaveTypeId'] as string, fromDate: String(v['fromDate']).slice(0, 10), toDate: String(v['toDate']).slice(0, 10), reason: (v['reason'] as string) || null, submit: true }).subscribe({
      next: () => { this.saving.set(false); this.requesting.set(false); this.message.success('Leave request sent'); this.loadAll(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not send the request'); },
    });
  }

  act(r: LeaveRequestRow, what: 'submit' | 'cancel'): void {
    (what === 'submit' ? this.api.submitLeave(r.id) : this.api.cancelLeave(r.id)).subscribe({
      next: () => { this.message.success(what === 'submit' ? 'Submitted' : 'Cancelled'); this.loadAll(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Action failed'),
    });
  }

  decide(r: LeaveRequestRow, what: 'approve' | 'reject'): void {
    this.leaveApi.action(r.id, what).subscribe({
      next: () => { this.message.success(what === 'approve' ? 'Approved' : 'Rejected'); this.loadAll(); },
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
