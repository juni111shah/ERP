import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { MONTHS, PayrollApi, PayrollDto, PayrollLine } from './hr.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

type Mode = 'pay' | 'adjust' | 'void' | null;

const STATUS_COLORS: Record<string, string> = { draft: 'default', pendingApproval: 'purple', posted: 'blue', partiallyPaid: 'gold', paid: 'green', voided: 'red' };
const STATUS_LABELS: Record<string, string> = { pendingApproval: 'Needs approval', partiallyPaid: 'Partly paid' };

/** One payroll run: totals, the employee lines (adjust a line for one-offs while draft), post, pay (all or selected employees), void, payslips and the bank files. */
@Component({
  selector: 'erp-payroll-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzCardModule, NzIconModule, NzModalModule, NzTableModule, NzTagModule],
  template: `
    @if (run(); as r) {
      <p><a routerLink="/hr/payroll">← Payroll</a></p>
      <h1 class="erp-page-title" data-testid="payroll-title">{{ 'ui.hr.payroll' | translate: 'Payroll' }} {{ period() }}
        <nz-tag [nzColor]="statusColor(r.status)">{{ statusLabel(r.status) }}</nz-tag>
        @if (r.kind === 'offCycle') { <nz-tag nzColor="orange">Off-cycle</nz-tag> }
        @if (r.kind === 'finalSettlement') { <nz-tag nzColor="magenta">Final settlement</nz-tag> }</h1>
      <p>{{ r.number }}@if (r.notes) { · {{ r.notes }} }</p>
      @if (r.status === 'voided') { <p style="color:#cf1322" data-testid="payroll-void-reason">Voided{{ r.voidedAt ? ' on ' + r.voidedAt.slice(0, 10) : '' }}: {{ r.voidReason }}</p> }
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin:12px 0" data-testid="payroll-actions">
        @if (r.status === 'draft' || r.status === 'pendingApproval') {
          <button *erpHasPermission="'hr.payroll.manage'" nz-button [disabled]="busy()" (click)="recalculate()" data-testid="payroll-recalc">Recalculate</button>
          <button *erpHasPermission="'hr.payroll.post'" nz-button nzType="primary" [disabled]="busy()" (click)="post()" data-testid="payroll-post">{{ r.status === 'pendingApproval' ? 'Post (after approval)' : 'Submit and post' }}</button>
          <button *erpHasPermission="'hr.payroll.manage'" nz-button nzDanger [disabled]="busy()" (click)="remove()" data-testid="payroll-delete">Delete</button>
        }
        @if (r.status === 'posted' || r.status === 'partiallyPaid') {
          <button *erpHasPermission="'hr.payroll.pay'" nz-button nzType="primary" (click)="mode.set('pay')" data-testid="payroll-pay">{{ selected().size ? 'Pay ' + selected().size + ' selected' : 'Pay salaries' }}</button>
        }
        @if (r.status === 'posted' || r.status === 'partiallyPaid' || r.status === 'paid') {
          <button *erpHasPermission="'hr.payroll.pay'" nz-button (click)="download(r.status === 'partiallyPaid')" data-testid="payroll-bankfile">Bank file (CSV)</button>
          <button *erpHasPermission="'hr.payroll.pay'" nz-button (click)="downloadWps(r.status === 'partiallyPaid')" data-testid="payroll-wps">WPS file (SIF)</button>
          <button *erpHasPermission="'hr.payroll.read'" nz-button (click)="emailAll()" [disabled]="busy()" data-testid="payroll-email-all"><span nz-icon nzType="mail"></span>Email payslips</button>
          <button *erpHasPermission="'hr.payroll.post'" nz-button nzDanger (click)="startVoid()" data-testid="payroll-void">Void</button>
        }
        <a [href]="'/print/payroll/' + r.id" target="_blank" nz-button data-testid="payroll-print"><span nz-icon nzType="printer"></span>Print Summary</a>
      </div>
      <div style="display:flex;gap:16px;flex-wrap:wrap;margin-bottom:16px">
        <nz-card><small>Employees</small><div><b>{{ r.employees }}</b></div></nz-card>
        <nz-card><small>Gross earnings</small><div><b>{{ r.grossEarnings | number: '1.2-2' }}</b></div><small>after absence, incl. overtime</small></nz-card>
        <nz-card><small>Deductions</small><div><b>{{ r.deductions | number: '1.2-2' }}</b></div><small>incl. loan recoveries</small></nz-card>
        <nz-card><small>Net pay</small><div><b data-testid="payroll-net">{{ r.netPay | number: '1.2-2' }}</b></div>@if (r.paidOn) { <small>{{ 'ui.hr.paidOn' | translate: 'paid on' }} {{ r.paidOn }}</small> }</nz-card>
      </div>
      <nz-table [nzData]="r.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="payroll-lines">
        <thead><tr>
          @if (canSelect()) { <th style="width:36px"></th> }
          <th>Employee</th><th>Days</th><th>Absent</th><th>OT hrs</th><th>Basic</th><th>Allowances</th><th>Overtime</th><th>Absence</th><th>Adjustment</th>
          @if (hasSettlement()) { <th>Leave payout</th><th>End of service</th> }
          <th>Gross earnings</th><th>Deductions</th>
          @if (hasLoans()) { <th>of which loans</th> }
          <th>Net pay</th><th>Paid</th><th>Actions</th></tr></thead>
        <tbody>
          @for (l of r.lines; track l.id) {
            <tr>
              @if (canSelect()) { <td>@if (!l.paidOn) { <input type="checkbox" [checked]="selected().has(l.employeeId)" (change)="toggle(l.employeeId)" data-testid="payroll-select" /> }</td> }
              <td>{{ l.employeeName }}<div style="font-size:12px;color:var(--erp-color-text-secondary)">{{ l.employeeNumber }}</div></td>
              <td>{{ l.proratedDays || '' }}</td><td>{{ l.absentDays }}</td><td>{{ l.overtimeHours }}</td><td>{{ l.basic | number: '1.2-2' }}</td><td>{{ l.allowances | number: '1.2-2' }}</td><td>{{ l.overtime | number: '1.2-2' }}</td>
              <td>{{ l.absence | number: '1.2-2' }}</td><td>{{ l.adjustment | number: '1.2-2' }}</td>
              @if (hasSettlement()) { <td>{{ l.leaveEncashment | number: '1.2-2' }}</td><td>{{ l.endOfService | number: '1.2-2' }}</td> }
              <td>{{ l.grossEarnings | number: '1.2-2' }}</td><td>{{ l.deductions | number: '1.2-2' }}</td>
              @if (hasLoans()) { <td>{{ l.loanRecovery | number: '1.2-2' }}</td> }
              <td><b>{{ l.netPay | number: '1.2-2' }}</b></td>
              <td>{{ l.paidOn ?? '' }}</td>
              <td>
                <a [href]="'/print/payslip/' + r.id + '?employeeId=' + l.employeeId" target="_blank" data-testid="payroll-payslip" style="margin-inline-end:8px">Payslip</a>
                @if (r.status === 'posted' || r.status === 'partiallyPaid' || r.status === 'paid') { <a (click)="emailOne(l)" style="margin-inline-end:8px" data-testid="payroll-email-one">Email</a> }
                @if (r.status === 'draft' || r.status === 'pendingApproval') { <a *erpHasPermission="'hr.payroll.manage'" (click)="startAdjust(l)" data-testid="payroll-adjust">Adjust</a> }
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
      <erp-record-drawer [title]="title()" [open]="mode() !== null" [fields]="fields()" [saving]="busy()" [errors]="errors()" [width]="440"
                         testId="payroll-action-form" [submitLabel]="mode() === 'pay' ? 'Pay' : mode() === 'void' ? 'Void payroll' : 'Save'" (submitted)="submit($event)" (closed)="mode.set(null)" />
    }`,
})
export class PayrollDetailPage {
  private readonly api = inject(PayrollApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly run = signal<PayrollDto | null>(null);
  readonly busy = signal(false);
  readonly mode = signal<Mode>(null);
  readonly line = signal<PayrollLine | null>(null);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly errors = signal<ProblemDetails | null>(null);
  readonly period = computed(() => { const r = this.run(); return r ? `${MONTHS[r.month - 1]} ${r.year}` : ''; });
  readonly canSelect = computed(() => { const r = this.run(); return !!r && (r.status === 'posted' || r.status === 'partiallyPaid') && r.lines.some((l) => !l.paidOn); });
  readonly hasLoans = computed(() => !!this.run()?.lines.some((l) => (l.loanRecovery ?? 0) > 0));
  readonly hasSettlement = computed(() => this.run()?.kind === 'finalSettlement');
  readonly title = computed(() => this.mode() === 'pay' ? 'Pay salaries' : this.mode() === 'void' ? 'Void this payroll' : 'Adjust ' + (this.line()?.employeeName ?? ''));
  readonly fields = computed<FieldSpec[]>(() => {
    if (this.mode() === 'pay') {
      return [
        { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
        { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date(), hint: this.selected().size ? `Pays the ${this.selected().size} selected employee(s) only.` : 'Pays every employee not paid yet.' },
      ];
    }
    if (this.mode() === 'void') {
      return [{ key: 'reason', label: 'Reason', type: 'textarea', required: true, maxLength: 500, hint: 'The accrual and any payments are reversed in the ledger. The month can then be run again.' }];
    }
    return [
      { key: 'adjustment', label: 'One-off amount', type: 'number', required: true, default: this.line()?.adjustment ?? 0, hint: 'Positive adds to earnings, negative takes off.' },
      { key: 'note', label: 'Note', type: 'text', maxLength: 300, default: this.line()?.adjustmentNote ?? '' },
    ];
  });

  constructor() { this.load(); }

  statusColor(s: string): string { return STATUS_COLORS[s] ?? 'default'; }
  statusLabel(s: string): string { return STATUS_LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1); }
  toggle(employeeId: string): void { const next = new Set(this.selected()); if (!next.delete(employeeId)) next.add(employeeId); this.selected.set(next); }
  startAdjust(l: PayrollLine): void { this.errors.set(null); this.line.set(l); this.mode.set('adjust'); }
  startVoid(): void { this.errors.set(null); this.mode.set('void'); }

  recalculate(): void { this.run$(this.api.recalculate(this.id), 'Recalculated'); }
  post(): void {
    this.modal.confirm({ nzTitle: 'Post this payroll?', nzContent: 'The accrual is posted to the ledger and the run can no longer be edited.', nzOkText: 'Post', nzOnOk: () => this.run$(this.api.post(this.id), 'Done') });
  }
  remove(): void {
    this.modal.confirm({ nzTitle: 'Delete this draft?', nzOkDanger: true, nzOkText: 'Delete', nzOnOk: () => new Promise<void>((resolve) => this.api.remove(this.id).subscribe({ next: () => { history.back(); resolve(); }, error: (e) => { this.fail(e); resolve(); } })) });
  }

  emailAll(): void {
    this.busy.set(true);
    this.api.emailAll(this.id).subscribe({
      next: (r) => { this.busy.set(false); this.message.success(`${r.sent} payslip(s) emailed${r.skipped ? `, ${r.skipped} skipped (no email address): ${r.skippedEmployees.join(', ')}` : ''}`); },
      error: (e) => this.fail(e),
    });
  }
  emailOne(l: PayrollLine): void {
    this.api.emailPayslip(this.id, l.id).subscribe({ next: (r) => this.message.success(`Payslip sent to ${r.to}`), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not send the payslip') });
  }

  submit(v: Record<string, unknown>): void {
    if (this.mode() === 'pay') this.run$(this.api.pay(this.id, v['bankAccountId'] as string, String(v['date']).slice(0, 10), [...this.selected()]), 'Salaries paid', () => this.selected.set(new Set()));
    else if (this.mode() === 'void') this.run$(this.api.void(this.id, String(v['reason'] ?? '')), 'Payroll voided');
    else this.run$(this.api.adjust(this.id, this.line()!.id, Number(v['adjustment'] ?? 0), (v['note'] as string) ?? null), 'Line adjusted');
  }

  download(unpaidOnly: boolean): void {
    this.api.bankFile(this.id, unpaidOnly).subscribe((csv) => this.save(csv, `${this.run()?.number ?? 'payroll'}-bank-file.csv`));
  }

  downloadWps(unpaidOnly: boolean): void {
    this.api.wpsFile(this.id, unpaidOnly).subscribe({
      next: (sif) => this.save(sif, `${this.run()?.number ?? 'payroll'}.SIF`),
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'The WPS file could not be prepared'),
    });
  }

  private save(text: string, name: string): void {
    const url = URL.createObjectURL(new Blob([text], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a'); a.href = url; a.download = name; a.click(); URL.revokeObjectURL(url);
  }

  private run$(call: ReturnType<PayrollApi['post']>, success: string, after?: () => void): Promise<void> {
    this.busy.set(true); this.errors.set(null);
    return new Promise((resolve) => call.subscribe({
      next: (r) => { this.busy.set(false); this.mode.set(null); this.run.set(r); after?.(); this.message.success(r.status === 'pendingApproval' ? 'Sent for approval. Post it again once approved.' : success); resolve(); },
      error: (e) => { this.fail(e); resolve(); },
    }));
  }

  private fail(e: unknown): void { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); }
  private load(): void { this.api.detail(this.id).subscribe((r) => this.run.set(r)); }
}
