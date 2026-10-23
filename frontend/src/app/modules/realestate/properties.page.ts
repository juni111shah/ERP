import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
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

@Component({
  selector: 'erp-properties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Properties</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code, name, community" emptyText="No properties yet" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'realestate.properties.manage'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newProperty' | translate: 'New property'" nz-tooltip [nzTooltipTitle]="'ui.add.newProperty' | translate: 'New property'" nzType="primary" (click)="state.create()" data-testid="property-new"><span nz-icon nzType="plus"></span>New property</button>
    </erp-paged-table>
    <erp-record-drawer title="New property" [open]="state.open()" [fields]="fields" customFieldEntityType="realestate.property"
                       [saving]="state.saving()" [errors]="state.errors()" testId="property-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class PropertiesPage {
  private readonly api = inject(PropertiesApi);
  private readonly router = inject(Router);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
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
