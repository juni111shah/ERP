import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { PartyGroupDto, PartyGroupsApi, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { RevealDirective } from '../../shared/reveal.directive';

@Component({
  selector: 'erp-party-groups',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Party groups</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No party groups yet" (rowClick)="state.edit($event)">
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
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 30 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}
