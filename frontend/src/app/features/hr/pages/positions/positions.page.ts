import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { PositionDto, PositionsApi } from '@core/api/hr-people.api';
import { createPagedList } from '@core/api/paged-list';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { deleteWithConfirm } from '@shared/utils/delete-with-confirm';
import { MasterDataState } from '@shared/utils/master-data-state';

/** Job positions (title and grade) employees can be assigned to. */
@Component({
  selector: 'erp-positions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Positions</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search positions" emptyText="No positions yet" (rowClick)="edit($event)">
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="position-new"><span nz-icon nzType="plus"></span>New position</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit position' : 'New position'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" writePermission="hr.employees.manage" testId="position-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.employees.manage'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="position-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class PositionsPage {
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  /** Opening a row edits it, so only someone who may change positions can open one. */
  readonly canManage = computed(() => this.session.hasPermission('hr.employees.manage'));
  private readonly positionsApi = inject(PositionsApi);
  readonly list = createPagedList((q) => this.positionsApi.list(q), { sort: 'code' });
  readonly state = new MasterDataState<PositionDto>({
    save: (id, body) => (id ? this.positionsApi.update(id, body) : this.positionsApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Position',
  });
  readonly columns: TableColumn<PositionDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'title', label: 'Title', sortField: 'title' },
    { key: 'grade', label: 'Grade', width: '120px' },
    { key: 'isActive', label: 'Active', value: (p) => (p.isActive ? 'Yes' : 'No'), width: '80px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 150 },
    { key: 'grade', label: 'Grade', type: 'text', maxLength: 30 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];

  edit(row: PositionDto): void { if (this.canManage()) this.state.edit(row); }

  remove(id: string): void {
    deleteWithConfirm(this.modal, this.message, {
      title: 'Delete this position?', remove: () => this.positionsApi.delete(id), entity: 'Position', name: this.state.editing()?.title,
      failure: 'Could not delete the position', done: () => { this.state.close(); this.list.reload(); },
    });
  }
}
