import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { problemMessage } from '../../core/http/problem-details';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { doneText } from '../../core/format/done-text';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { HolidayDto, HolidaysApi } from './hr.api';

/** Public holidays: not counted as leave days. */
@Component({
  selector: 'erp-holidays',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Public holidays</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search holidays" emptyText="No public holidays yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'hr.leave.approve'" nz-button nzType="primary" (click)="state.create()" data-testid="holiday-new"><span nz-icon nzType="plus"></span>New holiday</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit holiday' : 'New holiday'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="holiday-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.leave.approve'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="holiday-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class HolidaysPage {
  private readonly confirm = inject(NzModalService);
  private readonly toast = inject(NzMessageService);
  private readonly api = inject(HolidaysApi);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'date' });
  readonly state = new MasterDataState<HolidayDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Holiday',
  });
  readonly columns: TableColumn<HolidayDto>[] = [{ key: 'date', label: 'Date', sortField: 'date', width: '140px' }, { key: 'name', label: 'Name' }];
  readonly fields: FieldSpec[] = [
    { key: 'date', label: 'Date', type: 'date', required: true },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
  ];

  remove(id: string): void {
    const deletedName = this.state.editing()?.name;
    this.confirm.confirm({
      nzTitle: 'Delete this holiday?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.api.delete(id).subscribe({
        next: () => { this.toast.success(doneText('deleted', 'Holiday', deletedName)); this.state.close(); this.list.reload(); resolve(); },
        error: (e) => { this.toast.error(problemMessage(e, 'Could not delete the holiday')); resolve(); },
      })),
    });
  }
}
