import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { LISTING_KINDS, ListingDto, ListingsApi, RealEstateLookups, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';

/** Properties and units offered for rent or sale: draft, publish, close. */
@Component({
  selector: 'erp-listings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Listings</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search title or channel" emptyText="No listings yet" (rowClick)="state.edit($event)">
      <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All statuses" [ngModel]="null" (ngModelChange)="filterStatus($event)" data-testid="listing-status-filter">
        @for (s of statuses; track s) { <nz-option [nzValue]="s" [nzLabel]="humanize(s)"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'realestate.crm.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="listing-new"><span nz-icon nzType="plus"></span>New listing</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Listing' : 'New listing'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="listing-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as l) {
        <div drawerActions style="display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-bottom:16px" data-testid="listing-actions">
          <erp-status-tag [status]="l.status" />
          <ng-container *erpHasPermission="'realestate.crm.manage'">
            @if (l.status === 'draft') { <button nz-button nzSize="small" nzType="primary" (click)="setStatus(l, 'published')" data-testid="listing-publish">Publish</button> }
            @if (l.status === 'published') {
              <button nz-button nzSize="small" (click)="setStatus(l, 'closed')" data-testid="listing-close">Close</button>
              <button nz-button nzSize="small" (click)="setStatus(l, 'draft')">Unpublish</button>
            }
            @if (l.status === 'closed') { <button nz-button nzSize="small" (click)="setStatus(l, 'draft')">Reopen as draft</button> }
          </ng-container>
        </div>
      }
    </erp-record-drawer>`,
})
export class ListingsPage {
  private readonly api = inject(ListingsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly humanize = humanize;
  readonly statuses = ['draft', 'published', 'closed'];
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-listedOn' });
  readonly state = new MasterDataState<ListingDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Listing',
  });
  readonly columns: TableColumn<ListingDto>[] = [
    { key: 'title', label: 'Title' },
    { key: 'kind', label: 'Kind', value: (l) => humanize(l.kind), width: '110px' },
    { key: 'price', label: 'Price', sortField: 'price', value: (l) => l.price.toLocaleString(), width: '130px' },
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
      next: () => { this.state.close(); this.message.success('Listing updated'); this.list.reload(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the listing'),
    });
  }
}
