import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { map } from 'rxjs';
import { ACCOUNT_TYPES, AccountDto, AccountsApi, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

@Component({
  selector: 'erp-accounts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Chart of accounts</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No accounts yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'finance.accounts.write'" nz-button nzType="primary" (click)="state.create()" data-testid="account-new"><span nz-icon nzType="plus"></span>New account</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit account' : 'New account'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())" customFieldEntityType="account"
                       [saving]="state.saving()" [errors]="state.errors()" testId="account-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class AccountsPage {
  private readonly api = inject(AccountsApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code', pageSize: 50 });
  readonly state = new MasterDataState<AccountDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Account',
  });
  readonly columns: TableColumn<AccountDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type', value: (a) => ACCOUNT_TYPES.find((t) => t.value === a.type)?.label ?? a.type },
    { key: 'isPostable', label: 'Postable', value: (a) => yesNo(a.isPostable) },
    { key: 'isBank', label: 'Bank / cash', value: (a) => yesNo(a.isBank) },
    { key: 'isSystem', label: 'System', value: (a) => yesNo(a.isSystem) },
    { key: 'isActive', label: 'Active', value: (a) => yesNo(a.isActive) },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'type', label: 'Type', type: 'select', required: true, options: ACCOUNT_TYPES, default: 'asset' },
    { key: 'reportingGroup', label: 'Reporting group', type: 'text', maxLength: 60 },
    {
      key: 'parentId', label: 'Parent account', type: 'lookup',
      lookup: (term) => this.api.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })))),
      currentLabel: (r) => String(r['parentId']),
    },
    { key: 'isPostable', label: 'Postable (not a header)', type: 'switch', default: true },
    { key: 'isBank', label: 'Bank / cash account', type: 'switch', default: false },
    { key: 'currencyCode', label: 'Currency (if not base)', type: 'text', maxLength: 3 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true, hint: 'System accounts keep their code, type and active flag.' },
  ];
}
