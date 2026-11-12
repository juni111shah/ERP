import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { createPagedList } from '@core/api/paged-list';
import { PropertiesApi, PropertyDto, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { propertyFields } from '../../realestate-forms';
import { propertyStats } from '../../realestate-stats';

/** Properties: KPI cards, the paged list and the new-property drawer; a row opens the property's detail page. */
@Component({
  selector: 'erp-properties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzTagModule, StatusTagComponent, StatCardComponent, TranslatePipe, NzToolTipModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  templateUrl: './properties.page.html',
})
export class PropertiesPage {
  private readonly propertiesApi = inject(PropertiesApi);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly humanize = humanize;
  readonly list = createPagedList((q) => this.propertiesApi.list(q), { sort: 'code' });
  readonly stats = computed(() => propertyStats(this.list.items(), this.list.total()));
  readonly fields = propertyFields(inject(RealEstateLookups));
  readonly state = new MasterDataState<PropertyDto>({
    save: (_, body) => this.propertiesApi.create(body), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Property',
  });
  readonly columns: TableColumn<PropertyDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Property', sortField: 'name' },
    { key: 'propertyType', label: 'Type', sortField: 'propertyType', value: (p) => humanize(p.propertyType) },
    { key: 'region', label: 'Region', sortField: 'region' },
    { key: 'community', label: 'Community', sortField: 'community' },
    { key: 'totalUnits', label: 'Units', align: 'right' },
    { key: 'indicativeValue', label: 'Indicative value', sortField: 'indicativeValue', align: 'right', value: (p) => p.indicativeValue.toLocaleString() },
    { key: 'isActive', label: 'Status', width: '100px' },
  ];

  open(p: PropertyDto): void { void this.router.navigate(['/real-estate/properties', p.id]); }
}
