import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '../../core/format/done-text';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { MONTHS, PayrollApi, PayrollDto, PayrollLine } from './hr.api';
import { StatutoryRulesApi, StatutoryTotal } from './hr-people.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErpDatePipe } from '../../shared/display.pipes';

type Mode = 'pay' | 'adjust' | 'void' | null;

const STATUS_LABELS: Record<string, string> = { pendingApproval: 'Needs approval', partiallyPaid: 'Partly paid' };

/** One payroll run: totals, the employee lines (adjust a line for one-offs while draft), post, pay (all or selected employees), void, payslips and the bank files. */
@Component({
  selector: 'erp-payroll-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, TranslatePipe, DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, StatCardComponent, StatePanelComponent, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, NzTableModule, NzTagModule],
  styles: [`.num { text-align: end; white-space: nowrap; } .void-reason { color: var(--erp-color-error-text); } .sub { font-size: 12px; color: var(--erp-color-text-secondary); } .gap { margin-inline-end: 8px; }`],
  template: `
    @if (failed()) {
      <p><a routerLink="/hr/payroll">← Payroll</a></p>
      <erp-state-panel kind="error" title="This payroll run could not be loaded" hint="It may have been deleted, or your connection dropped." actionLabel="Try again" (action)="reload()" />
    }
    @if (run(); as r) {
      <p><a routerLink="/hr/payroll">← Payroll</a></p>
      <h1 data-testid="payroll-title" style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:0 0 12px;font-size:20px;font-weight:600">{{ 'ui.hr.payroll' | translate: 'Payroll' }} {{ period() }}
        <erp-status [value]="r.status" [label]="statusLabel(r.status)" />
        @if (r.kind === 'offCycle') { <nz-tag nzColor="orange">Off-cycle</nz-tag> }
        @if (r.kind === 'finalSettlement') { <nz-tag nzColor="magenta">Final settlement</nz-tag> }</h1>
      <p>{{ r.number }}@if (r.notes) { · {{ r.notes }} }</p>
      @if (r.status === 'voided') { <p class="void-reason" data-testid="payroll-void-reason">Voided @if (r.voidedAt) { on {{ r.voidedAt | erpDate }} }: {{ r.voidReason }}</p> }
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin:12px 0" data-testid="payroll-actions">
        @if (r.status === 'draft' || r.status === 'pendingApproval') {
          @if (r.kind !== 'finalSettlement') { <button *erpHasPermission="'hr.payroll.manage'" nz-button [disabled]="busy()" (click)="recalculate()" data-testid="payroll-recalc">Recalculate</button> }
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
        <a [href]="'/print/payroll/' + r.id" target="_blank" nz-button data-testid="payroll-print"><span nz-icon nzType="printer"></span>Print summary</a>
      </div>
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="payroll-totals">
        <erp-stat-card label="Employees" [value]="r.employees" format="number" [tone]="1" />
        <erp-stat-card label="Gross earnings" [value]="r.grossEarnings" format="money" [shortenMillions]="false" caption="after absence, incl. overtime" [tone]="2" />
        <erp-stat-card label="Deductions" [value]="r.deductions" format="money" [shortenMillions]="false" caption="incl. loan recoveries" [tone]="3" />
        @if (r.employerCost) { <erp-stat-card label="Employer cost" [value]="r.employerCost" format="money" [shortenMillions]="false" caption="on top of the pay" [tone]="4" /> }
        <erp-stat-card label="Net pay" [value]="r.netPay" format="money" [shortenMillions]="false" [caption]="r.paidOn ? (('ui.hr.paidOn' | translate: 'paid on') + ' ' + (r.paidOn | erpDate)) : null" testId="payroll-net" [tone]="0" />
      </div>
      <nz-table [nzData]="r.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No employees in this run" data-testid="payroll-lines">
        <thead><tr>
          @if (canSelect()) { <th style="width:36px" aria-label="Select"></th> }
          <th>Employee</th><th class="num">Days</th><th class="num">Absent</th><th class="num">OT hrs</th><th class="num">Basic</th><th class="num">Allowances</th><th class="num">Overtime</th><th class="num">Absence</th><th class="num">Adjustment</th>
          @if (hasSettlement()) { <th class="num">Leave payout</th><th class="num">End of service</th> }
          <th class="num">Gross earnings</th><th class="num">Deductions</th>
          @if (hasStatutory()) { <th class="num">of which statutory</th> }
          @if (hasLoans()) { <th class="num">of which loans</th> }
          <th class="num">Net pay ({{ currency() }})</th><th>Paid</th><th>Actions</th></tr></thead>
        <tbody>
          @for (l of r.lines; track l.id) {
            <tr>
              @if (canSelect()) { <td>@if (!l.paidOn) { <input type="checkbox" [attr.aria-label]="'Select ' + l.employeeName" [checked]="selected().has(l.employeeId)" (change)="toggle(l.employeeId)" data-testid="payroll-select" /> }</td> }
              <td>{{ l.employeeName }}<div class="sub">{{ l.employeeNumber }}</div></td>
              <td class="num">{{ l.proratedDays || '' }}</td><td class="num">{{ l.absentDays }}</td><td class="num">{{ l.overtimeHours }}</td><td class="num">{{ l.basic | number: '1.2-2' }}</td><td class="num">{{ l.allowances | number: '1.2-2' }}</td><td class="num">{{ l.overtime | number: '1.2-2' }}</td>
              <td class="num">{{ l.absence | number: '1.2-2' }}</td><td class="num">{{ l.adjustment | number: '1.2-2' }}</td>
              @if (hasSettlement()) { <td class="num">{{ l.leaveEncashment | number: '1.2-2' }}</td><td class="num">{{ l.endOfService | number: '1.2-2' }}</td> }
              <td class="num">{{ l.grossEarnings | number: '1.2-2' }}</td><td class="num">{{ l.deductions | number: '1.2-2' }}</td>
              @if (hasStatutory()) { <td class="num">{{ l.statutoryDeduction | number: '1.2-2' }}</td> }
              @if (hasLoans()) { <td class="num">{{ l.loanRecovery | number: '1.2-2' }}</td> }
              <td class="num"><b>{{ l.netPay | number: '1.2-2' }}</b></td>
              <td>{{ l.paidOn | erpDate }}</td>
              <td style="white-space:nowrap">
                <a class="gap" [href]="'/print/payslip/' + r.id + '?employeeId=' + l.employeeId" target="_blank" data-testid="payroll-payslip">Payslip</a>
                @if (r.status === 'posted' || r.status === 'partiallyPaid' || r.status === 'paid') { <a class="gap" (click)="emailOne(l)" data-testid="payroll-email-one">Email</a> }
                @if (r.status === 'draft' || r.status === 'pendingApproval') { <a *erpHasPermission="'hr.payroll.manage'" (click)="startAdjust(l)" data-testid="payroll-adjust">Adjust</a> }
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
      @if (statutory().length) {
        <h3 style="margin-top:20px">Statutory deductions and contributions</h3>
        <nz-table [nzData]="statutory()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="payroll-statutory">
          <thead><tr><th>Rule</th><th>Taken from</th><th class="num">Employees</th><th class="num">Base</th><th class="num">Amount ({{ currency() }})</th></tr></thead>
          <tbody>@for (s of statutory(); track s.code) { <tr><td>{{ s.code }} · {{ s.name }}</td><td>{{ s.kind === 'employerCost' ? 'Employer' : 'Employee' }}</td><td class="num">{{ s.employees }}</td><td class="num">{{ s.baseAmount | number: '1.2-2' }}</td><td class="num"><b>{{ s.amount | number: '1.2-2' }}</b></td></tr> }</tbody>
        </nz-table>
      }
      <erp-record-drawer [title]="title()" [open]="mode() !== null" [fields]="fields()" [saving]="busy()" [errors]="errors()" [width]="440"
                         testId="payroll-action-form" [submitLabel]="mode() === 'pay' ? 'Pay' : mode() === 'void' ? 'Void payroll' : 'Save'" (submitted)="submit($event)" (closed)="mode.set(null)" />
    }`,
})
export class PayrollDetailPage {
  private readonly api = inject(PayrollApi);
  private readonly statutoryApi = inject(StatutoryRulesApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly failed = signal(false);
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly run = signal<PayrollDto | null>(null);
  readonly busy = signal(false);
  readonly mode = signal<Mode>(null);
  readonly line = signal<PayrollLine | null>(null);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly errors = signal<ProblemDetails | null>(null);
  readonly period = computed(() => { const r = this.run(); return r ? `${MONTHS[r.month - 1]} ${r.year}` : ''; });
  readonly canSelect = computed(() => { const r = this.run(); return !!r && (r.status === 'posted' || r.status === 'partiallyPaid') && r.lines.some((l) => !l.paidOn); });
  readonly statutory = signal<StatutoryTotal[]>([]);
  readonly hasStatutory = computed(() => !!this.run()?.lines.some((l) => (l.statutoryDeduction ?? 0) > 0));
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

  statusLabel(s: string): string | null { return STATUS_LABELS[s] ?? null; }
  reload(): void { this.load(); }
  toggle(employeeId: string): void { const next = new Set(this.selected()); if (!next.delete(employeeId)) next.add(employeeId); this.selected.set(next); }
  startAdjust(l: PayrollLine): void { this.errors.set(null); this.line.set(l); this.mode.set('adjust'); }
  startVoid(): void { this.errors.set(null); this.mode.set('void'); }

  recalculate(): void { this.run$(this.api.recalculate(this.id), (r) => `Payroll ${r.number} recalculated`); }
  post(): void {
    this.modal.confirm({ nzTitle: 'Post this payroll?', nzContent: 'The accrual is posted to the ledger and the run can no longer be edited.', nzOkText: 'Post', nzOnOk: () => this.run$(this.api.post(this.id), (r) => doneText('posted', 'Payroll', r.number)) });
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
    if (this.mode() === 'pay') this.run$(this.api.pay(this.id, v['bankAccountId'] as string, String(v['date']).slice(0, 10), [...this.selected()]), (r) => `Salaries paid for payroll ${r.number}`, () => this.selected.set(new Set()));
    else if (this.mode() === 'void') this.run$(this.api.void(this.id, String(v['reason'] ?? '')), (r) => doneText('voided', 'Payroll', r.number));
    else this.run$(this.api.adjust(this.id, this.line()!.id, Number(v['adjustment'] ?? 0), (v['note'] as string) ?? null), (r) => `Payroll ${r.number} line adjusted`);
  }

  download(unpaidOnly: boolean): void {
    this.api.bankFile(this.id, unpaidOnly).subscribe({
      next: (csv) => this.save(csv, `${this.run()?.number ?? 'payroll'}-bank-file.csv`),
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'The bank file could not be prepared'),
    });
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

  private run$(call: ReturnType<PayrollApi['post']>, success: (r: PayrollDto) => string, after?: () => void): Promise<void> {
    this.busy.set(true); this.errors.set(null);
    return new Promise((resolve) => call.subscribe({
      next: (r) => { this.busy.set(false); this.mode.set(null); this.run.set(r); this.loadTotals(r); after?.(); this.message.success(r.status === 'pendingApproval' ? 'Sent for approval. Post it again once approved.' : success(r)); resolve(); },
      error: (e) => { this.fail(e); resolve(); },
    }));
  }

  private fail(e: unknown): void { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); }
  private load(): void {
    this.failed.set(false);
    this.api.detail(this.id).subscribe({ next: (r) => { this.run.set(r); this.loadTotals(r); }, error: () => this.failed.set(true) });
  }
  private loadTotals(r: PayrollDto): void {
    this.statutoryApi.totals(r.id).subscribe({ next: (t) => this.statutory.set(t), error: () => this.statutory.set([]) });
  }
}
