import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { LOAN_KINDS, LOAN_STATUSES, LoanRow, LoansApi } from './hr-people.api';
import { EmployeeSummary, EmployeesApi, MONTHS } from './hr.api';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

/** Staff loans and salary advances: instalments are recovered by the monthly payroll run. */
@Component({
  selector: 'erp-loans',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule],
  template: `
    <h1 class="erp-page-title">Loans and advances</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search reason" emptyText="No loans or advances yet" [clickable]="false">
      <button toolbar *erpHasPermission="'hr.payroll.manage'" nz-button nzType="primary" (click)="mode.set('create')" data-testid="loan-new"><span nz-icon nzType="plus"></span>New loan</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('principal') { {{ currency() }} {{ row.principal | number: '1.2-2' }} }
          @case ('remaining') { <b>{{ currency() }} {{ row.remaining | number: '1.2-2' }}</b> }
          @case ('actions') {
            @if (row.status === 'active') {
              @if (!row.disbursedOn) { <a *erpHasPermission="'hr.payroll.pay'" (click)="startPayOut(row)" data-testid="loan-disburse" style="margin-inline-end:10px">Pay out</a> }
              <a *erpHasPermission="'hr.payroll.manage'" (click)="cancel(row)" data-testid="loan-cancel">Cancel</a>
            }
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer [title]="mode() === 'create' ? 'New loan or advance' : 'Pay out the loan'" [open]="mode() !== null" [fields]="fields()" [saving]="saving()" [errors]="errors()" [width]="460"
                       testId="loan-form" [submitLabel]="mode() === 'create' ? 'Save' : 'Pay out'" (submitted)="submit($event)" (closed)="mode.set(null)" />`,
})
export class LoansPage {
  private readonly api = inject(LoansApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly employeePage = toSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'fullName' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[] }))), { initialValue: { items: [] as EmployeeSummary[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-startYear' });
  readonly mode = signal<'create' | 'disburse' | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  private readonly target = signal<LoanRow | null>(null);

  readonly columns: TableColumn<LoanRow>[] = [
    { key: 'employee', label: 'Employee', value: (l) => this.employeePage().items.find((e) => e.id === l.employeeId)?.fullName ?? '' },
    { key: 'kind', label: 'Type', value: (l) => LOAN_KINDS.find((k) => k.value === l.kind)?.label ?? l.kind, width: '130px' },
    { key: 'principal', label: 'Amount', align: 'right', value: (l) => l.principal.toLocaleString(), width: '140px' },
    { key: 'remaining', label: 'Remaining', align: 'right', width: '140px' },
    { key: 'start', label: 'Recovery starts', value: (l) => `${MONTHS[l.startMonth - 1]} ${l.startYear}`, width: '150px' },
    { key: 'instalments', label: 'Instalments', width: '100px' },
    { key: 'disbursedOn', label: 'Paid out on', width: '120px' },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: LOAN_STATUSES },
    { key: 'actions', label: '', width: '140px' },
  ];

  readonly fields = computed<FieldSpec[]>(() => this.mode() === 'disburse'
    ? [
        { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
        { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
      ]
    : [
        { key: 'employeeId', label: 'Employee', type: 'select', required: true, options: this.employeePage().items.filter((e) => e.status !== 'terminated').map((e) => ({ value: e.id, label: `${e.number} · ${e.fullName}` })) },
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
      nzOnOk: () => new Promise<void>((resolve) => this.api.cancel(l.id).subscribe({ next: () => { this.message.success('Loan cancelled'); this.list.reload(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not cancel'); resolve(); } })),
    });
  }

  submit(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    const payOut = this.mode() === 'disburse';
    let call;
    if (this.mode() === 'disburse') {
      call = this.api.disburse(this.target()!.id, v['bankAccountId'] as string, String(v['date']).slice(0, 10));
    } else {
      const [year, month] = String(v['start']).slice(0, 10).split('-').map(Number);
      call = this.api.create({ employeeId: v['employeeId'], kind: v['kind'], principal: v['principal'], instalments: v['instalments'], instalmentAmount: v['instalmentAmount'] ?? null, startYear: year, startMonth: month, reason: v['reason'] ?? null });
    }
    call.subscribe({
      next: () => { this.saving.set(false); this.mode.set(null); this.message.success(payOut ? 'Loan paid out' : 'Loan saved'); this.list.reload(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save'); },
    });
  }
}
