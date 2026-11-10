import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { BusinessUnitsApi } from '@core/api/admin.api';
import { ASSET_STATUSES, AssetCategoriesApi, AssetCategoryDto, AssetDto, AssetsApi } from '@core/api/assets.api';
import { createPagedList } from '@core/api/paged-list';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { assetFields, businessUnitLookup } from '../../asset-form';

@Component({
  selector: 'erp-asset-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzIconModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  templateUrl: './asset-register.page.html',
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
