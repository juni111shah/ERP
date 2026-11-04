import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { DepartmentDto, DepartmentsApi } from './hr-people.api';
import { EmployeeSummary, EmployeesApi } from './hr.api';

/** Departments: a tree (parent department), a head and an optional business unit that payroll cost is split to. */
@Component({
  selector: 'erp-departments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Departments</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search departments" emptyText="No departments yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="dept-new"><span nz-icon nzType="plus"></span>New department</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit department' : 'New department'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="dept-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class DepartmentsPage {
  private readonly api = inject(DepartmentsApi);
  private readonly employeesPage = toSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'fullName' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[] }))), { initialValue: { items: [] as EmployeeSummary[] } });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<DepartmentDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Department',
  });
  readonly columns: TableColumn<DepartmentDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'parent', label: 'Parent', value: (d) => this.list.items().find((x) => x.id === d.parentDepartmentId)?.name ?? '' },
    { key: 'head', label: 'Head', value: (d) => this.employeesPage().items.find((e) => e.id === d.headEmployeeId)?.fullName ?? '' },
    { key: 'isActive', label: 'Active', value: (d) => (d.isActive ? 'Yes' : 'No'), width: '80px' },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'parentDepartmentId', label: 'Parent department', type: 'select', options: this.list.items().filter((d) => d.id !== this.state.editing()?.id).map((d) => ({ value: d.id, label: `${d.code} · ${d.name}` })) },
    { key: 'headEmployeeId', label: 'Head of department', type: 'select', options: this.employeesPage().items.map((e) => ({ value: e.id, label: `${e.number} · ${e.fullName}` })) },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);
}
