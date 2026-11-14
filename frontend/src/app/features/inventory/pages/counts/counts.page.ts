import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CountRow, CountsApi, InventoryLookups } from '@core/api/inventory.api';
import { createPagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { injectWarehouseList, warehouseNameMap } from '../../inventory-warehouses';

/** Physical stock counts: start a sheet for a warehouse, then open it to enter the counted quantities and post. */
@Component({
  selector: 'erp-counts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Stock counts</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search count number or memo" emptyText="No stock counts yet. Start one to compare what is on the shelf with the system." (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'inventory.counts.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="count-new"><span nz-icon nzType="plus"></span>New count</button>
    </erp-paged-table>
    <erp-record-drawer title="New stock count" [open]="state.open()" [fields]="fields" [value]="null" [saving]="state.saving()" [errors]="state.errors()" writePermission="inventory.counts.manage" testId="count-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class CountsPage {
  private readonly countsApi = inject(CountsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(InventoryLookups);
  private readonly router = inject(Router);
  readonly list = createPagedList((q) => this.countsApi.list(q), { sort: '-countDate' });
  readonly state = new MasterDataState<CountRow>({
    save: (_id, body) => this.countsApi.create(body), reload: () => this.list.reload(), message: this.message, entityName: 'Stock count',
  });
  private readonly warehouses = injectWarehouseList();
  private readonly warehouseNames = computed(() => warehouseNameMap(this.warehouses()));
  readonly columns: TableColumn<CountRow>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '140px' },
    { key: 'warehouse', label: 'Warehouse', value: (c) => this.warehouseNames().get(c.warehouseId) ?? '' },
    { key: 'countDate', label: 'Count date', sortField: 'countDate', width: '130px' },
    { key: 'status', label: 'Status', width: '120px' },
    { key: 'lines', label: 'Items', width: '90px', align: 'right' },
    { key: 'memo', label: 'Memo' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'warehouseId', label: 'Warehouse', type: 'lookup', lookup: this.lookups.warehouse, required: true },
    { key: 'countDate', label: 'Count date', type: 'date', required: true, default: new Date() },
    { key: 'memo', label: 'Memo', type: 'text', maxLength: 500 },
  ];

  open(row: CountRow): void { void this.router.navigate(['/inventory/counts', row.id]); }
}
