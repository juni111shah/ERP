import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { PayrollApi, PayrollRunInput, PayrollSummary } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { asProblem } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { itemsSignal } from '@shared/utils/items-signal';
import { dayText, monthYear } from '../../hr-format';
import { entityOptions } from '../../hr-options';

const RUN_KINDS = [{ value: 'regular', label: 'Regular' }, { value: 'offCycle', label: 'Off-cycle' }, { value: 'finalSettlement', label: 'Final settlement' }];

/** "Regular", "Off-cycle" or "Final settlement". */
function runKindLabel(kind: string | undefined): string {
  return kind === 'offCycle' ? 'Off-cycle' : kind === 'finalSettlement' ? 'Final settlement' : 'Regular';
}

/** The new-run drawer's values as the API takes them: the month of the picked day, and the off-cycle options. */
function payrollRunInput(v: Record<string, unknown>): PayrollRunInput {
  const [year, month] = dayText(v['period']).split('-').map(Number);
  return {
    legalEntityId: v['legalEntityId'] as string, year, month, plannedPayDate: (v['plannedPayDate'] as string | undefined) ?? null, notes: (v['notes'] as string | undefined) ?? null,
    kind: (v['kind'] as string | undefined) ?? 'regular', includeSalary: v['includeSalary'] === true,
  };
}

/** Payroll runs: the list of monthly, off-cycle and final-settlement runs, and the drawer that generates a new one. */
@Component({
  selector: 'erp-payroll',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzToolTipModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule],
  templateUrl: './payroll.page.html',
})
export class PayrollPage {
  private readonly i18n = inject(I18nService);
  private readonly payrollApi = inject(PayrollApi);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly entityPage = itemsSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }));
  readonly list = createPagedList((q) => this.payrollApi.list(q), { sort: '-year,-month' });
  readonly drawer = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly columns: TableColumn<PayrollSummary>[] = [
    { key: 'number', label: 'Run', width: '140px' },
    { key: 'period', label: 'Period', value: (r) => monthYear(r.year, r.month) },
    { key: 'kind', label: 'Type', value: (r) => runKindLabel(r.kind), width: '130px', filterField: 'kind', filterOptions: RUN_KINDS },
    { key: 'employees', label: 'Employees', width: '100px' },
    { key: 'grossEarnings', label: 'Gross', align: 'right', value: (r) => r.grossEarnings.toLocaleString() },
    { key: 'deductions', label: 'Deductions', align: 'right', value: (r) => r.deductions.toLocaleString() },
    { key: 'netPay', label: 'Net pay', sortField: 'netPay', align: 'right', value: (r) => r.netPay.toLocaleString() },
    { key: 'paidOn', label: 'Paid on' },
    { key: 'status', label: 'Status', sortField: 'status', width: '130px' },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, options: entityOptions(this.entityPage().items) },
    { key: 'period', label: 'Payroll month', type: 'date', required: true, default: new Date(), hint: 'Any day of the month; one run per entity per month.' },
    { key: 'kind', label: 'Type', type: 'select', options: [{ value: 'regular', label: 'Regular monthly run' }, { value: 'offCycle', label: 'Off-cycle (bonus, correction)' }], default: 'regular', hint: 'An off-cycle run needs a note saying why. Final settlements are prepared from the employee record.' },
    { key: 'includeSalary', label: 'Include salary and deductions', type: 'switch', default: false, hidden: (v) => v['kind'] !== 'offCycle', hint: 'Off.: lines start at zero and you add one-off amounts.' },
    { key: 'plannedPayDate', label: 'Planned pay date', type: 'date' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500, hint: 'Every active employee of the entity is included. Record attendance exceptions first; you can recalculate a draft at any time.' },
  ]);

  label(s: string): string | null { return s === 'pendingApproval' ? 'Needs approval' : s === 'partiallyPaid' ? 'Partly paid' : null; }
  open(r: PayrollSummary): void { void this.router.navigate(['/hr/payroll', r.id]); }

  generate(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    this.payrollApi.generate(payrollRunInput(v)).subscribe({
      next: (run) => { this.saving.set(false); this.drawer.set(false); this.message.success(`${this.i18n.t('ui.msg.payrollGenerated', 'Payroll generated')}: ${run.number}`); void this.router.navigate(['/hr/payroll', run.id]); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not generate the payroll'); },
    });
  }
}
