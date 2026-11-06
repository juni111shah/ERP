import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { problemMessage } from '../../core/http/problem-details';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { doneText } from '../../core/format/done-text';
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
  private readonly confirm = inject(NzModalService);
  private readonly toast = inject(NzMessageService);
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

  remove(id: string): void {
    const deletedName = this.state.editing()?.name;
    this.confirm.confirm({
      nzTitle: 'Delete this department?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.api.delete(id).subscribe({
        next: () => { this.toast.success(doneText('deleted', 'Department', deletedName)); this.state.close(); this.list.reload(); resolve(); },
        error: (e) => { this.toast.error(problemMessage(e, 'Could not delete the department')); resolve(); },
      })),
    });
  }
}
