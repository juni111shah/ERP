import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { AccountsApi, yesNo } from '@core/api/admin.api';
import { AssetCategoriesApi, AssetCategoryDto, DEPRECIATION_METHODS } from '@core/api/assets.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { loadInto } from '@shared/utils/load-into';
import { MasterDataState } from '@shared/utils/master-data-state';
import { depreciationMethodLabel } from '../../asset-labels';

/** Asset categories: how long assets of a kind last, how they lose value and which accounts they post to. */
@Component({
  selector: 'erp-asset-categories',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule, NzPopconfirmModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  templateUrl: './asset-categories.page.html',
})
export class AssetCategoriesPage {
  private readonly categoriesApi = inject(AssetCategoriesApi);
  private readonly accountsApi = inject(AccountsApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  /** Opening a row edits it, so only someone who may manage categories can open one. */
  readonly canManage = computed(() => this.session.hasPermission('assets.categories.manage'));
  readonly list = createPagedList((q) => this.categoriesApi.list(q), { sort: 'code' });
  /** "code · name" by account id, so an account already on a category shows its name in the drawer. */
  private readonly accountLabels = signal<Record<string, string>>({});
  readonly state = new MasterDataState<AssetCategoryDto>({
    save: (id, body) => (id ? this.categoriesApi.update(id, body) : this.categoriesApi.create(body)),
    reload: () => this.list.reload(), message: this.message, entityName: 'Asset category',
  });
  readonly columns: TableColumn<AssetCategoryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'method', label: 'Method', value: (c) => depreciationMethodLabel(c.method) },
    { key: 'lifeMonths', label: 'Life (months)', align: 'right', width: '130px' },
    { key: 'isActive', label: 'Active', value: (c) => yesNo(c.isActive) },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];
  private label = (key: string) => (r: Record<string, unknown>) => this.accountLabels()[String(r[key])] ?? String(r[key]);
  readonly fields = computed<FieldSpec[]>(() => {
    const account = (key: string, label: string, required: boolean): FieldSpec => ({ key, label, type: 'lookup', required, lookup: this.accountsApi.lookup, currentLabel: this.label(key) });
    return [
      { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 30 },
      { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
      { key: 'lifeMonths', label: 'Useful life (months)', type: 'number', required: true, min: 1, max: 1200, precision: 0, default: 60 },
      { key: 'method', label: 'Method', type: 'select', required: true, options: DEPRECIATION_METHODS, default: 'straightLine' },
      { key: 'decliningRatePercent', label: 'Yearly rate (%)', type: 'number', min: 0, max: 100, hint: 'Declining balance only. Left empty, the double-declining rate for the life applies.', hidden: (v) => v['method'] !== 'decliningBalance' },
      account('assetAccountId', 'Asset account', true),
      account('accumulatedDepreciationAccountId', 'Accumulated depreciation account', true),
      account('depreciationExpenseAccountId', 'Depreciation expense account', true),
      account('disposalGainLossAccountId', 'Gain or loss on disposal account', false),
      { key: 'isActive', label: 'Active', type: 'switch', default: true },
    ];
  });

  constructor() {
    loadInto(this.accountLabels, this.accountsApi.list({ page: 1, pageSize: 200, sort: 'code' }), (r) => Object.fromEntries(r.items.map((a) => [a.id, `${a.code} · ${a.name}`])));
  }

  edit(row: AssetCategoryDto): void { if (this.canManage()) this.state.edit(row); }

  remove(row: AssetCategoryDto): void {
    this.categoriesApi.delete(row.id).subscribe({
      next: () => { this.message.success(doneText('deleted', 'Asset category', row.name)); this.list.reload(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not delete this category')),
    });
  }
}
