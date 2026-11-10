import { doneText } from '@core/format/done-text';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { PartyGroupDto, PartyGroupsApi, yesNo } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RevealDirective } from '@shared/directives/reveal.directive';

@Component({
  selector: 'erp-party-groups',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzModalModule],
  template: `
    <h1 class="erp-page-title">Party groups</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No party groups yet" (rowClick)="state.edit($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'actions') {
          <button *erpHasPermission="'core.partygroups.write'" nz-button nzDanger nzSize="small" type="button" (click)="$event.stopPropagation(); remove(row)" [attr.data-testid]="'pg-delete-' + row.code">Delete</button>
        } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'core.partygroups.write'" nz-button nzType="primary" (click)="state.create()" data-testid="pg-new"><span nz-icon nzType="plus"></span> New party group</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit party group' : 'New party group'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="pg-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class PartyGroupsPage {
  private readonly api = inject(PartyGroupsApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<PartyGroupDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Party group',
  });
  readonly columns: TableColumn<PartyGroupDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '160px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'isActive', label: 'Active', value: (g) => yesNo(g.isActive) },
    { key: 'actions', label: '', width: '100px', filter: false, value: () => '' },
  ];
  private readonly modal = inject(NzModalService);
  private readonly toast = inject(NzMessageService);
  remove(g: PartyGroupDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${g.name}?`, nzContent: 'A group that parties still belong to cannot be deleted.', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(g.id).subscribe({ next: () => { this.toast.success(doneText('deleted', 'Party group', g.name)); this.list.reload(); }, error: (e) => this.toast.error(problemMessage(e, 'Could not delete the party group')) }),
    });
  }
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 30 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}
