import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ACCOUNT_TYPES, AccountDto, AccountsApi, yesNo } from '../../../core/api/admin.api';
import { createPagedList } from '../../../core/api/paged-list';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { HasPermissionDirective } from '../../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../../shared/paged-table.component';
import { RevealDirective } from '../../../shared/reveal.directive';

/** The flat, paged chart of accounts (the "List view" of the accounts screen). Loads only while it is shown. */
@Component({
  selector: 'erp-accounts-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzToolTipModule, RevealDirective, TranslatePipe],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" [searchPlaceholder]="'coa.searchAccounts' | translate: 'Search code or name'" [emptyText]="'ui.coa.noAccountsYet' | translate: 'No accounts yet'" (rowClick)="edit.emit($event)">
      <button toolbar *erpHasPermission="'finance.accounts.write'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'coa.newAccount' | translate: 'New account'" nz-tooltip [nzTooltipTitle]="'coa.newAccount' | translate: 'New account'" nzType="primary" (click)="create.emit()" data-testid="account-new"><span nz-icon nzType="plus"></span>New account</button>
    </erp-paged-table>`,
})
export class AccountsListComponent {
  private readonly api = inject(AccountsApi);
  readonly create = output<void>();
  readonly edit = output<AccountDto>();
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code', pageSize: 50 });
  readonly columns: TableColumn<AccountDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type', value: (a) => ACCOUNT_TYPES.find((t) => t.value === a.type)?.label ?? a.type },
    { key: 'isPostable', label: 'Postable', value: (a) => yesNo(a.isPostable) },
    { key: 'isBank', label: 'Bank / cash', value: (a) => yesNo(a.isBank) },
    { key: 'isSystem', label: 'System', value: (a) => yesNo(a.isSystem) },
    { key: 'isActive', label: 'Active', value: (a) => yesNo(a.isActive) },
  ];

  reload(): void { this.list.reload(); }
}
