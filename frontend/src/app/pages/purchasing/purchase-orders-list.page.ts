import { doneText } from '../../core/format/done-text';
import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ItemsApi, ItemDto, LegalEntitiesApi, TaxCodeDto, TaxCodesApi } from '../../core/api/admin.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { createPagedList } from '../../core/api/paged-list';
import { PurchaseOrderInput, PurchaseOrderSummaryDto, PurchaseOrdersApi, PurchasingKpiApi, PurchasingKpiDto } from '../../core/api/purchasing.api';
import { PartiesApi, PartyDto } from '../parties.page';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

import { StatCardComponent } from '../../shared/stat-card.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

@Component({
  selector: 'erp-purchase-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatusTagComponent,
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzDatePickerModule,
    NzTableModule,
    NzPopconfirmModule,
    PagedTableComponent,
    StatCardComponent,
    HasPermissionDirective,
  ],
  template: `
    <h1 class="erp-page-title">Purchase Orders</h1>

    <!-- KPI Stat Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="po-stats">
      <erp-stat-card
        label="Open POs"
        [value]="kpis()?.openOrdersCount ?? 0"
        format="number"
        caption="draft to partly received"
        [tone]="1" />
      <erp-stat-card
        label="Open Order Value"
        [value]="kpis()?.openOrdersAmount ?? 0"
        format="money"
        caption="open orders, including drafts"
        [tone]="3" />
      <erp-stat-card
        label="Pending Receiving"
        [value]="kpis()?.pendingReceiptsCount ?? 0"
        format="number"
        caption="approved, not fully received"
        [tone]="4" />
      <erp-stat-card
        label="Total Ordered"
        [value]="kpis()?.totalSpend ?? 0"
        format="money"
        caption="all orders except cancelled"
        [tone]="0" />
    </div>

    <!-- Purchase Orders Table -->
    <erp-paged-table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search PO number, vendor, quote ref..."
      exportFilename="purchase-orders"
      (rowClick)="openOrder($event)">

      <button toolbar *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="po-new">
        <span nz-icon nzType="plus"></span>New Purchase Order
      </button>
          
          <ng-template #cell let-row let-column="column" let-value="value">
            @if (column.key === 'number') {
              <a [routerLink]="['/purchasing/orders', row.id]" class="po-link" (click)="$event.stopPropagation()">
                <strong>{{ row.number }}</strong>
              </a>
            } @else if (column.key === 'vendorName') {
              <div class="vendor-cell">
                <span class="vendor-name">{{ row.vendorName }}</span>
                @if (row.vendorReference) {
                  <span class="vendor-ref">Ref: {{ row.vendorReference }}</span>
                }
              </div>
            } @else if (column.key === 'totalAmount') {
              <strong class="amount">{{ row.currency }} {{ row.totalAmount | number:'1.2-2' }}</strong>
            } @else if (column.key === 'status') {
              <erp-status [value]="row.status" />
            } @else if (column.key === 'invoiced') {
              @if (row.invoicedBillId) {
                <nz-tag nzColor="green"><span nz-icon nzType="check"></span> Billed</nz-tag>
              } @else {
                <nz-tag nzColor="default">Pending</nz-tag>
              }
            } @else if (column.key === 'actions') {
              <div class="action-buttons" (click)="$event.stopPropagation()">
                <a [href]="'/print/purchase-order/' + row.id" target="_blank" nz-button nzType="link" nzSize="small">
                  <span nz-icon nzType="printer"></span>Print
                </a>
                @if (row.status === 'draft' || row.status === 'cancelled') {
                  <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="link" nzDanger nzSize="small"
                          nz-popconfirm nzPopconfirmTitle="Delete this purchase order?" (nzOnConfirm)="deleteOrder(row)">
                    Delete
                  </button>
                }
              </div>
            } @else {
              {{ value }}
            }
          </ng-template>
        </erp-paged-table>

      <!-- Create Purchase Order Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="New Purchase Order"
        [nzWidth]="850"
        (nzOnClose)="drawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form nz-form [formGroup]="form" nzLayout="vertical" (ngSubmit)="submitForm()">
            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Legal Entity</nz-form-label>
                  <nz-form-control nzErrorTip="Please select a legal entity">
                    <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                      @for (e of entities(); track e.id) {
                        <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                      }
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>PO Number (Auto if blank)</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="number" placeholder="PO-2026-0001" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Vendor / Supplier</nz-form-label>
                  <nz-form-control nzErrorTip="Vendor is required">
                    <nz-select
                      formControlName="vendorPartyId"
                      nzShowSearch
                      nzPlaceHolder="Select Vendor"
                      (ngModelChange)="onVendorSelected($event)">
                      @for (v of vendors(); track v.id) {
                        <nz-option [nzValue]="v.id" [nzLabel]="v.name + ' (' + v.code + ')'"></nz-option>
                      }
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Vendor Quote / Reference</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="vendorReference" placeholder="e.g. V-QUOT-9821" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Order Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="orderDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label>Expected Delivery Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="expectedDeliveryDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Currency</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="currency" placeholder="QAR" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <!-- Lines Section -->
            <div class="lines-header">
              <h3>Line Items</h3>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addLine()">
                <span nz-icon nzType="plus"></span>Add Item
              </button>
            </div>

            <div formArrayName="lines" class="lines-table-container">
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 220px;">Item / Product</th>
                    <th>Description</th>
                    <th style="width: 80px;">Qty</th>
                    <th style="width: 100px;">Price</th>
                    <th style="width: 75px;">Disc %</th>
                    <th style="width: 75px;">Tax %</th>
                    <th style="width: 100px;">Total</th>
                    <th style="width: 40px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (ctrl of linesArray.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td style="width: 220px;">
                        <nz-select
                          formControlName="itemId"
                          nzShowSearch
                          nzAllowClear
                          nzPlaceHolder="Select Item"
                          (ngModelChange)="onItemSelected($index, $event)"
                          style="width: 100%;">
                          @for (it of items(); track it.id) {
                            <nz-option [nzValue]="it.id" [nzLabel]="it.sku + ' - ' + it.name"></nz-option>
                          }
                        </nz-select>
                      </td>
                      <td><input aria-label="Item description" nz-input formControlName="description" placeholder="Item description" /></td>
                      <td><nz-input-number formControlName="quantity" [nzMin]="1" [nzStep]="1" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="unitPrice" [nzMin]="0" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="discountPercent" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="taxRate" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td class="line-calc">{{ calculateLineTotal(ctrl.value) | number:'1.2-2' }}</td>
                      <td>
                        @if (linesArray.length > 1) {
                          <button nz-button nzType="text" nzDanger nzSize="small" type="button" aria-label="Remove line" nz-tooltip nzTooltipTitle="Remove line" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="po-summary-panel">
              <div class="summary-line">
                <span>Subtotal:</span>
                <span>{{ calculatedTotals().subTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-line">
                <span>Total Tax:</span>
                <span>{{ calculatedTotals().taxTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-line total-highlight">
                <span>Total Amount:</span>
                <span>{{ form.get('currency')?.value }} {{ calculatedTotals().total | number:'1.2-2' }}</span>
              </div>
            </div>

            <div nz-row [nzGutter]="16" style="margin-top: 16px;">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Delivery / Receiving Address</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="deliveryAddress" rows="2" placeholder="Warehouse address"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Payment Terms</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="paymentTerms" rows="2" placeholder="e.g. Net 30, payment on delivery"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Create Purchase Order</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .po-link { color: var(--erp-color-primary); font-weight: 600; white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .po-link:hover { text-decoration: underline; color: var(--erp-color-primary-hover); }
    .vendor-cell { display: flex; flex-direction: column; }
    .vendor-name { font-weight: 600; color: var(--erp-color-text-primary); }
    .vendor-ref { font-size: 12px; color: var(--erp-color-text-muted); }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .lines-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
    .lines-header h3 { margin: 0; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .lines-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .lines-table th { background: var(--erp-color-bg-subtle); padding: 8px; text-align: left; font-weight: 600; color: var(--erp-color-text-secondary); border: 1px solid var(--erp-color-border-subtle); }
    .lines-table td { padding: 4px; border: 1px solid var(--erp-color-border-subtle); vertical-align: middle; }
    .line-calc { text-align: end; padding-inline-end: 8px; font-weight: 600; color: var(--erp-color-text-primary); }
    .po-summary-panel { margin-top: 16px; margin-inline-start: auto; width: 280px; background: var(--erp-color-bg-subtle); border-radius: 8px; padding: 12px; border: 1px solid var(--erp-color-border-subtle); }
    .summary-line { display: flex; justify-content: space-between; margin-bottom: 6px; font-size: 13px; color: var(--erp-color-text-secondary); }
    .total-highlight { border-top: 1px solid var(--erp-color-border-default); padding-top: 6px; margin-top: 6px; font-weight: 700; font-size: 15px; color: var(--erp-color-text-primary); }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
  `],
})
export class PurchaseOrdersListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly kpiApi = inject(PurchasingKpiApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly taxCodesApi = inject(TaxCodesApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.ordersApi.list(q), { sort: 'number' });
  readonly kpis = signal<PurchasingKpiDto | null>(null);
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly vendors = signal<PartyDto[]>([]);
  readonly items = signal<ItemDto[]>([]);
  readonly taxCodes = signal<TaxCodeDto[]>([]);
  readonly defaultTaxRate = signal<number>(0);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', Validators.required],
    number: [''],
    vendorPartyId: [null, Validators.required],
    vendorName: ['', Validators.required],
    vendorReference: [''],
    orderDate: [new Date(), Validators.required],
    expectedDeliveryDate: [null],
    currency: [this.currencyService.code(), Validators.required],
    paymentTerms: ['Net 30'],
    deliveryAddress: ['Central Warehouse'],
    notes: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<PurchaseOrderSummaryDto>[] = [
    { key: 'number', label: 'PO #', sortField: 'number', width: '140px' },
    { key: 'vendorName', label: 'Vendor / Supplier', sortField: 'vendorName' },
    { key: 'orderDate', label: 'Order Date', sortField: 'orderDate', width: '120px', filterField: 'orderDate' },
    { key: 'expectedDeliveryDate', label: 'Expected Due', sortField: 'expectedDeliveryDate', width: '120px', filterField: 'expectedDeliveryDate' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '160px', align: 'right', filter: false },
    { key: 'status', label: 'Status', width: '140px', filterField: 'status', filterOptions: ['draft', 'submitted', 'approved', 'partiallyReceived', 'received', 'billed', 'cancelled'] },
    { key: 'invoiced', label: 'Bill', width: '110px', filter: false, value: (r) => (r.invoicedBillId ? 'Billed' : 'Pending') },
    { key: 'actions', label: 'Actions', width: '140px' },
  ];

  /** A plain function: the form array is not a signal, so a computed would keep its first result. */
  readonly calculatedTotals = () => {
    let sub = 0;
    let tax = 0;
    const lines = this.linesArray.value as { quantity: number; unitPrice: number; discountPercent: number; taxRate: number }[];
    for (const l of lines) {
      const base = (l.quantity || 0) * (l.unitPrice || 0);
      const disc = base * ((l.discountPercent || 0) / 100);
      const afterDisc = base - disc;
      const taxAmt = afterDisc * ((l.taxRate || 0) / 100);
      sub += afterDisc;
      tax += taxAmt;
    }
    return { subTotal: sub, taxTotal: tax, total: sub + tax };
  };

  constructor() {
    this.loadKpis();
    this.loadEntities();
    this.loadVendors();
    this.loadItems();
    this.loadTaxCodes();
  }

  loadTaxCodes(): void {
    this.taxCodesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        const items = res.items || [];
        this.taxCodes.set(items);
        const def = items.find((t) => t.isDefault) || items.find((t) => t.kind === 'vat' || t.kind === 'salesTax') || items[0];
        if (def && typeof def.rate === 'number') {
          this.defaultTaxRate.set(def.rate);
        }
      },
      error: () => {},
    });
  }

  loadKpis(): void {
    this.kpiApi.getSummary().subscribe({
      next: (k) => this.kpis.set(k),
      error: () => {},
    });
  }

  loadEntities(): void {
    this.entitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.entities.set(res.items.map((x) => ({ id: x.id, name: x.name }))),
      error: () => {},
    });
  }

  openOrder(row: PurchaseOrderSummaryDto): void {
    this.router.navigate(['/purchasing/orders', row.id]);
  }

  loadVendors(): void {
    this.partiesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => {
        const vendorParties = res.items.filter((p) => p.roles?.some((r) => r.toLowerCase().includes('vendor') || r.toLowerCase().includes('supplier')));
        this.vendors.set(vendorParties.length > 0 ? vendorParties : res.items);
      },
      error: () => {},
    });
  }

  loadItems(): void {
    this.itemsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.items.set(res.items),
      error: () => {},
    });
  }

  onVendorSelected(partyId: string | null): void {
    if (!partyId) return;
    const v = this.vendors().find((x) => x.id === partyId);
    if (v) {
      this.form.patchValue({
        vendorName: v.name,
        currency: v.currencyCode || this.form.value.currency || this.currencyService.code(),
      });
    }
  }

  onItemSelected(index: number, itemId: string | null): void {
    if (!itemId) return;
    const it = this.items().find((x) => x.id === itemId);
    if (it) {
      let taxRate = this.defaultTaxRate();
      if (it.taxCodeId) {
        const tc = this.taxCodes().find((t) => t.id === it.taxCodeId);
        if (tc && typeof tc.rate === 'number') {
          taxRate = tc.rate;
        }
      }
      const lineGroup = this.linesArray.at(index);
      lineGroup.patchValue({
        itemCode: it.sku,
        description: it.name,
        unitOfMeasure: it.uom || 'EA',
        unitPrice: it.purchasePrice || it.salesPrice || 0,
        taxRate,
      });
    }
  }

  openCreateDrawer(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id ?? '',
      number: '',
      vendorPartyId: null,
      vendorName: '',
      vendorReference: '',
      orderDate: new Date(),
      expectedDeliveryDate: null,
      currency: this.currencyService.code(),
      paymentTerms: 'Net 30',
      deliveryAddress: '',
      notes: '',
    });
    this.linesArray.clear();
    this.addLine();
    this.drawerVisible.set(true);
  }

  addLine(): void {
    this.linesArray.push(
      this.fb.group({
        itemId: [null],
        itemCode: [''],
        description: ['', Validators.required],
        quantity: [1, [Validators.required, Validators.min(0.01)]],
        unitOfMeasure: ['EA'],
        unitPrice: [0, [Validators.required, Validators.min(0)]],
        discountPercent: [0, [Validators.min(0), Validators.max(100)]],
        taxRate: [this.defaultTaxRate(), [Validators.min(0), Validators.max(100)]],
      })
    );
  }

  removeLine(index: number): void {
    this.linesArray.removeAt(index);
  }

  calculateLineTotal(val: { quantity: number; unitPrice: number; discountPercent: number; taxRate: number }): number {
    const base = (val.quantity || 0) * (val.unitPrice || 0);
    const disc = base * ((val.discountPercent || 0) / 100);
    const afterDisc = base - disc;
    const tax = afterDisc * ((val.taxRate || 0) / 100);
    return afterDisc + tax;
  }

  submitForm(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach((c) => {
        c.markAsDirty();
        c.updateValueAndValidity({ onlySelf: true });
      });
      return;
    }

    this.isSubmitting.set(true);
    const raw = this.form.value;
    const input: PurchaseOrderInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      vendorPartyId: raw.vendorPartyId || null,
      vendorName: raw.vendorName,
      vendorReference: raw.vendorReference || null,
      orderDate: this.formatDate(raw.orderDate),
      expectedDeliveryDate: raw.expectedDeliveryDate ? this.formatDate(raw.expectedDeliveryDate) : null,
      currency: raw.currency,
      paymentTerms: raw.paymentTerms || null,
      deliveryAddress: raw.deliveryAddress || null,
      notes: raw.notes || null,
      lines: raw.lines,
    };

    this.ordersApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(doneText('created', 'Purchase order', created.number));
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/purchasing/orders', created.id]);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(problemMessage(err, 'Failed to create purchase order'));
      },
    });
  }

  deleteOrder(row: { id: string; number?: string | null }): void {
    this.ordersApi.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Purchase order', row.number));
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete order')),
    });
  }

  private formatDate(d: Date | string): string {
    const date = new Date(d);
    return date.toISOString().split('T')[0];
  }
}
