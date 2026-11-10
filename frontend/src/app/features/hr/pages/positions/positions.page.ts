import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { problemMessage } from '@core/http/problem-details';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { doneText } from '@core/format/done-text';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { PositionDto, PositionsApi } from '@core/api/hr-people.api';

/** Job positions (title and grade) employees can be assigned to. */
@Component({
  selector: 'erp-positions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Positions</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search positions" emptyText="No positions yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="position-new"><span nz-icon nzType="plus"></span>New position</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit position' : 'New position'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="position-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.employees.manage'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="position-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class PositionsPage {
  private readonly confirm = inject(NzModalService);
  private readonly toast = inject(NzMessageService);
  private readonly api = inject(PositionsApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<PositionDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Position',
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

  remove(id: string): void {
    const deletedName = this.state.editing()?.title;
    this.confirm.confirm({
      nzTitle: 'Delete this position?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.api.delete(id).subscribe({
        next: () => { this.toast.success(doneText('deleted', 'Position', deletedName)); this.state.close(); this.list.reload(); resolve(); },
        error: (e) => { this.toast.error(problemMessage(e, 'Could not delete the position')); resolve(); },
      })),
    });
  }
}
