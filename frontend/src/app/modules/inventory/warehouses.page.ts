import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { InventoryLookups, WarehouseRow, WarehousesApi } from './inventory.api';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'erp-warehouses',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Warehouses</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No warehouses yet" [clickable]="false">
      <button toolbar *erpHasPermission="'inventory.warehouses.write'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newWarehouse' | translate: 'New warehouse'" nz-tooltip [nzTooltipTitle]="'ui.add.newWarehouse' | translate: 'New warehouse'" nzType="primary" (click)="state.create()" data-testid="wh-new"><span nz-icon nzType="plus"></span>New warehouse</button>
    </erp-paged-table>
    <erp-record-drawer title="New warehouse" [open]="state.open()" [fields]="fields" [value]="null" customFieldEntityType="inventory.warehouse" [saving]="state.saving()" [errors]="state.errors()" testId="wh-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class WarehousesPage {
  private readonly api = inject(WarehousesApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(InventoryLookups);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<WarehouseRow>({ save: (_id, body) => this.api.create(body as never), reload: () => this.list.reload(), message: this.message, entityName: 'Warehouse' });
  readonly columns: TableColumn<WarehouseRow>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '140px' }, { key: 'name', label: 'Name', sortField: 'name' }, { key: 'address', label: 'Address' },
    { key: 'isActive', label: 'Active', value: (w) => (w.isActive ? 'Yes' : 'No'), width: '90px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: this.lookups.legalEntities() },
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'address', label: 'Address', type: 'textarea', maxLength: 500 },
  ];
}
