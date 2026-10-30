import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DecimalPipe } from '@angular/common';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { EMPLOYEE_STATUSES, EmployeeDto, EmployeeSummary, EmployeesApi } from './hr.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { StatCardComponent } from '../../shared/stat-card.component';

/** Employees: salary structure, join date and status; end-of-service accrual under the employing entity's country rule is shown on the record. */
@Component({
  selector: 'erp-employees',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, RevealDirective, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, DecimalPipe, StatCardComponent],
  styles: [`
    .emp-cell { display: flex; align-items: center; gap: 10px; }
    .emp-avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: linear-gradient(135deg, #10955d 0%, #05663d 100%);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 12px;
      flex-shrink: 0;
    }
    .emp-info { display: flex; flex-direction: column; }
    .emp-name { font-weight: 600; color: var(--erp-color-text-title, #0f172a); font-size: 13px; }
  `],
  template: `
    <h1 class="erp-page-title">Employees</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="employee-stats">
      <erp-stat-card label="Total employees"
                     [value]="list.total()"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     [caption]="stats().countOnPage + ' ' + ('table.onThisPage' | translate: 'on this page')"
                     [tone]="0" />
      <erp-stat-card label="Active staff"
                     [value]="stats().active"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="on payroll"
                     [tone]="1" />
      <erp-stat-card label="Monthly payroll"
                     [value]="stats().totalGross"
                     format="money"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="total gross on page"
                     [tone]="2" />
      <erp-stat-card label="Departments"
                     [value]="stats().departmentsCount"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="active teams"
                     [tone]="3" />
    </div>

    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search name, designation or department" emptyText="No employees yet" (rowClick)="open($event)">
      <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All statuses" (ngModelChange)="filterStatus($event)" [ngModel]="null" data-testid="emp-status-filter">
        @for (s of statuses; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="emp-new"><span nz-icon nzType="plus"></span>New employee</button>

      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('number') {
            <span class="code-pill">{{ row.number }}</span>
          }
          @case ('fullName') {
            <div class="emp-cell">
              <div class="emp-avatar">{{ initials(row.fullName) }}</div>
              <div class="emp-info">
                <span class="emp-name">{{ row.fullName }}</span>
              </div>
            </div>
          }
          @case ('department') {
            @if (row.department) {
              <nz-tag>{{ row.department }}</nz-tag>
            } @else {
              <span class="muted-text">—</span>
            }
          }
          @case ('grossMonthly') {
            <b>{{ currency() }} {{ row.grossMonthly | number: '1.2-2' }}</b>
          }
          @case ('status') {
            <nz-tag [nzColor]="row.status === 'active' ? 'green' : row.status === 'terminated' ? 'red' : 'blue'">
              {{ row.status }}
            </nz-tag>
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? state.editing()!.fullName : 'New employee'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())" customFieldEntityType="hr.employee"
                       [saving]="state.saving()" [errors]="state.errors()" testId="emp-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) {
        <p drawerActions data-testid="emp-summary">Gross <b>{{ e.grossMonthly.toLocaleString() }}</b> {{ 'ui.hr.aMonth' | translate: 'a month' }} · {{ 'ui.hr.service' | translate: 'service' }} {{ e.serviceYears.toFixed(1) }} {{ 'ui.hr.years' | translate: 'years' }} · {{ 'ui.hr.eosAccrued' | translate: 'end-of-service accrued' }} <b>{{ e.gratuityAccrued.toLocaleString() }}</b></p>
      }
    </erp-record-drawer>`,
})
export class EmployeesPage {
  private readonly api = inject(EmployeesApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  readonly statuses = EMPLOYEE_STATUSES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'number' });
  readonly state = new MasterDataState<EmployeeDto>({
    save: (id, body) => this.api.save(id, body), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Employee',
  });

  readonly stats = computed(() => {
    const items = this.list.items();
    const active = items.filter((e) => e.status === 'active').length;
    const totalGross = items.reduce((sum, e) => sum + (e.grossMonthly || 0), 0);
    const depts = new Set(items.map((e) => e.department).filter(Boolean));
    return {
      active,
      totalGross,
      departmentsCount: depts.size,
      countOnPage: items.length,
    };
  });

  initials(name: string): string {
    return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'E';
  }
  readonly columns: TableColumn<EmployeeSummary>[] = [
    { key: 'number', label: 'No.', sortField: 'number', width: '110px' },
    { key: 'fullName', label: 'Name', sortField: 'fullName' },
    { key: 'designation', label: 'Designation' },
    { key: 'department', label: 'Department' },
    { key: 'joinDate', label: 'Joined', sortField: 'joinDate', width: '120px' },
    { key: 'grossMonthly', label: 'Gross / month', value: (e) => e.grossMonthly.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status', width: '110px', filterField: 'status', filterOptions: EMPLOYEE_STATUSES },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, readonlyOnEdit: true, options: this.entityPage().items.map((e) => ({ value: e.id, label: e.name })) },
    { key: 'number', label: 'Employee no.', type: 'text', maxLength: 30, hint: 'Leave blank to auto-number.' },
    { key: 'fullName', label: 'Full name', type: 'text', required: true, maxLength: 200 },
    { key: 'designation', label: 'Designation', type: 'text', required: true, maxLength: 150 },
    { key: 'department', label: 'Department', type: 'text', maxLength: 100 },
    { key: 'joinDate', label: 'Join date', type: 'date', required: true },
    { key: 'status', label: 'Status', type: 'select', options: EMPLOYEE_STATUSES, default: 'active' },
    { key: 'terminationDate', label: 'Termination date', type: 'date', hidden: (v) => v['status'] !== 'terminated' },
    { key: 'email', label: 'Email', type: 'text', maxLength: 200 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 50 },
    { key: 'nationality', label: 'Nationality', type: 'text', maxLength: 100 },
    { key: 'basicSalary', label: 'Basic salary (monthly)', type: 'number', required: true, min: 0, hint: 'Also the end-of-service base.' },
    { key: 'housingAllowance', label: 'Housing allowance', type: 'number', min: 0, default: 0 },
    { key: 'transportAllowance', label: 'Transport allowance', type: 'number', min: 0, default: 0 },
    { key: 'otherAllowances', label: 'Other allowances', type: 'number', min: 0, default: 0 },
    { key: 'recurringDeductions', label: 'Recurring deductions', type: 'number', min: 0, default: 0, hint: 'Loan or advance recovery per month.' },
    { key: 'iban', label: 'IBAN', type: 'text', maxLength: 50 },
    { key: 'labourCardNumber', label: 'Labour card / MOHRE personal no.', type: 'text', maxLength: 30, hint: 'UAE WPS: the 14-digit number used in the salary file.' },
    { key: 'bankRoutingCode', label: 'Bank routing code', type: 'text', maxLength: 20, hint: 'UAE WPS: routing code of the employee\'s bank.' },
  ]);

  filterStatus(s: string | null): void { this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []); }
  open(row: EmployeeSummary): void { this.api.detail(row.id).subscribe((e) => this.state.edit(e)); }
}
