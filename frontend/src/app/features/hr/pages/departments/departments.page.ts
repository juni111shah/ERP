import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { DepartmentDto, DepartmentsApi } from '@core/api/hr-people.api';
import { EmployeesApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { deleteWithConfirm } from '@shared/utils/delete-with-confirm';
import { itemsSignal } from '@shared/utils/items-signal';
import { MasterDataState } from '@shared/utils/master-data-state';
import { departmentOptions, employeeOptions } from '../../hr-options';

/** Departments: a tree (parent department), a head and an optional business unit that payroll cost is split to. */
@Component({
  selector: 'erp-departments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Departments</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search departments" emptyText="No departments yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="dept-new"><span nz-icon nzType="plus"></span>New department</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit department' : 'New department'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="dept-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.employees.manage'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="dept-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class DepartmentsPage {
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);
  private readonly departmentsApi = inject(DepartmentsApi);
  private readonly employeesPage = itemsSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'fullName' }));
  readonly list = createPagedList((q) => this.departmentsApi.list(q), { sort: 'code' });
  readonly state = new MasterDataState<DepartmentDto>({
    save: (id, body) => (id ? this.departmentsApi.update(id, body) : this.departmentsApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Department',
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
    { key: 'parentDepartmentId', label: 'Parent department', type: 'select', options: departmentOptions(this.list.items().filter((d) => d.id !== this.state.editing()?.id)) },
    { key: 'headEmployeeId', label: 'Head of department', type: 'select', options: employeeOptions(this.employeesPage().items) },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);

  remove(id: string): void {
    deleteWithConfirm(this.modal, this.message, {
      title: 'Delete this department?', remove: () => this.departmentsApi.delete(id), entity: 'Department', name: this.state.editing()?.name,
      failure: 'Could not delete the department', done: () => { this.state.close(); this.list.reload(); },
    });
  }
}
