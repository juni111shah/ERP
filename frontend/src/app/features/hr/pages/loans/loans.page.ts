import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { Observable } from 'rxjs';
import { AccountsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { LOAN_KINDS, LOAN_STATUSES, LoanInput, LoanRow, LoansApi } from '@core/api/hr-people.api';
import { EmployeesApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { itemsSignal } from '@shared/utils/items-signal';
import { accountOptions, bankAccounts } from '@shared/utils/payment-accounts';
import { dayText, monthYear } from '../../hr-format';
import { currentEmployees, employeeOptions, optionLabel } from '../../hr-options';

/** The new-loan drawer's values as the API takes them: recovery starts in the month of the picked day. */
function loanInput(v: Record<string, unknown>): LoanInput {
  const [year, month] = dayText(v['start']).split('-').map(Number);
  return {
    employeeId: v['employeeId'] as string, kind: v['kind'] as string, principal: v['principal'] as number, instalments: v['instalments'] as number,
    instalmentAmount: (v['instalmentAmount'] as number | undefined) ?? null, startYear: year, startMonth: month, reason: (v['reason'] as string | undefined) ?? null,
  };
}

/** Staff loans and salary advances: instalments are recovered by the monthly payroll run. */
@Component({
  selector: 'erp-loans',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule],
  templateUrl: './loans.page.html',
})
export class LoansPage {
  private readonly loansApi = inject(LoansApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly employeePage = itemsSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'fullName' }));
  private readonly accountPage = itemsSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  readonly list = createPagedList((q) => this.loansApi.list(q), { sort: '-startYear' });
  readonly mode = signal<'create' | 'disburse' | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  private readonly target = signal<LoanRow | null>(null);

  readonly columns: TableColumn<LoanRow>[] = [
    { key: 'employee', label: 'Employee', value: (l) => this.employeePage().items.find((e) => e.id === l.employeeId)?.fullName ?? '' },
    { key: 'kind', label: 'Type', value: (l) => optionLabel(LOAN_KINDS, l.kind), width: '130px' },
    { key: 'principal', label: 'Amount', align: 'right', value: (l) => l.principal.toLocaleString(), width: '140px' },
    { key: 'remaining', label: 'Remaining', align: 'right', width: '140px' },
    { key: 'start', label: 'Recovery starts', value: (l) => monthYear(l.startYear, l.startMonth), width: '150px' },
    { key: 'instalments', label: 'Instalments', width: '100px' },
    { key: 'disbursedOn', label: 'Paid out on', width: '120px' },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: LOAN_STATUSES },
    { key: 'actions', label: '', width: '140px' },
  ];

  readonly fields = computed<FieldSpec[]>(() => this.mode() === 'disburse'
    ? [
        { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: accountOptions(bankAccounts(this.accountPage().items)) },
        { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
      ]
    : [
        { key: 'employeeId', label: 'Employee', type: 'select', required: true, options: employeeOptions(currentEmployees(this.employeePage().items)) },
        { key: 'kind', label: 'Type', type: 'select', required: true, options: LOAN_KINDS, default: 'loan' },
        { key: 'principal', label: 'Amount', type: 'number', required: true, min: 0.01 },
        { key: 'instalments', label: 'Number of instalments', type: 'number', required: true, min: 1, max: 120, default: 1 },
        { key: 'instalmentAmount', label: 'Instalment amount', type: 'number', min: 0.01, hint: 'Leave blank to divide the amount equally.' },
        { key: 'start', label: 'First recovery month', type: 'date', required: true, default: new Date(), hint: 'Any day of the month.' },
        { key: 'reason', label: 'Reason', type: 'text', maxLength: 300 },
      ]);

  startPayOut(l: LoanRow): void { this.target.set(l); this.errors.set(null); this.mode.set('disburse'); }

  cancel(l: LoanRow): void {
    this.modal.confirm({
      nzTitle: 'Cancel this loan?', nzContent: 'Recovery stops. Instalments already taken in posted payrolls stay.', nzOkText: 'Cancel loan', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.loansApi.cancel(l.id).subscribe({ next: () => { this.message.success(doneText('cancelled', 'Loan')); this.list.reload(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not cancel'); resolve(); } })),
    });
  }

  /** The drawer's OK: pays out the chosen loan, or saves a new one. */
  submit(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    const payOut = this.mode() === 'disburse';
    const call: Observable<unknown> = payOut
      ? this.loansApi.disburse(this.target()!.id, v['bankAccountId'] as string, dayText(v['date']))
      : this.loansApi.create(loanInput(v));
    call.subscribe({
      next: () => { this.saving.set(false); this.mode.set(null); this.message.success(payOut ? `Loan of ${this.target()?.principal} paid out` : `Loan of ${v['principal']} saved`); this.list.reload(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save'); },
    });
  }
}
