import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { ITEM_TYPES, ItemDto, ItemsApi, TaxCodesApi, yesNo } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { SetupApi } from '@core/api/setup.api';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';

import { DecimalPipe } from '@angular/common';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

@Component({
  selector: 'erp-items',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzTagModule, TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  templateUrl: './items.page.html',
})
export class ItemsPage {
  private readonly currencyService = inject(WorkspaceCurrencyService);
  readonly currency = this.currencyService.currency;
  private readonly api = inject(ItemsApi);
  private readonly taxes = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  /** The units and categories the business keeps in its setup; offered as suggestions, and anything else can still be typed. */
  private readonly profile = toSignal(
    inject(SessionStore).hasPermission('core.settings.read') ? inject(SetupApi).profile().pipe(catchError(() => of(null))) : of(null), { initialValue: null });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'sku' });
  readonly state = new MasterDataState<ItemDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Item',
  });
  readonly columns: TableColumn<ItemDto>[] = [
    { key: 'sku', label: 'SKU', sortField: 'sku', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'barcode', label: 'Barcode', width: '150px' },
    { key: 'category', label: 'Category', width: '140px' },
    { key: 'type', label: 'Type', value: (i) => (i.type === 'product' ? 'Product' : 'Service') },
    { key: 'uom', label: 'UoM', width: '80px' },
    { key: 'salesPrice', label: 'Sales price', width: '140px', align: 'right' },
    { key: 'isActive', label: 'Active', width: '90px', value: (i) => yesNo(i.isActive) },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'sku', label: 'SKU', type: 'text', required: true, maxLength: 40 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'barcode', label: 'Barcode', type: 'text', maxLength: 64, hint: 'What a scanner reads at the till. Optional, and unique to one item.' },
    { key: 'type', label: 'Type', type: 'select', required: true, options: ITEM_TYPES, default: 'service' },
    { key: 'uom', label: 'Unit of measure', type: 'text', required: true, maxLength: 20, default: 'each', suggestions: this.profile()?.units },
    { key: 'category', label: 'Category', type: 'text', maxLength: 100, suggestions: this.profile()?.itemCategories, hint: 'Optional. Pick one from your list or type a new one.' },
    { key: 'salesPrice', label: 'Sales price', type: 'number', min: 0 },
    { key: 'purchasePrice', label: 'Purchase price', type: 'number', min: 0 },
    { key: 'taxCodeId', label: 'Tax code', type: 'select', options: this.taxes().items.map((t) => ({ value: t.id, label: `${t.code} · ${t.name}` })) },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);
}
