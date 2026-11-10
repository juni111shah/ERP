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
  templateUrl: './party-groups.page.html',
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
