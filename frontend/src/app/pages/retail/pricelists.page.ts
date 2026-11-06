import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CreatePriceListInput,
  RetailPriceListDetailDto,
  RetailPriceListItemDto,
  RetailPriceListsApi,
  RetailPriceListSummaryDto
} from '../../core/api/retail.api';
import { createPagedList } from '../../core/api/paged-list';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ErpLabelPipe } from '../../shared/display.pipes';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { displayDate } from '../../core/format/display';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';
import { ErpFormModule } from '../../core/forms/erp-form.module';
import { showFormErrors } from '../../core/forms/form-errors';

/** The end date field must not fall before the start date field of the same form. */
const notBeforeStart = (startKey: string) => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

@Component({
  selector: 'erp-pricelists-page',
  standalone: true,
  imports: [ErpLabelPipe, NzPopconfirmModule, NzToolTipModule, StatusTagComponent, 
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzSwitchModule,
    NzModalModule,
    NzDrawerModule,
    ErpFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    NzTableModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" (rowClick)="viewDetail($event)" searchPlaceholder="Search by code or name" emptyText="No price lists yet. Create one to set item prices.">
      <button toolbar *erpHasPermission="'retail.pricelists.manage'" nz-button nzType="primary" (click)="openCreateModal()" data-testid="pricelist-new">
        <span nz-icon nzType="plus"></span> Create Price List
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') { <span class="mono-code font-bold">{{ row.code }}</span> }
          @case ('type') { {{ row.type | erpLabel }} }
          @case ('itemCount') { <span class="num">{{ row.itemCount }}</span> }
          @case ('validity') { <span class="nowrap">{{ validity(row) }}</span> }
          @case ('isActive') {
            <span class="status-cell">
              <erp-status [value]="row.isActive ? 'active' : 'inactive'" />
              @if (row.isDefault) { <nz-tag>Default</nz-tag> }
            </span>
          }
          @case ('actions') {
            <div class="table-actions" (click)="$event.stopPropagation()">
              <button *erpHasPermission="'retail.pricelists.manage'" nz-button nzType="link" nzSize="small" (click)="openEditModal(row)">
                <span nz-icon nzType="edit"></span> Edit
              </button>
              <button *erpHasPermission="'retail.pricelists.manage'" nz-button nzType="link" nzDanger nzSize="small"
                      nz-popconfirm [nzPopconfirmTitle]="'Delete price list ' + row.code + '?'" (nzOnConfirm)="deletePriceList(row)">
                <span nz-icon nzType="delete"></span> Delete
              </button>
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

      <!-- Create Price List Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        [nzTitle]="editing() ? 'Edit price list' : 'New price list'"
        nzOkText="Save"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="savePriceList()"
        nzWidth="720px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. PL-RETAIL-2026" [readonly]="!!editing()" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Standard store prices" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label nzRequired>Legal entity</nz-form-label>
                <nz-form-control nzErrorTip="Entity is required">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Type</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option nzValue="standard" nzLabel="Standard"></nz-option>
                    <nz-option nzValue="promotional" nzLabel="Promotional"></nz-option>
                    <nz-option nzValue="tiered" nzLabel="Tiered"></nz-option>
                    <nz-option nzValue="member" nzLabel="Member"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Currency</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="currency">
                    @for (c of currencyOptions(); track c) { <nz-option [nzValue]="c" [nzLabel]="c"></nz-option> }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label>Valid from</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="validFrom" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Valid to</nz-form-label>
                <nz-form-control nzErrorTip="The end date cannot be before the start date">
                  <nz-date-picker formControlName="validTo" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2 switches-row">
              <nz-form-item>
                <nz-form-control>
                  <label>
                    <nz-switch formControlName="isDefault"></nz-switch>
                    <span style="margin-inline-start: 8px;">Default price list</span>
                  </label>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-control>
                  <label>
                    <nz-switch formControlName="isActive"></nz-switch>
                    <span style="margin-inline-start: 8px;">Active</span>
                  </label>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Description</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="description" placeholder="Where these prices apply"></textarea>
              </nz-form-control>
            </nz-form-item>

            <div formArrayName="items">
              <div class="items-header">
                <strong>Item prices</strong>
                <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addItem()">
                  <span nz-icon nzType="plus"></span> Add item
                </button>
              </div>
              @if (itemsArray.length === 0) {
                <p class="text-secondary">No items yet. Add the items this list sets a price for.</p>
              } @else {
                <div class="item-grid item-head" aria-hidden="true">
                  <span>Item</span><span>Base price</span><span>List price</span><span>Minimum quantity</span><span></span>
                </div>
              }
              @for (line of itemsArray.controls; track line; let i = $index) {
                <div class="item-grid" [formGroupName]="i">
                  <nz-select formControlName="itemId" nzShowSearch nzPlaceHolder="Choose an item" (ngModelChange)="onItemChosen(i, $event)" [attr.aria-label]="'Item ' + (i + 1)" [nzStatus]="lineStatus(line, 'itemId')">
                    @for (it of itemsList(); track it.id) { <nz-option [nzValue]="it.id" [nzLabel]="it.sku + ' - ' + it.name"></nz-option> }
                  </nz-select>
                  <nz-input-number formControlName="basePrice" [nzMin]="0" style="width: 100%" [attr.aria-label]="'Base price of item ' + (i + 1)"></nz-input-number>
                  <nz-input-number formControlName="price" [nzMin]="0" style="width: 100%" [attr.aria-label]="'List price of item ' + (i + 1)" [nzStatus]="lineStatus(line, 'price')"></nz-input-number>
                  <nz-input-number formControlName="minQuantity" [nzMin]="0" style="width: 100%" nzPlaceHolder="None" [attr.aria-label]="'Minimum quantity of item ' + (i + 1)"></nz-input-number>
                  <button nz-button nzType="text" nzDanger type="button" nz-tooltip nzTooltipTitle="Remove item" aria-label="Remove item" (click)="removeItem(i)">
                    <span nz-icon nzType="minus-circle"></span>
                  </button>
                </div>
              }
            </div>
          </form>
        </ng-container>
      </nz-modal>

      <!-- View Detail Drawer -->
      <nz-drawer
        [nzVisible]="isDrawerVisible()"
        [nzTitle]="selectedDetail()?.code ?? 'Price list'"
        [nzWidth]="640"
        (nzOnClose)="isDrawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedDetail() as pl" class="drawer-content">
            <div class="detail-banner">
              <h3>{{ pl.name }}</h3>
              <div class="banner-badges">
                <nz-tag>{{ pl.type | erpLabel }}</nz-tag>
                <nz-tag>{{ pl.currency }}</nz-tag>
                <nz-tag *ngIf="pl.isDefault">Default</nz-tag>
              </div>
            </div>

            <p *ngIf="pl.description" class="text-secondary">{{ pl.description }}</p>

            <div class="table-section">
              <h4>Item prices ({{ pl.items.length }})</h4>
              <nz-table #itemsTable [nzData]="pl.items" nzSize="small" [nzBordered]="true" [nzShowPagination]="false" [nzFrontPagination]="false">
                <thead>
                  <tr>
                    <th>Item</th>
                    <th nzAlign="right">Base price</th>
                    <th nzAlign="right">List price</th>
                    <th nzAlign="right">Minimum quantity</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let itm of itemsTable.data">
                    <td>
                      <div><strong>{{ itm.itemName }}</strong></div>
                      <small class="mono-code">{{ itm.itemCode }}</small>
                    </td>
                    <td nzAlign="right" class="num">{{ pl.currency }} {{ itm.basePrice | number:'1.2-2' }}</td>
                    <td nzAlign="right" class="num font-bold">{{ pl.currency }} {{ itm.price | number:'1.2-2' }}</td>
                    <td nzAlign="right" class="num">{{ itm.minQuantity ? (itm.minQuantity | number:'1.0-4') : 'None' }}</td>
                  </tr>
                  <tr *ngIf="pl.items.length === 0">
                    <td colspan="4" class="text-center text-secondary py-4">No item prices on this list yet.</td>
                  </tr>
                </tbody>
              </nz-table>
            </div>
          </div>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary {
      color: var(--erp-color-text-muted);
      margin: 4px 0 0;
    }
    .mono-code {
      font-family: var(--erp-font-family-mono);
      white-space: nowrap;
    }
    .font-bold {
      font-weight: 600;
    }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .status-cell { display: inline-flex; align-items: center; gap: 4px; }
    .items-header { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-block: 8px; }
    .item-grid { display: grid; grid-template-columns: minmax(0, 3fr) minmax(0, 1.2fr) minmax(0, 1.2fr) minmax(0, 1.2fr) 32px; gap: 8px; align-items: center; margin-block-end: 8px; }
    .item-head { font-size: 12px; color: var(--erp-color-text-muted); margin-block-end: 4px; }
    .row-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    .row-3 {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 16px;
    }
    .switches-row {
      margin-bottom: 8px;
    }
    .drawer-content {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .detail-banner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 12px;
      border-block-end: 1px solid var(--erp-color-border-subtle);
    }
    .detail-banner h3 {
      margin: 0;
    }
    .banner-badges {
      display: flex;
      gap: 8px;
    }
    .table-section {
      margin-top: 16px;
    }
    .text-center {
      text-align: center;
    }
    .py-4 {
      padding-top: 16px;
      padding-bottom: 16px;
    }
  `]
})
export class PriceListsPageComponent implements OnInit {
  private readonly priceListsApi = inject(RetailPriceListsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly itemsList = signal<{ id: string; name: string; sku: string; salesPrice?: number }[]>([]);
  readonly editing = signal<RetailPriceListDetailDto | null>(null);
  /** The usual codes, always with the workspace's own and the one on the list being edited. */
  readonly currencyOptions = computed(() => [...new Set([this.currency(), this.editing()?.currency, 'QAR', 'AED', 'SAR', 'USD', 'EUR', 'GBP'].filter((c): c is string => !!c))]);
  readonly isSaving = signal(false);
  readonly selectedDetail = signal<RetailPriceListDetailDto | null>(null);

  readonly isModalVisible = signal(false);
  readonly isDrawerVisible = signal(false);

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    type: ['standard', [Validators.required]],
    currency: [this.currency(), [Validators.required]],
    validFrom: [null],
    validTo: [null, [notBeforeStart('validFrom')]],
    isDefault: [false],
    isActive: [true],
    description: [''],
    items: this.fb.array([])
  });

  readonly columns: TableColumn<RetailPriceListSummaryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type' },
    { key: 'currency', label: 'Currency' },
    { key: 'validity', label: 'Valid', filter: false, value: (r) => this.validity(r) },
    { key: 'itemCount', label: 'Items', align: 'right' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  readonly list = createPagedList<RetailPriceListSummaryDto>((q) => this.priceListsApi.list(q), { sort: '-createdAt' });

  ngOnInit(): void {
    this.form.get('validFrom')!.valueChanges.subscribe(() => this.form.get('validTo')!.updateValueAndValidity());
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.itemsList.set(res.items.filter(i => i.isActive))
    });
  }

  get itemsArray(): FormArray {
    return this.form.get('items') as FormArray;
  }

  validity(row: RetailPriceListSummaryDto): string {
    if (row.validFrom && row.validTo) return `${displayDate(row.validFrom)} to ${displayDate(row.validTo)}`;
    if (row.validFrom) return `From ${displayDate(row.validFrom)}`;
    if (row.validTo) return `Until ${displayDate(row.validTo)}`;
    return 'Always';
  }

  private itemGroup(i?: RetailPriceListItemDto): FormGroup {
    return this.fb.group({
      itemId: [i?.itemId ?? null, [Validators.required]],
      itemCode: [i?.itemCode ?? ''],
      itemName: [i?.itemName ?? ''],
      basePrice: [i?.basePrice ?? 0, [Validators.required, Validators.min(0)]],
      price: [i?.price ?? null, [Validators.required, Validators.min(0)]],
      minQuantity: [i?.minQuantity ?? null]
    });
  }

  lineStatus(line: AbstractControl, key: string): 'error' | '' {
    const c = line.get(key);
    return c?.invalid && c.dirty ? 'error' : '';
  }

  addItem(): void {
    this.itemsArray.push(this.itemGroup());
  }

  removeItem(index: number): void {
    this.itemsArray.removeAt(index);
  }

  onItemChosen(index: number, itemId: string): void {
    const it = this.itemsList().find(x => x.id === itemId);
    if (!it) return;
    const line = this.itemsArray.at(index);
    line.patchValue({
      itemCode: it.sku,
      itemName: it.name,
      basePrice: it.salesPrice ?? 0,
      price: line.value.price ?? it.salesPrice ?? null
    });
  }

  private setKeyFieldsEnabled(enabled: boolean): void {
    const control = this.form.get('legalEntityId')!;
    if (enabled) control.enable({ emitEvent: false });
    else control.disable({ emitEvent: false });
  }

  openEditModal(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.getDetail(row.id).subscribe({
      next: detail => {
        this.editing.set(detail);
        this.itemsArray.clear();
        detail.items.forEach(i => this.itemsArray.push(this.itemGroup(i)));
        this.form.patchValue({
          legalEntityId: detail.legalEntityId,
          code: detail.code,
          name: detail.name,
          type: detail.type,
          currency: detail.currency,
          validFrom: detail.validFrom ? new Date(`${detail.validFrom}T00:00:00`) : null,
          validTo: detail.validTo ? new Date(`${detail.validTo}T00:00:00`) : null,
          isDefault: detail.isDefault,
          isActive: detail.isActive,
          description: detail.description ?? ''
        });
        this.setKeyFieldsEnabled(false);
        this.isModalVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price list could not be loaded'))
    });
  }

  deletePriceList(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.delete(row.id).subscribe({
      next: () => {
        this.msg.success('Price list deleted');
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price list could not be deleted'))
    });
  }

  openCreateModal(): void {
    this.editing.set(null);
    this.setKeyFieldsEnabled(true);
    this.itemsArray.clear();
    this.form.reset({
      legalEntityId: this.entities().find(e => e.id === this.sessionStore.currentEntityId())?.id || this.entities()[0]?.id || '',
      code: '',
      name: '',
      type: 'standard',
      currency: this.currencyService.code(),
      validFrom: null,
      validTo: null,
      isDefault: false,
      isActive: true,
      description: ''
    });
    this.isModalVisible.set(true);
  }

  closeModal(): void {
    this.isModalVisible.set(false);
  }

  viewDetail(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.getDetail(row.id).subscribe({
      next: detail => {
        this.selectedDetail.set(detail);
        this.isDrawerVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price list could not be loaded'))
    });
  }

  savePriceList(): void {
    if (this.form.invalid) {
      showFormErrors(this.form);
      this.msg.warning('Fill in the highlighted fields, including every item');
      return;
    }

    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const editing = this.editing();
    const input: CreatePriceListInput = {
      legalEntityId: val.legalEntityId,
      code: val.code,
      name: val.name,
      type: val.type,
      currency: val.currency,
      validFrom: val.validFrom ? toIsoDate(new Date(val.validFrom)) : null,
      validTo: val.validTo ? toIsoDate(new Date(val.validTo)) : null,
      isDefault: val.isDefault,
      isActive: val.isActive,
      description: val.description || null,
      items: val.items,
      customFields: editing?.customFields ?? null
    };

    (editing ? this.priceListsApi.update(editing.id, input) : this.priceListsApi.create(input)).subscribe({
      next: () => {
        this.msg.success('Price list saved');
        this.isSaving.set(false);
        this.closeModal();
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The price list could not be saved'));
        this.isSaving.set(false);
      }
    });
  }
}
