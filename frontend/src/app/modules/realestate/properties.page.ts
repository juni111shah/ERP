import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { OWNERSHIPS, PROPERTY_TYPES, PropertiesApi, PropertyDto, RealEstateLookups, humanize } from './realestate.api';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Field specs for the property form (shared by the list and the detail page). */
export function propertyFields(lookups: RealEstateLookups): FieldSpec[] {
  return [
    { key: 'legalEntityId', label: 'Owning legal entity', type: 'select', required: true, options: lookups.legalEntities(), readonlyOnEdit: false },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'propertyType', label: 'Type', type: 'select', required: true, options: PROPERTY_TYPES, default: 'residential' },
    { key: 'ownershipType', label: 'Ownership', type: 'select', required: true, options: OWNERSHIPS, default: 'owned' },
    { key: 'managementFeePercent', label: 'Management fee %', type: 'number', min: 0, max: 100, default: 0, hidden: (v) => v['ownershipType'] !== 'managedForOwner', hint: 'Charged on rent collected; used in the owner statement.' },
    { key: 'ownerPartyId', label: 'Owner', type: 'lookup', lookup: lookups.party('other'), hidden: (v) => v['ownershipType'] !== 'managedForOwner', required: true },
    { key: 'region', label: 'Region / emirate', type: 'text', maxLength: 100 },
    { key: 'community', label: 'Community / area', type: 'text', maxLength: 100 },
    { key: 'plotNumber', label: 'Plot number', type: 'text', maxLength: 50 },
    { key: 'yearBuilt', label: 'Year built', type: 'number', min: 1800, max: 2200, precision: 0 },
    { key: 'totalUnits', label: 'Total lettable units', type: 'number', min: 0, precision: 0, default: 0, hint: 'Never below the number of registered units.' },
    { key: 'indicativeValue', label: 'Indicative value', type: 'number', min: 0, default: 0 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
  ];
}

import { DecimalPipe } from '@angular/common';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-properties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzTagModule, StatCardComponent, TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Properties</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="property-stats">
      <erp-stat-card label="Total properties"
                     [value]="list.total()"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     [caption]="stats().count + ' ' + ('table.onThisPage' | translate: 'on this page')"
                     [tone]="0" />
      <erp-stat-card label="Total units"
                     [value]="stats().totalUnits"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="lettable capacity"
                     [tone]="1" />
      <erp-stat-card label="Portfolio value"
                     [value]="stats().totalValue"
                     format="money"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="indicative appraisal"
                     [tone]="2" />
      <erp-stat-card label="Active properties"
                     [value]="stats().activeCount"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="operational sites"
                     [tone]="3" />
    </div>

    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code, name, community" emptyText="No properties yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') {
            <span class="code-pill">{{ row.code }}</span>
          }
          @case ('name') {
            <span class="name-cell">{{ row.name }}</span>
          }
          @case ('propertyType') {
            <nz-tag>{{ humanize(row.propertyType) }}</nz-tag>
          }
          @case ('indicativeValue') {
            <b>AED {{ row.indicativeValue | number: '1.2-2' }}</b>
          }
          @case ('isActive') {
            <nz-tag [nzColor]="row.isActive ? 'green' : 'default'">
              {{ row.isActive ? 'Active' : 'Inactive' }}
            </nz-tag>
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'realestate.properties.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="property-new"><span nz-icon nzType="plus"></span>New property</button>
    </erp-paged-table>
    <erp-record-drawer title="New property" [open]="state.open()" [fields]="fields" customFieldEntityType="realestate.property"
                       [saving]="state.saving()" [errors]="state.errors()" testId="property-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class PropertiesPage {
  private readonly api = inject(PropertiesApi);
  private readonly router = inject(Router);
  readonly humanize = humanize;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });

  readonly stats = computed(() => {
    const items = this.list.items();
    const totalUnits = items.reduce((s, p) => s + (p.totalUnits || 0), 0);
    const totalValue = items.reduce((s, p) => s + (p.indicativeValue || 0), 0);
    const activeCount = items.filter((p) => p.isActive).length;
    return {
      totalUnits,
      totalValue,
      activeCount,
      count: items.length,
    };
  });
  readonly fields = propertyFields(inject(RealEstateLookups));
  readonly state = new MasterDataState<PropertyDto>({
    save: (_, body) => this.api.create(body), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Property',
  });
  readonly columns: TableColumn<PropertyDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Property', sortField: 'name' },
    { key: 'propertyType', label: 'Type', sortField: 'propertyType', value: (p) => humanize(p.propertyType) },
    { key: 'region', label: 'Region', sortField: 'region' },
    { key: 'community', label: 'Community', sortField: 'community' },
    { key: 'totalUnits', label: 'Units' },
    { key: 'indicativeValue', label: 'Indicative value', sortField: 'indicativeValue', value: (p) => p.indicativeValue.toLocaleString() },
    { key: 'isActive', label: 'Active', width: '90px' },
  ];

  open(p: PropertyDto): void { void this.router.navigate(['/real-estate/properties', p.id]); }
}
