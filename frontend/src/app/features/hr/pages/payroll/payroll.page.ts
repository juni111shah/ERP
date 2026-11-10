import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi, LegalEntityDto } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { MONTHS, PayrollApi, PayrollSummary } from '@core/api/hr.api';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

import { DecimalPipe } from '@angular/common';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

@Component({
  selector: 'erp-payroll',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule],
  templateUrl: './payroll.page.html',
})
export class PayrollPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(PayrollApi);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-year,-month' });
  readonly drawer = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly columns: TableColumn<PayrollSummary>[] = [
    { key: 'number', label: 'Run', width: '140px' },
    { key: 'period', label: 'Period', value: (r) => `${MONTHS[r.month - 1]} ${r.year}` },
    { key: 'kind', label: 'Type', value: (r) => (r.kind === 'offCycle' ? 'Off-cycle' : r.kind === 'finalSettlement' ? 'Final settlement' : 'Regular'), width: '130px', filterField: 'kind', filterOptions: [{ value: 'regular', label: 'Regular' }, { value: 'offCycle', label: 'Off-cycle' }, { value: 'finalSettlement', label: 'Final settlement' }] },
    { key: 'employees', label: 'Employees', width: '100px' },
    { key: 'grossEarnings', label: 'Gross', align: 'right', value: (r) => r.grossEarnings.toLocaleString() },
    { key: 'deductions', label: 'Deductions', align: 'right', value: (r) => r.deductions.toLocaleString() },
    { key: 'netPay', label: 'Net pay', sortField: 'netPay', align: 'right', value: (r) => r.netPay.toLocaleString() },
    { key: 'paidOn', label: 'Paid on' },
    { key: 'status', label: 'Status', sortField: 'status', width: '130px' },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, options: this.entityPage().items.map((e) => ({ value: e.id, label: e.name })) },
    { key: 'period', label: 'Payroll month', type: 'date', required: true, default: new Date(), hint: 'Any day of the month; one run per entity per month.' },
    { key: 'kind', label: 'Type', type: 'select', options: [{ value: 'regular', label: 'Regular monthly run' }, { value: 'offCycle', label: 'Off-cycle (bonus, correction)' }], default: 'regular', hint: 'An off-cycle run needs a note saying why. Final settlements are prepared from the employee record.' },
    { key: 'includeSalary', label: 'Include salary and deductions', type: 'switch', default: false, hidden: (v) => v['kind'] !== 'offCycle', hint: 'Off.: lines start at zero and you add one-off amounts.' },
    { key: 'plannedPayDate', label: 'Planned pay date', type: 'date' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500, hint: 'Every active employee of the entity is included. Record attendance exceptions first; you can recalculate a draft at any time.' },
  ]);

  label(s: string): string | null { return s === 'pendingApproval' ? 'Needs approval' : s === 'partiallyPaid' ? 'Partly paid' : null; }
  open(r: PayrollSummary): void { void this.router.navigate(['/hr/payroll', r.id]); }

  generate(v: Record<string, unknown>): void {
    const period = String(v['period']).slice(0, 10).split('-').map(Number);
    this.saving.set(true); this.errors.set(null);
    this.api.generate({ legalEntityId: v['legalEntityId'], year: period[0], month: period[1], plannedPayDate: v['plannedPayDate'] ?? null, notes: v['notes'] ?? null, kind: v['kind'] ?? 'regular', includeSalary: v['includeSalary'] === true }).subscribe({
      next: (run) => { this.saving.set(false); this.drawer.set(false); this.message.success(`${this.i18n.t('ui.msg.payrollGenerated', 'Payroll generated')}: ${run.number}`); void this.router.navigate(['/hr/payroll', run.id]); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not generate the payroll'); },
    });
  }
}
