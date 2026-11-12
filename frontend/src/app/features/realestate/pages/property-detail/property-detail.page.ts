import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { createPagedList } from '@core/api/paged-list';
import { DESCRIPTION_COLUMNS, PropertiesApi, PropertyDto, PropertySummary, RealEstateLookups, UnitListItem, UnitsApi, humanize } from '@core/api/realestate.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { propertyFields } from '../../realestate-forms';

/** Property detail: KPI cards (spec RE-PROP-03), info and the property's units. */
@Component({
  selector: 'erp-property-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, StatePanelComponent, RecordHistoryComponent, TranslatePipe, RouterLink, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzCardModule, NzDescriptionsModule,
    NzGridModule, NzProgressModule, NzStatisticModule, NzTagModule],
  templateUrl: './property-detail.page.html',
})
export class PropertyDetailPage {
  /** route param :id (read at construction so the paged list can use it immediately) */
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly propertiesApi = inject(PropertiesApi);
  private readonly unitsApi = inject(UnitsApi);
  readonly humanize = humanize;
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly failed = signal(false);
  readonly descriptionColumns = DESCRIPTION_COLUMNS;
  readonly summary = signal<PropertySummary | null>(null);
  readonly fields = propertyFields(inject(RealEstateLookups));
  readonly units = createPagedList((q) => this.unitsApi.list({ ...q, filters: [{ field: 'propertyId', op: 'eq', value: this.id }] }), { sort: 'unitNumber' });
  readonly state = new MasterDataState<PropertyDto>({
    save: (id, body) => this.propertiesApi.update(id!, body), reload: () => this.load(), message: inject(NzMessageService), entityName: 'Property',
  });
  readonly unitColumns: TableColumn<UnitListItem>[] = [
    { key: 'unitNumber', label: 'Unit', sortField: 'unitNumber', width: '110px' },
    { key: 'unitTypeName', label: 'Type' },
    { key: 'floor', label: 'Floor' },
    { key: 'areaSqft', label: 'Area (sq ft)', sortField: 'areaSqft', align: 'right' },
    { key: 'askingRent', label: 'Asking rent', sortField: 'askingRent', align: 'right', value: (u) => u.askingRent.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status' },
    { key: 'tenantName', label: 'Tenant' },
    { key: 'leaseEnds', label: 'Lease ends' },
  ];

  constructor() { this.load(); }

  reload(): void { this.load(); }
  private load(): void { this.failed.set(false); this.propertiesApi.summary(this.id).subscribe({ next: (s) => this.summary.set(s), error: () => this.failed.set(true) }); }
}
