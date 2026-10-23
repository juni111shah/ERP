import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { ITEM_TYPES, ItemDto, ItemsApi, TaxCodesApi, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'erp-items',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Items</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search SKU or name" emptyText="No items yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'core.items.write'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newItem' | translate: 'New item'" nz-tooltip [nzTooltipTitle]="'ui.add.newItem' | translate: 'New item'" nzType="primary" (click)="state.create()" data-testid="item-new"><span nz-icon nzType="plus"></span>New item</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit item' : 'New item'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())" customFieldEntityType="item"
                       [saving]="state.saving()" [errors]="state.errors()" testId="item-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class ItemsPage {
  private readonly api = inject(ItemsApi);
  private readonly taxes = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'sku' });
  readonly state = new MasterDataState<ItemDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Item',
  });
  readonly columns: TableColumn<ItemDto>[] = [
    { key: 'sku', label: 'SKU', sortField: 'sku', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type', value: (i) => (i.type === 'product' ? 'Product' : 'Service') },
    { key: 'uom', label: 'UoM', width: '80px' },
    { key: 'salesPrice', label: 'Sales price' },
    { key: 'isActive', label: 'Active', width: '90px', value: (i) => yesNo(i.isActive) },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'sku', label: 'SKU', type: 'text', required: true, maxLength: 40 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'type', label: 'Type', type: 'select', required: true, options: ITEM_TYPES, default: 'service' },
    { key: 'uom', label: 'Unit of measure', type: 'text', required: true, maxLength: 20, default: 'each' },
    { key: 'salesPrice', label: 'Sales price', type: 'number', min: 0 },
    { key: 'purchasePrice', label: 'Purchase price', type: 'number', min: 0 },
    { key: 'taxCodeId', label: 'Tax code', type: 'select', options: this.taxes().items.map((t) => ({ value: t.id, label: `${t.code} · ${t.name}` })) },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);
}
