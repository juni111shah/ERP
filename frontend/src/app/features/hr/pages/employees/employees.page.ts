import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DecimalPipe } from '@angular/common';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { catchError, of } from 'rxjs';
import { AdminApi, LegalEntitiesApi, LegalEntityDto } from '@core/api/admin.api';
import { SessionStore } from '@core/session/session.store';
import { DepartmentDto, DepartmentsApi, PositionDto, PositionsApi } from '@core/api/hr-people.api';
import { EmployeeDocumentsComponent } from '../../components/employee-documents/employee-documents.component';
import { TerminateDialogComponent } from '../../components/terminate-dialog/terminate-dialog.component';
import { EmployeeLifeComponent } from '../../components/employee-life/employee-life.component';
import { RouterLink } from '@angular/router';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { EMPLOYEE_STATUSES, EmployeeDto, EmployeeSummary, EmployeesApi } from '@core/api/hr.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';

const CONTRACT_TYPES = [{ value: 'permanent', label: 'Permanent' }, { value: 'fixedTerm', label: 'Fixed term' }, { value: 'probation', label: 'Probation' }, { value: 'partTime', label: 'Part time' }, { value: 'contractor', label: 'Contractor' }];

/** Employees: salary structure, join date and status; end-of-service accrual under the employing entity's country rule is shown on the record. */
@Component({
  selector: 'erp-employees',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatusTagComponent, EmployeeLifeComponent, EmployeeDocumentsComponent, TerminateDialogComponent, NzToolTipModule, RevealDirective, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, DecimalPipe, StatCardComponent],
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
    .emp-name { font-weight: 600; color: var(--erp-color-text-primary); font-size: 13px; }
  `],
  template: `
    <h1 class="erp-page-title">Employees</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="employee-stats">
      <erp-stat-card label="Total employees"
                     [value]="staff().total"
                     format="number"
                     [loading]="staffLoading()"
                     [caption]="stats().capped ? 'figures below cover the first ' + staff().items.length : 'everyone in this workspace'"
                     [tone]="0" />
      <erp-stat-card label="Active staff"
                     [value]="stats().active"
                     format="number"
                     [loading]="staffLoading()"
                     [caption]="stats().onLeave ? stats().onLeave + ' more on leave' : 'at work, not on leave'"
                     [tone]="1" />
      @if (canSeePay) {
        <erp-stat-card label="Monthly gross pay"
                       [value]="stats().totalGross"
                       format="money"
                       [loading]="staffLoading()"
                       caption="current salaries, excluding leavers"
                       [tone]="2" />
      }
      <erp-stat-card label="Departments"
                     [value]="stats().departmentsCount"
                     format="number"
                     [loading]="staffLoading()"
                     caption="with staff assigned"
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
            <erp-status [value]="row.status" />
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
        <div drawerActions style="display:flex;gap:8px;flex-wrap:wrap;margin:8px 0">
        <p data-testid="emp-summary" style="flex-basis:100%;margin:0">@if (canSeePay) { Gross <b>{{ currency() }} {{ e.grossMonthly | number: '1.2-2' }}</b> {{ 'ui.hr.aMonth' | translate: 'a month' }} · }{{ 'ui.hr.service' | translate: 'service' }} {{ e.serviceYears.toFixed(1) }} {{ 'ui.hr.years' | translate: 'years' }}@if (canSeePay) { · {{ 'ui.hr.eosAccrued' | translate: 'end-of-service accrued' }} <b>{{ currency() }} {{ e.gratuityAccrued | number: '1.2-2' }}</b> }</p>
          <button nz-button (click)="lifeFor.set(e)" data-testid="emp-life"><span nz-icon nzType="audit"></span>History and tasks</button>
          <button nz-button (click)="docsFor.set(e)" data-testid="emp-docs"><span nz-icon nzType="file-text"></span>Documents</button>
          @if (e.status !== 'terminated') {
            <button *erpHasPermission="'hr.employees.manage'" nz-button nzDanger (click)="terminateFor.set(e)" data-testid="emp-terminate">End employment</button>
          } @else {
            <button *erpHasPermission="'hr.payroll.manage'" nz-button (click)="terminateFor.set(e)" data-testid="emp-settlement">Final settlement</button>
          }
        </div>
      }
    </erp-record-drawer>
    @if (lifeFor(); as l) { <erp-employee-life [employeeId]="l.id" [employeeName]="l.fullName" [open]="true" (closed)="lifeFor.set(null)" /> }
    @if (docsFor(); as d) { <erp-employee-documents [employeeId]="d.id" [employeeName]="d.fullName" [open]="true" (closed)="docsFor.set(null)" /> }
    @if (terminateFor(); as t) { <erp-terminate-dialog [employeeId]="t.id" [employeeName]="t.fullName" [open]="true" [endedOn]="t.status === 'terminated' ? (t.terminationDate ?? null) : null" (closed)="terminateFor.set(null)" (changed)="onTerminated()" /> }`,
})
export class EmployeesPage {
  private readonly api = inject(EmployeesApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  readonly statuses = EMPLOYEE_STATUSES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'number' });
  readonly state = new MasterDataState<EmployeeDto>({
    save: (id, body) => this.api.save(id, body), reload: () => this.reloadAll(), message: inject(NzMessageService), entityName: 'Employee',
  });

  /** The figures on the cards come from this list, not from the page below, so paging, search and filters do not change them. The API returns at most 200 rows. */
  readonly staff = signal<{ items: EmployeeSummary[]; total: number }>({ items: [], total: 0 });
  readonly staffLoading = signal(true);

  readonly stats = computed(() => {
    const { items, total } = this.staff();
    const employed = items.filter((e) => e.status !== 'terminated');
    return {
      active: items.filter((e) => e.status === 'active').length,
      onLeave: items.filter((e) => e.status === 'onLeave').length,
      totalGross: employed.reduce((sum, e) => sum + (e.grossMonthly || 0), 0),
      departmentsCount: new Set(employed.map((e) => e.department).filter(Boolean)).size,
      capped: total > items.length,
    };
  });

  constructor() { this.loadStaff(); }

  private loadStaff(): void {
    this.api.list({ page: 1, pageSize: 200, sort: 'number' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[], total: 0 }))).subscribe((p) => { this.staff.set({ items: p.items, total: p.total ?? p.items.length }); this.staffLoading.set(false); });
  }
  private reloadAll(): void { this.list.reload(); this.loadStaff(); }

  initials(name: string): string {
    return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'E';
  }
  private readonly session = inject(SessionStore);
  /** Without hr.salary.read the API sends no pay figures, so the page shows none instead of zeros. */
  readonly canSeePay = this.session.hasPermission('hr.salary.read');
  readonly columns: TableColumn<EmployeeSummary>[] = [
    { key: 'number', label: 'No.', sortField: 'number', width: '110px' },
    { key: 'fullName', label: 'Name', sortField: 'fullName' },
    { key: 'designation', label: 'Designation' },
    { key: 'department', label: 'Department' },
    { key: 'joinDate', label: 'Joined', sortField: 'joinDate', width: '120px' },
    ...(this.canSeePay ? [{ key: 'grossMonthly', label: 'Gross / month', align: 'right', value: (e) => e.grossMonthly?.toLocaleString() ?? '' } as TableColumn<EmployeeSummary>] : []),
    { key: 'status', label: 'Status', sortField: 'status', width: '110px', filterField: 'status', filterOptions: EMPLOYEE_STATUSES },
  ];
  private readonly departmentPage = toSignal(inject(DepartmentsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as DepartmentDto[] }))), { initialValue: { items: [] as DepartmentDto[] } });
  private readonly positionPage = toSignal(inject(PositionsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as PositionDto[] }))), { initialValue: { items: [] as PositionDto[] } });
  private readonly userList = toSignal(inject(AdminApi).users().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly docsFor = signal<EmployeeDto | null>(null);
  readonly lifeFor = signal<EmployeeDto | null>(null);
  readonly terminateFor = signal<EmployeeDto | null>(null);
  onTerminated(): void { this.reloadAll(); this.state.close(); }

  readonly fields = computed<FieldSpec[]>(() => {
    const pay: FieldSpec[] = !this.canSeePay ? [] : [
      { key: 'basicSalary', label: 'Basic salary (monthly)', type: 'number', required: true, min: 0, hint: 'Also the end-of-service base.' },
      { key: 'housingAllowance', label: 'Housing allowance', type: 'number', min: 0, default: 0 },
      { key: 'transportAllowance', label: 'Transport allowance', type: 'number', min: 0, default: 0 },
      { key: 'otherAllowances', label: 'Other allowances', type: 'number', min: 0, default: 0 },
      { key: 'recurringDeductions', label: 'Recurring deductions', type: 'number', min: 0, default: 0, hint: 'A fixed monthly deduction. Staff loans have their own screen.' },
      { key: 'iban', label: 'IBAN', type: 'text', maxLength: 50 },
      { key: 'bankName', label: 'Bank name', type: 'text', maxLength: 150 },
      { key: 'labourCardNumber', label: 'Labour card / MOHRE personal no.', type: 'text', maxLength: 30, hint: 'UAE WPS: the 14-digit number used in the salary file.' },
      { key: 'bankRoutingCode', label: 'Bank routing code', type: 'text', maxLength: 20, hint: 'UAE WPS: routing code of the employee\'s bank.' },
    ];
    const self = this.state.editing()?.id;
    return [
      { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, readonlyOnEdit: true, options: this.entityPage().items.map((e) => ({ value: e.id, label: e.name })) },
      { key: 'number', label: 'Employee no.', type: 'text', maxLength: 30, hint: 'Leave blank to auto-number.' },
      { key: 'fullName', label: 'Full name', type: 'text', required: true, maxLength: 200 },
      { key: 'designation', label: 'Designation', type: 'text', required: true, maxLength: 150 },
      { key: 'departmentId', label: 'Department', type: 'select', options: this.departmentPage().items.filter((d) => d.isActive).map((d) => ({ value: d.id, label: `${d.code} · ${d.name}` })) },
      { key: 'department', label: 'Department (free text)', type: 'text', maxLength: 100, hint: 'Used when no department is chosen above.' },
      { key: 'positionId', label: 'Position', type: 'select', options: this.positionPage().items.filter((p) => p.isActive).map((p) => ({ value: p.id, label: `${p.code} · ${p.title}` })) },
      { key: 'managerEmployeeId', label: 'Line manager', type: 'select', options: this.staff().items.filter((e) => e.id !== self && e.status !== 'terminated').map((e) => ({ value: e.id, label: `${e.number} · ${e.fullName}` })) },
      { key: 'userId', label: 'Sign-in account', type: 'select', hint: 'Links this person to a user so they can use self-service.', options: this.userList().map((u) => ({ value: u.id, label: `${u.fullName} (${u.email})` })) },
      { key: 'joinDate', label: 'Join date', type: 'date', required: true },
      { key: 'status', label: 'Status', type: 'select', options: EMPLOYEE_STATUSES, default: 'active' },
      { key: 'terminationDate', label: 'Termination date', type: 'date', hidden: (v) => v['status'] !== 'terminated' },
      { key: 'contractType', label: 'Contract type', type: 'select', options: CONTRACT_TYPES },
      { key: 'probationEndDate', label: 'Probation ends', type: 'date' },
      { key: 'contractEndDate', label: 'Contract ends', type: 'date' },
      { key: 'noticePeriodDays', label: 'Notice period (days)', type: 'number', min: 0, max: 365 },
      { key: 'email', label: 'Email', type: 'text', maxLength: 200 },
      { key: 'phone', label: 'Phone', type: 'text', maxLength: 50 },
      { key: 'nationality', label: 'Nationality', type: 'text', maxLength: 100 },
      { key: 'dateOfBirth', label: 'Date of birth', type: 'date' },
      { key: 'gender', label: 'Gender', type: 'select', options: [{ value: 'Female', label: 'Female' }, { value: 'Male', label: 'Male' }] },
      { key: 'maritalStatus', label: 'Marital status', type: 'select', options: [{ value: 'Single', label: 'Single' }, { value: 'Married', label: 'Married' }, { value: 'Divorced', label: 'Divorced' }, { value: 'Widowed', label: 'Widowed' }] },
      { key: 'address', label: 'Address', type: 'text', maxLength: 500 },
      { key: 'emergencyContactName', label: 'Emergency contact', type: 'text', maxLength: 150 },
      { key: 'emergencyContactPhone', label: 'Emergency phone', type: 'text', maxLength: 50 },
      ...pay,
    ];
  });

  filterStatus(s: string | null): void { this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []); }
  open(row: EmployeeSummary): void { this.api.detail(row.id).subscribe((e) => this.state.edit(e)); }
}
