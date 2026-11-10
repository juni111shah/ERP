import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, of } from 'rxjs';
import { AdminApi, LegalEntitiesApi } from '@core/api/admin.api';
import { DepartmentsApi, PositionsApi } from '@core/api/hr-people.api';
import { EMPLOYEE_STATUSES, EmployeeDto, EmployeeSummary, EmployeesApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { itemsSignal } from '@shared/utils/items-signal';
import { MasterDataState } from '@shared/utils/master-data-state';
import { EmployeeDocumentsComponent } from '../../components/employee-documents/employee-documents.component';
import { EmployeeLifeComponent } from '../../components/employee-life/employee-life.component';
import { TerminateDialogComponent } from '../../components/terminate-dialog/terminate-dialog.component';
import { employeeFields, employeePayFields, staffStats } from '../../hr-employees';
import { initials } from '../../hr-format';

/** Employees: salary structure, join date and status; end-of-service accrual under the employing entity's country rule is shown on the record. */
@Component({
  selector: 'erp-employees',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatusTagComponent, EmployeeLifeComponent, EmployeeDocumentsComponent, TerminateDialogComponent, NzToolTipModule, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, DecimalPipe, StatCardComponent],
  styleUrl: './employees.page.scss',
  templateUrl: './employees.page.html',
})
export class EmployeesPage {
  private readonly employeesApi = inject(EmployeesApi);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly entityPage = itemsSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }));
  private readonly departmentPage = itemsSignal(inject(DepartmentsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  private readonly positionPage = itemsSignal(inject(PositionsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  private readonly userList = toSignal(inject(AdminApi).users().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly statuses = EMPLOYEE_STATUSES;
  readonly list = createPagedList((q) => this.employeesApi.list(q), { sort: 'number' });
  readonly state = new MasterDataState<EmployeeDto>({
    save: (id, body) => this.employeesApi.save(id, body), reload: () => this.reloadAll(), message: inject(NzMessageService), entityName: 'Employee',
  });

  /** The figures on the cards come from this list, not from the page below, so paging, search and filters do not change them. The API returns at most 200 rows. */
  readonly staff = signal<{ items: EmployeeSummary[]; total: number }>({ items: [], total: 0 });
  readonly staffLoading = signal(true);
  readonly stats = computed(() => { const { items, total } = this.staff(); return staffStats(items, total); });

  readonly initials = initials;
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
  readonly docsFor = signal<EmployeeDto | null>(null);
  readonly lifeFor = signal<EmployeeDto | null>(null);
  readonly terminateFor = signal<EmployeeDto | null>(null);

  readonly fields = computed<FieldSpec[]>(() => {
    const pay = this.canSeePay ? employeePayFields() : [];
    const lists = { entities: this.entityPage().items, departments: this.departmentPage().items, positions: this.positionPage().items, staff: this.staff().items, users: this.userList() };
    return employeeFields(lists, this.state.editing()?.id, pay);
  });

  constructor() { this.loadStaff(); }

  onTerminated(): void { this.reloadAll(); this.state.close(); }
  filterStatus(s: string | null): void { this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []); }
  open(row: EmployeeSummary): void { this.employeesApi.detail(row.id).subscribe((e) => this.state.edit(e)); }

  private loadStaff(): void {
    this.employeesApi.list({ page: 1, pageSize: 200, sort: 'number' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[], total: 0 }))).subscribe((p) => { this.staff.set({ items: p.items, total: p.total ?? p.items.length }); this.staffLoading.set(false); });
  }
  private reloadAll(): void { this.list.reload(); this.loadStaff(); }
}
