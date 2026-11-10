import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { Observable } from 'rxjs';
import { AccountsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { StatutoryRulesApi, StatutoryTotal } from '@core/api/hr-people.api';
import { PayrollApi, PayrollDto, PayrollLine } from '@core/api/hr.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { itemsSignal } from '@shared/utils/items-signal';
import { accountOptions, bankAccounts } from '@shared/utils/payment-accounts';
import { dayText, monthYear } from '../../hr-format';

type Mode = 'pay' | 'adjust' | 'void' | null;

const STATUS_LABELS: Record<string, string> = { pendingApproval: 'Needs approval', partiallyPaid: 'Partly paid' };

/** Offers `text` as a file download called `name`. */
function downloadText(text: string, name: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a'); a.href = url; a.download = name; a.click(); URL.revokeObjectURL(url);
}

/** One payroll run: totals, the employee lines (adjust a line for one-offs while draft), post, pay (all or selected employees), void, payslips and the bank files. */
@Component({
  selector: 'erp-payroll-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, TranslatePipe, DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, StatCardComponent, StatePanelComponent, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, NzTableModule, NzTagModule],
  styleUrl: './payroll-detail.page.scss',
  templateUrl: './payroll-detail.page.html',
})
export class PayrollDetailPage {
  private readonly payrollApi = inject(PayrollApi);
  private readonly statutoryApi = inject(StatutoryRulesApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly failed = signal(false);
  private readonly accountPage = itemsSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  readonly run = signal<PayrollDto | null>(null);
  readonly busy = signal(false);
  readonly mode = signal<Mode>(null);
  readonly line = signal<PayrollLine | null>(null);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly errors = signal<ProblemDetails | null>(null);
  readonly period = computed(() => { const r = this.run(); return r ? monthYear(r.year, r.month) : ''; });
  readonly canSelect = computed(() => { const r = this.run(); return !!r && (r.status === 'posted' || r.status === 'partiallyPaid') && r.lines.some((l) => !l.paidOn); });
  readonly statutory = signal<StatutoryTotal[]>([]);
  readonly hasStatutory = computed(() => !!this.run()?.lines.some((l) => (l.statutoryDeduction ?? 0) > 0));
  readonly hasLoans = computed(() => !!this.run()?.lines.some((l) => (l.loanRecovery ?? 0) > 0));
  readonly hasSettlement = computed(() => this.run()?.kind === 'finalSettlement');
  readonly title = computed(() => this.mode() === 'pay' ? 'Pay salaries' : this.mode() === 'void' ? 'Void this payroll' : 'Adjust ' + (this.line()?.employeeName ?? ''));
  readonly fields = computed<FieldSpec[]>(() => {
    if (this.mode() === 'pay') {
      return [
        { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: accountOptions(bankAccounts(this.accountPage().items)) },
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

  recalculate(): void { this.runAction(this.payrollApi.recalculate(this.id), (r) => `Payroll ${r.number} recalculated`); }
  post(): void {
    this.modal.confirm({ nzTitle: 'Post this payroll?', nzContent: 'The accrual is posted to the ledger and the run can no longer be edited.', nzOkText: 'Post', nzOnOk: () => this.runAction(this.payrollApi.post(this.id), (r) => doneText('posted', 'Payroll', r.number)) });
  }
  remove(): void {
    this.modal.confirm({ nzTitle: 'Delete this draft?', nzOkDanger: true, nzOkText: 'Delete', nzOnOk: () => new Promise<void>((resolve) => this.payrollApi.remove(this.id).subscribe({ next: () => { history.back(); resolve(); }, error: (e) => { this.fail(e); resolve(); } })) });
  }

  emailAll(): void {
    this.busy.set(true);
    this.payrollApi.emailAll(this.id).subscribe({
      next: (r) => { this.busy.set(false); this.message.success(`${r.sent} payslip(s) emailed${r.skipped ? `, ${r.skipped} skipped (no email address): ${r.skippedEmployees.join(', ')}` : ''}`); },
      error: (e) => this.fail(e),
    });
  }
  emailOne(l: PayrollLine): void {
    this.payrollApi.emailPayslip(this.id, l.id).subscribe({ next: (r) => this.message.success(`Payslip sent to ${r.to}`), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not send the payslip') });
  }

  /** The drawer's OK: pay, void or adjust a line, depending on the mode it was opened in. */
  submit(v: Record<string, unknown>): void {
    if (this.mode() === 'pay') this.runAction(this.payrollApi.pay(this.id, v['bankAccountId'] as string, dayText(v['date']), [...this.selected()]), (r) => `Salaries paid for payroll ${r.number}`, () => this.selected.set(new Set()));
    else if (this.mode() === 'void') this.runAction(this.payrollApi.void(this.id, String(v['reason'] ?? '')), (r) => doneText('voided', 'Payroll', r.number));
    else this.runAction(this.payrollApi.adjust(this.id, this.line()!.id, Number(v['adjustment'] ?? 0), (v['note'] as string) ?? null), (r) => `Payroll ${r.number} line adjusted`);
  }

  download(unpaidOnly: boolean): void {
    this.payrollApi.bankFile(this.id, unpaidOnly).subscribe({
      next: (csv) => downloadText(csv, `${this.run()?.number ?? 'payroll'}-bank-file.csv`),
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'The bank file could not be prepared'),
    });
  }

  downloadWps(unpaidOnly: boolean): void {
    this.payrollApi.wpsFile(this.id, unpaidOnly).subscribe({
      next: (sif) => downloadText(sif, `${this.run()?.number ?? 'payroll'}.SIF`),
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'The WPS file could not be prepared'),
    });
  }

  /** Runs an action on the run and shows the run it returns; resolves when done, so a confirm dialog can wait for it. */
  private runAction(call: Observable<PayrollDto>, success: (r: PayrollDto) => string, after?: () => void): Promise<void> {
    this.busy.set(true); this.errors.set(null);
    return new Promise((resolve) => call.subscribe({
      next: (r) => { this.busy.set(false); this.mode.set(null); this.run.set(r); this.loadTotals(r); after?.(); this.message.success(r.status === 'pendingApproval' ? 'Sent for approval. Post it again once approved.' : success(r)); resolve(); },
      error: (e) => { this.fail(e); resolve(); },
    }));
  }

  private fail(e: unknown): void { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); }
  private load(): void {
    this.failed.set(false);
    this.payrollApi.detail(this.id).subscribe({ next: (r) => { this.run.set(r); this.loadTotals(r); }, error: () => this.failed.set(true) });
  }
  private loadTotals(r: PayrollDto): void {
    this.statutoryApi.totals(r.id).subscribe({ next: (t) => this.statutory.set(t), error: () => this.statutory.set([]) });
  }
}
