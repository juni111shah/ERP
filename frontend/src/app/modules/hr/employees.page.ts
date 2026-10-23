import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { EMPLOYEE_STATUSES, EmployeeDto, EmployeeSummary, EmployeesApi } from './hr.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';

/** Employees: salary structure, join date and status; end-of-service accrual under the employing entity's country rule is shown on the record. */
@Component({
  selector: 'erp-employees',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, RevealDirective, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Employees</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search name, designation or department" emptyText="No employees yet" (rowClick)="open($event)">
      <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All statuses" (ngModelChange)="filterStatus($event)" [ngModel]="null" data-testid="emp-status-filter">
        @for (s of statuses; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newEmployee' | translate: 'New employee'" nz-tooltip [nzTooltipTitle]="'ui.add.newEmployee' | translate: 'New employee'" nzType="primary" (click)="state.create()" data-testid="emp-new"><span nz-icon nzType="plus"></span>New employee</button>
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
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  readonly statuses = EMPLOYEE_STATUSES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'number' });
  readonly state = new MasterDataState<EmployeeDto>({
    save: (id, body) => this.api.save(id, body), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Employee',
  });
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
