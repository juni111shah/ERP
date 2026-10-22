import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { MONTHS, PayrollApi, PayrollSummary } from './hr.api';

const COLORS: Record<string, string> = { draft: 'default', pendingApproval: 'purple', posted: 'blue', paid: 'green' };

@Component({
  selector: 'erp-payroll',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Payroll</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search run number" emptyText="No payroll runs yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <nz-tag [nzColor]="color(row.status)">{{ label(row.status) }}</nz-tag> } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'hr.payroll.manage'" nz-button nzType="primary" (click)="drawer.set(true)" data-testid="payroll-new"><span nz-icon nzType="plus"></span>Generate payroll</button>
    </erp-paged-table>
    <erp-record-drawer title="Generate payroll" [open]="drawer()" [fields]="fields()" [saving]="saving()" [errors]="errors()" [width]="440" testId="payroll-form" submitLabel="Generate"
                       (submitted)="generate($event)" (closed)="drawer.set(false)" />`,
})
export class PayrollPage {
  private readonly api = inject(PayrollApi);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-year' });
  readonly drawer = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly columns: TableColumn<PayrollSummary>[] = [
    { key: 'number', label: 'Run', width: '140px' },
    { key: 'period', label: 'Period', value: (r) => `${MONTHS[r.month - 1]} ${r.year}` },
    { key: 'employees', label: 'Employees', width: '100px' },
    { key: 'grossEarnings', label: 'Gross', value: (r) => r.grossEarnings.toLocaleString() },
    { key: 'deductions', label: 'Deductions', value: (r) => r.deductions.toLocaleString() },
    { key: 'netPay', label: 'Net pay', sortField: 'netPay', value: (r) => r.netPay.toLocaleString() },
    { key: 'paidOn', label: 'Paid on' },
    { key: 'status', label: 'Status', sortField: 'status', width: '130px' },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, options: this.entityPage().items.map((e) => ({ value: e.id, label: e.name })) },
    { key: 'period', label: 'Payroll month', type: 'date', required: true, default: new Date(), hint: 'Any day of the month; one run per entity per month.' },
    { key: 'plannedPayDate', label: 'Planned pay date', type: 'date' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 500, hint: 'Every active employee of the entity is included. Record attendance exceptions first; you can recalculate a draft at any time.' },
  ]);

  color(s: string): string { return COLORS[s] ?? 'default'; }
  label(s: string): string { return s === 'pendingApproval' ? 'Needs approval' : s.charAt(0).toUpperCase() + s.slice(1); }
  open(r: PayrollSummary): void { void this.router.navigate(['/hr/payroll', r.id]); }

  generate(v: Record<string, unknown>): void {
    const period = String(v['period']).slice(0, 10).split('-').map(Number);
    this.saving.set(true); this.errors.set(null);
    this.api.generate({ legalEntityId: v['legalEntityId'], year: period[0], month: period[1], plannedPayDate: v['plannedPayDate'] ?? null, notes: v['notes'] ?? null }).subscribe({
      next: (run) => { this.saving.set(false); this.drawer.set(false); this.message.success(`Payroll ${run.number} generated`); void this.router.navigate(['/hr/payroll', run.id]); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not generate the payroll'); },
    });
  }
}
