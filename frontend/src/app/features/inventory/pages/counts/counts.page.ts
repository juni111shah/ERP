import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { CountRow, CountsApi, InventoryLookups, WarehousesApi } from '@core/api/inventory.api';

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
    <erp-record-drawer title="New stock count" [open]="state.open()" [fields]="fields" [value]="null" [saving]="state.saving()" [errors]="state.errors()" testId="count-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class CountsPage {
  private readonly api = inject(CountsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(InventoryLookups);
  private readonly router = inject(Router);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-countDate' });
  readonly state = new MasterDataState<CountRow>({
    save: (_id, body) => this.api.create(body as never), reload: () => this.list.reload(), message: this.message, entityName: 'Stock count',
  });
  private readonly warehouseNames = toSignal(inject(WarehousesApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(map((r) => new Map(r.items.map((w) => [w.id, w.name]))), catchError(() => of(new Map<string, string>()))), { initialValue: new Map<string, string>() });
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
