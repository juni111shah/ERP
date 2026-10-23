import { RecordHistoryComponent } from '../../shared/record-history.component';
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
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { RecordDrawerComponent } from '../../shared/record-drawer.component';
import { propertyFields } from './properties.page';
import { PropertiesApi, PropertyDto, PropertySummary, RealEstateLookups, UnitListItem, UnitsApi, humanize, DESCRIPTION_COLUMNS } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Property detail: KPI cards (spec RE-PROP-03), info and the property's units. */
@Component({
  selector: 'erp-property-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, RecordHistoryComponent, TranslatePipe, RouterLink, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzCardModule, NzDescriptionsModule,
    NzGridModule, NzProgressModule, NzStatisticModule, NzTagModule],
  template: `
    @if (summary(); as s) {
      <p><a routerLink="/real-estate/properties">← Properties</a></p>
      <h1 class="erp-page-title" data-testid="property-title">{{ s.property.code }} · {{ s.property.name }}
        <button *erpHasPermission="'realestate.properties.manage'" nz-button style="margin-inline-start:16px" (click)="state.edit(s.property)" data-testid="property-edit">Edit</button>
      </h1>
      @if (s.unitsNotFullyRegistered) {
        <nz-tag nzColor="gold" data-testid="units-warning">{{ s.registeredUnits }} {{ 'ui.common.of' | translate: 'of' }} {{ s.property.totalUnits }} {{ 'ui.re.unitsRegistered' | translate: 'units registered' }}</nz-tag>
      }
      <div class="erp-stat-grid" style="margin-top:16px" data-testid="property-kpis">
        <erp-stat-card label="Units registered" [value]="s.registeredUnits + ' / ' + s.property.totalUnits" [tone]="0" />
        <erp-stat-card label="Occupied units" [value]="s.occupiedUnits" format="number" [tone]="1"><nz-progress [nzPercent]="s.occupancyPercent" nzSize="small"></nz-progress></erp-stat-card>
        <erp-stat-card label="Contracted annual rent" [value]="s.contractedAnnualRent" format="number" [tone]="2" />
        <erp-stat-card label="Gross yield" [value]="s.grossYieldPercent" format="percent" [tone]="3" />
      </div>
      <nz-card style="margin-top:16px" nzTitle="Details">
        <nz-descriptions nzBordered [nzColumn]="descriptionColumns">
          <nz-descriptions-item nzTitle="Type">{{ humanize(s.property.propertyType) }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Ownership">{{ humanize(s.property.ownershipType) }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Region">{{ s.property.region }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Community">{{ s.property.community }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Plot number">{{ s.property.plotNumber }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Year built">{{ s.property.yearBuilt }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Indicative value">{{ s.property.indicativeValue.toLocaleString() }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Status">{{ s.property.isActive ? 'Active' : 'Inactive' }}</nz-descriptions-item>
        </nz-descriptions>
      </nz-card>
      <nz-card style="margin-top:16px" nzTitle="Units">
        <erp-paged-table [list]="units" [columns]="unitColumns" [searchable]="false" [clickable]="false" emptyText="No units registered yet">
          <ng-template #cell let-row let-column="column" let-value="value">
            @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
          </ng-template>
        </erp-paged-table>
      </nz-card>
      <nz-card *erpHasPermission="'core.audit.read'" style="margin-top:16px" [nzTitle]="'history.title' | translate: 'History'"><erp-record-history recordType="Property" [recordId]="s.property.id" /></nz-card>
      <erp-record-drawer title="Edit property" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())" customFieldEntityType="realestate.property"
                         [saving]="state.saving()" [errors]="state.errors()" testId="property-form" (submitted)="state.submit($event)" (closed)="state.close()" />
    }`,
})
export class PropertyDetailPage {
  /** route param :id (read at construction so the paged list can use it immediately) */
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly api = inject(PropertiesApi);
  private readonly unitsApi = inject(UnitsApi);
  readonly humanize = humanize;
  readonly descriptionColumns = DESCRIPTION_COLUMNS;
  readonly summary = signal<PropertySummary | null>(null);
  readonly fields = propertyFields(inject(RealEstateLookups));
  readonly units = createPagedList((q) => this.unitsApi.list({ ...q, filters: [{ field: 'propertyId', op: 'eq', value: this.id }] }), { sort: 'unitNumber' });
  readonly state = new MasterDataState<PropertyDto>({
    save: (id, body) => this.api.update(id!, body), reload: () => this.load(), message: inject(NzMessageService), entityName: 'Property',
  });
  readonly unitColumns: TableColumn<UnitListItem>[] = [
    { key: 'unitNumber', label: 'Unit', sortField: 'unitNumber', width: '110px' },
    { key: 'unitTypeName', label: 'Type' },
    { key: 'floor', label: 'Floor' },
    { key: 'areaSqft', label: 'Area (sq ft)', sortField: 'areaSqft' },
    { key: 'askingRent', label: 'Asking rent', sortField: 'askingRent', value: (u) => u.askingRent.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status' },
    { key: 'tenantName', label: 'Tenant' },
    { key: 'leaseEnds', label: 'Lease ends' },
  ];

  constructor() { this.load(); }

  private load(): void { this.api.summary(this.id).subscribe((s) => this.summary.set(s)); }
}
