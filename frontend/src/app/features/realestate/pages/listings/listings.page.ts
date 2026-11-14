import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { createPagedList } from '@core/api/paged-list';
import { LISTING_KINDS, ListingDto, ListingsApi, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { MasterDataState } from '@shared/utils/master-data-state';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { listingStats } from '../../realestate-stats';

const LISTING_STATUSES = ['draft', 'published', 'closed'];

/** Properties and units offered for rent or sale: draft, publish, close. */
@Component({
  selector: 'erp-listings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, TranslatePipe, NzToolTipModule, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, DecimalPipe, StatCardComponent],
  templateUrl: './listings.page.html',
})
export class ListingsPage {
  private readonly listingsApi = inject(ListingsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  private readonly session = inject(SessionStore);
  /** A row opens the edit drawer (with publish / close), so only people who may manage listings can open it. */
  readonly canManage = computed(() => this.session.hasPermission('realestate.crm.manage'));
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly humanize = humanize;
  readonly statuses = LISTING_STATUSES;
  readonly list = createPagedList((q) => this.listingsApi.list(q), { sort: '-listedOn' });
  readonly state = new MasterDataState<ListingDto>({
    save: (id, body) => (id ? this.listingsApi.update(id, body) : this.listingsApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Listing',
  });
  readonly stats = computed(() => listingStats(this.list.items()));
  readonly columns: TableColumn<ListingDto>[] = [
    { key: 'title', label: 'Title' },
    { key: 'kind', label: 'Kind', value: (l) => humanize(l.kind), width: '110px' },
    { key: 'price', label: 'Price', sortField: 'price', align: 'right', value: (l) => l.price.toLocaleString(), width: '150px' },
    { key: 'channels', label: 'Channels' },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: LISTING_STATUSES },
    { key: 'listedOn', label: 'Listed', sortField: 'listedOn', width: '120px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'propertyId', label: 'Property', type: 'lookup', lookup: this.lookups.property, required: true, readonlyOnEdit: true },
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: this.lookups.unit, hint: 'Leave blank to list the whole property.' },
    { key: 'kind', label: 'Kind', type: 'select', options: LISTING_KINDS, default: 'rent', required: true },
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 200 },
    { key: 'price', label: 'Price', type: 'number', min: 0, required: true, hint: 'Annual rent, or the sale price.' },
    { key: 'channels', label: 'Channels', type: 'text', maxLength: 300, hint: 'For example Bayut, Property Finder, website.' },
    { key: 'description', label: 'Description', type: 'textarea', maxLength: 4000 },
  ];

  /** "Bayut, website" -> ["Bayut", "website"]. */
  channelList(channels?: string): string[] {
    if (!channels) return [];
    return channels.split(',').map((c) => c.trim()).filter(Boolean);
  }

  filterStatus(status: string | null): void { this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []); }

  setStatus(l: ListingDto, status: string): void {
    this.listingsApi.setStatus(l.id, status).subscribe({
      next: () => { this.state.close(); this.message.success(doneText('updated', 'Listing', l.title)); this.list.reload(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the listing'),
    });
  }
}
