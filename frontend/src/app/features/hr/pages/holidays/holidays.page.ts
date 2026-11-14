import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { HolidayDto, HolidaysApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { deleteWithConfirm } from '@shared/utils/delete-with-confirm';
import { MasterDataState } from '@shared/utils/master-data-state';

/** Public holidays: not counted as leave days. */
@Component({
  selector: 'erp-holidays',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Public holidays</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search holidays" emptyText="No public holidays yet" (rowClick)="edit($event)">
      <button toolbar *erpHasPermission="'hr.leave.approve'" nz-button nzType="primary" (click)="state.create()" data-testid="holiday-new"><span nz-icon nzType="plus"></span>New holiday</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit holiday' : 'New holiday'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" writePermission="hr.leave.approve" testId="holiday-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.leave.approve'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="holiday-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class HolidaysPage {
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  /** Opening a row edits it, so only someone who may change holidays can open one. */
  readonly canManage = computed(() => this.session.hasPermission('hr.leave.approve'));
  private readonly holidaysApi = inject(HolidaysApi);
  readonly list = createPagedList((q) => this.holidaysApi.list(q), { sort: 'date' });
  readonly state = new MasterDataState<HolidayDto>({
    save: (id, body) => (id ? this.holidaysApi.update(id, body) : this.holidaysApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Holiday',
  });
  readonly columns: TableColumn<HolidayDto>[] = [{ key: 'date', label: 'Date', sortField: 'date', width: '140px' }, { key: 'name', label: 'Name' }];
  readonly fields: FieldSpec[] = [
    { key: 'date', label: 'Date', type: 'date', required: true },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
  ];

  edit(row: HolidayDto): void { if (this.canManage()) this.state.edit(row); }

  remove(id: string): void {
    deleteWithConfirm(this.modal, this.message, {
      title: 'Delete this holiday?', remove: () => this.holidaysApi.delete(id), entity: 'Holiday', name: this.state.editing()?.name,
      failure: 'Could not delete the holiday', done: () => { this.state.close(); this.list.reload(); },
    });
  }
}
