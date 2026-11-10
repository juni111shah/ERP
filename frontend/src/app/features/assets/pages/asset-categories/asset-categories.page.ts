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
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

/** Asset categories: how long assets of a kind last, how they lose value and which accounts they post to. */
@Component({
  selector: 'erp-asset-categories',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule, NzPopconfirmModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  template: `
    <h1 class="erp-page-title">Asset categories</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No asset categories yet. Add one before you register assets." (rowClick)="edit($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') { <span class="code-pill">{{ row.code }}</span> }
          @case ('isActive') { <erp-status [value]="row.isActive ? 'active' : 'inactive'" /> }
          @case ('actions') {
            <button *erpHasPermission="'assets.categories.manage'" nz-button nzSize="small" nzDanger type="button" data-testid="category-delete"
                    nz-popconfirm nzPopconfirmTitle="Delete this category?" (nzOnConfirm)="remove(row)">Delete</button>
          }
          @default { {{ value }} }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'assets.categories.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="category-new"><span nz-icon nzType="plus"></span>New category</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit asset category' : 'New asset category'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="category-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class AssetCategoriesPage {
  private readonly api = inject(AssetCategoriesApi);
  private readonly accounts = inject(AccountsApi);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  private readonly accountLabels = signal<Record<string, string>>({});
  readonly state = new MasterDataState<AssetCategoryDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: this.message, entityName: 'Asset category',
  });
  readonly columns: TableColumn<AssetCategoryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'method', label: 'Method', value: (c) => DEPRECIATION_METHODS.find((m) => m.value === c.method)?.label ?? c.method },
    { key: 'lifeMonths', label: 'Life (months)', align: 'right', width: '130px' },
    { key: 'isActive', label: 'Active', value: (c) => yesNo(c.isActive) },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];
  private label = (key: string) => (r: Record<string, unknown>) => this.accountLabels()[String(r[key])] ?? String(r[key]);
  readonly fields = computed<FieldSpec[]>(() => {
    const account = (key: string, label: string, required: boolean): FieldSpec => ({ key, label, type: 'lookup', required, lookup: this.accounts.lookup, currentLabel: this.label(key) });
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
    this.accounts.list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({
      next: (r) => this.accountLabels.set(Object.fromEntries(r.items.map((a) => [a.id, `${a.code} · ${a.name}`]))),
      error: () => undefined,
    });
  }

  edit(row: AssetCategoryDto): void { this.state.edit(row); }

  remove(row: AssetCategoryDto): void {
    this.api.delete(row.id).subscribe({
      next: () => { this.message.success(doneText('deleted', 'Asset category', row.name)); this.list.reload(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not delete this category')),
    });
  }
}
