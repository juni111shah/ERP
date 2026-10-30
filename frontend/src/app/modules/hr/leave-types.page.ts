import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { LeaveTypeDto, LeaveTypesApi } from './hr.api';

/** Kinds of leave: paid or unpaid, yearly entitlement, carry-forward cap and whether a request needs approval. */
@Component({
  selector: 'erp-leave-types',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Leave types</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search leave types" emptyText="No leave types yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'hr.leave.approve'" nz-button nzType="primary" (click)="state.create()" data-testid="leave-type-new"><span nz-icon nzType="plus"></span>New leave type</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit leave type' : 'New leave type'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="leave-type-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class LeaveTypesPage {
  private readonly api = inject(LeaveTypesApi);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<LeaveTypeDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Leave type',
  });
  readonly columns: TableColumn<LeaveTypeDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '110px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'isPaid', label: 'Paid', value: (t) => (t.isPaid ? 'Paid' : 'Unpaid'), width: '90px' },
    { key: 'annualDays', label: 'Days a year', value: (t) => (t.annualDays > 0 ? String(t.annualDays) : 'No limit'), width: '120px' },
    { key: 'carryForwardCap', label: 'Carry forward', value: (t) => String(t.carryForwardCap), width: '120px' },
    { key: 'requiresApproval', label: 'Approval', value: (t) => (t.requiresApproval ? 'Needed' : 'Automatic'), width: '110px' },
    { key: 'isActive', label: 'Active', value: (t) => (t.isActive ? 'Yes' : 'No'), width: '80px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'isPaid', label: 'Paid leave', type: 'switch', default: true, hint: 'Unpaid leave is deducted from salary like an unpaid absence.' },
    { key: 'annualDays', label: 'Days a year', type: 'number', min: 0, max: 366, default: 0, hint: '0 = no yearly limit.' },
    { key: 'carryForwardCap', label: 'Carry forward (max days)', type: 'number', min: 0, max: 366, default: 0 },
    { key: 'requiresApproval', label: 'Needs approval', type: 'switch', default: true },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}
