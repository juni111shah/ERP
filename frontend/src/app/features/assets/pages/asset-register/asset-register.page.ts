import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { BusinessUnitsApi } from '@core/api/admin.api';
import { ASSET_STATUSES, AssetDto, AssetsApi } from '@core/api/assets.api';
import { createPagedList } from '@core/api/paged-list';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { MasterDataState } from '@shared/utils/master-data-state';
import { assetFields, businessUnitLookup } from '../../asset-form';
import { assetStatusLabel } from '../../asset-labels';
import { AssetLookups } from '../../asset-lookups';

/** The fixed asset register: every asset with its cost and book value, and the drawer to add one. */
@Component({
  selector: 'erp-asset-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzIconModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  providers: [AssetLookups],
  templateUrl: './asset-register.page.html',
})
export class AssetRegisterPage {
  private readonly assetsApi = inject(AssetsApi);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
  private readonly unitsApi = inject(BusinessUnitsApi);
  private readonly lookups = inject(AssetLookups);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly categories = this.lookups.categories;
  readonly list = createPagedList((q) => this.assetsApi.list(q), { sort: '-acquisitionDate' });
  readonly state = new MasterDataState<AssetDto>({
    save: (id, body) => this.assetsApi.save(id, body),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Asset',
  });
  readonly columns: TableColumn<AssetDto>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '130px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'category', label: 'Category', value: (a) => this.lookups.category(a.categoryId)?.name ?? '', filter: false },
    { key: 'acquisitionDate', label: 'Acquired', sortField: 'acquisitionDate', filterField: 'acquisitionDate' },
    { key: 'cost', label: 'Cost', align: 'right', sortField: 'cost' },
    { key: 'netBookValue', label: 'Net book value', align: 'right', filter: false },
    { key: 'custodian', label: 'Custodian', sortField: 'custodian' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ASSET_STATUSES },
  ];
  readonly fields = computed(() => assetFields(legalEntityOptions(this.session), defaultLegalEntityId(this.session), this.categories(), businessUnitLookup(this.unitsApi)));

  constructor() {
    this.lookups.loadCategories();
  }

  statusLabel(status: string): string { return assetStatusLabel(status); }
  open(row: AssetDto): void { void this.router.navigate(['/assets/register', row.id]); }
}
