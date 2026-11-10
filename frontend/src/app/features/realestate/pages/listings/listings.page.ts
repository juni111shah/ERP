import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '@core/format/done-text';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { createPagedList } from '@core/api/paged-list';
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { LISTING_KINDS, ListingDto, ListingsApi, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Properties and units offered for rent or sale: draft, publish, close. */
@Component({
  selector: 'erp-listings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, TranslatePipe, NzToolTipModule, RevealDirective, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, DecimalPipe, StatCardComponent],
  templateUrl: './listings.page.html',
})
export class ListingsPage {
  private readonly api = inject(ListingsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly humanize = humanize;
  readonly statuses = ['draft', 'published', 'closed'];
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-listedOn' });
  readonly state = new MasterDataState<ListingDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Listing',
  });

  readonly stats = computed(() => {
    const items = this.list.items();
    const published = items.filter((l) => l.status === 'published').length;
    const rentals = items.filter((l) => l.kind === 'rent').length;
    const totalValue = items.reduce((sum, l) => sum + (l.price || 0), 0);
    return {
      published,
      rentals,
      totalValue,
      countOnPage: items.length,
    };
  });

  channelList(channels?: string): string[] {
    if (!channels) return [];
    return channels.split(',').map((c) => c.trim()).filter(Boolean);
  }
  readonly columns: TableColumn<ListingDto>[] = [
    { key: 'title', label: 'Title' },
    { key: 'kind', label: 'Kind', value: (l) => humanize(l.kind), width: '110px' },
    { key: 'price', label: 'Price', sortField: 'price', align: 'right', value: (l) => l.price.toLocaleString(), width: '150px' },
    { key: 'channels', label: 'Channels' },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: ['draft', 'published', 'closed'] },
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

  filterStatus(status: string | null): void { this.list.setFilters(status ? [{ field: 'status', op: 'eq', value: status }] : []); }

  setStatus(l: ListingDto, status: string): void {
    this.api.setStatus(l.id, status).subscribe({
      next: () => { this.state.close(); this.message.success(doneText('updated', 'Listing', l.title)); this.list.reload(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the listing'),
    });
  }
}
