import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { BusinessUnitsApi } from '../../core/api/admin.api';
import { ASSET_STATUSES, AssetCategoriesApi, AssetCategoryDto, AssetDto, AssetsApi } from '../../core/api/assets.api';
import { createPagedList } from '../../core/api/paged-list';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { defaultLegalEntityId, legalEntityOptions } from '../../shared/entity-options';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { assetFields, businessUnitLookup } from './asset-form';

@Component({
  selector: 'erp-asset-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzIconModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  template: `
    <h1 class="erp-page-title">Asset register</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, name or custodian" emptyText="No assets yet. Add an asset, then capitalise it to start depreciation." (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('number') { <span class="code-pill">{{ row.number }}</span> }
          @case ('status') { <erp-status [value]="row.status" [label]="statusLabel(row.status)" /> }
          @case ('cost') { {{ row.cost | number: '1.2-2' }} }
          @case ('netBookValue') { <b>{{ row.netBookValue | number: '1.2-2' }}</b> }
          @default { {{ value }} }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'assets.register.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="asset-new"><span nz-icon nzType="plus"></span>New asset</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit asset' : 'New asset'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="asset-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class AssetRegisterPage {
  private readonly api = inject(AssetsApi);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
  private readonly units = inject(BusinessUnitsApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly categories = signal<AssetCategoryDto[]>([]);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-acquisitionDate' });
  readonly state = new MasterDataState<AssetDto>({
    save: (id, body) => this.api.save(id, body),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Asset',
  });
  readonly columns: TableColumn<AssetDto>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '130px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'category', label: 'Category', value: (a) => this.categoryName(a.categoryId), filter: false },
    { key: 'acquisitionDate', label: 'Acquired', sortField: 'acquisitionDate', filterField: 'acquisitionDate' },
    { key: 'cost', label: 'Cost', align: 'right', sortField: 'cost' },
    { key: 'netBookValue', label: 'Net book value', align: 'right', filter: false },
    { key: 'custodian', label: 'Custodian', sortField: 'custodian' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ASSET_STATUSES },
  ];
  readonly fields = computed(() => assetFields(legalEntityOptions(this.session), defaultLegalEntityId(this.session), this.categories(), businessUnitLookup(this.units)));

  constructor() {
    inject(AssetCategoriesApi).list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({ next: (r) => this.categories.set(r.items), error: () => undefined });
  }

  statusLabel(status: string): string { return ASSET_STATUSES.find((s) => s.value === status)?.label ?? status; }
  private categoryName(id: string): string { return this.categories().find((c) => c.id === id)?.name ?? ''; }
  open(row: AssetDto): void { void this.router.navigate(['/assets/register', row.id]); }
}
