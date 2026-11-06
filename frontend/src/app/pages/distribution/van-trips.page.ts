import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { createPagedList } from '../../core/api/paged-list';
import {
  CompleteTripInput,
  CreateTripInput,
  DistributionRoutesApi,
  DistributionRouteSummaryDto,
  DistributionTripsApi,
  VanSalesTripDto,
  VanSalesTripSummaryDto
} from '../../core/api/distribution.api';
import { WarehousesApi } from '../../modules/inventory/inventory.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';

@Component({
  selector: 'erp-van-trips-page',
  standalone: true,
  imports: [ErpDatePipe, StatusTagComponent, NzToolTipModule, 
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    NzTableModule,
    NzDividerModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" (rowClick)="viewDetail($event)" searchPlaceholder="Search by trip number" emptyText="No van trips yet. Dispatch one to start selling on a route.">
      <button toolbar *erpHasPermission="'distribution.trips.manage'" nz-button nzType="primary" (click)="openDispatchModal()" data-testid="trip-new">
        <span nz-icon nzType="send"></span> Dispatch Van Trip
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('tripNumber') { <span class="mono-code font-bold">{{ row.tripNumber }}</span> }
          @case ('driverName') {
            <div class="nowrap">{{ row.driverName }}</div>
            <div class="sub mono-code">{{ row.vehicleNumber }}</div>
          }
          @case ('departureDate') { <span class="nowrap">{{ row.departureDate | erpDate }}</span> }
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('totalSalesAmount') { <span class="num font-bold">{{ currency() }} {{ row.totalSalesAmount | number:'1.2-2' }}</span> }
          @case ('totalCollectionsAmount') { <span class="num">{{ currency() }} {{ row.totalCollectionsAmount | number:'1.2-2' }}</span> }
          @case ('actions') {
            <div class="table-actions" (click)="$event.stopPropagation()">
              <a [href]="'/print/manifest/' + row.id" target="_blank" nz-button nzType="link" nzSize="small">
                <span nz-icon nzType="printer"></span> Manifest
              </a>
              @if (row.status === 'scheduled' || row.status === 'inProgress') {
                <button *erpHasPermission="'distribution.trips.manage'" nz-button nzType="link" nzSize="small" (click)="openReconcileModal(row)">
                  <span nz-icon nzType="check-circle"></span> Reconcile
                </button>
              }
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

      <!-- Dispatch Van Trip Modal -->
      <nz-modal
        [(nzVisible)]="isDispatchModalVisible"
        nzTitle="Dispatch van trip"
        nzOkText="Dispatch"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="isDispatchModalVisible.set(false)"
        (nzOnOk)="saveTrip()"
        nzWidth="820px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
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
                <nz-form-label nzRequired>Route</nz-form-label>
                <nz-form-control nzErrorTip="Route is required">
                  <nz-select formControlName="routeId" (ngModelChange)="onRouteSelected($event)" nzPlaceHolder="Select Route">
                    <nz-option *ngFor="let rt of routesList()" [nzValue]="rt.id" [nzLabel]="rt.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Departure date</nz-form-label>
                <nz-form-control nzErrorTip="Date is required">
                  <nz-date-picker formControlName="departureDate" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label nzRequired>Driver</nz-form-label>
                <nz-form-control nzErrorTip="Driver is required">
                  <input nz-input formControlName="driverName" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Vehicle plate</nz-form-label>
                <nz-form-control nzErrorTip="Vehicle plate is required">
                  <input nz-input formControlName="vehicleNumber" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Warehouse</nz-form-label>
                <nz-form-control nzErrorTip="Warehouse is required">
                  <nz-select formControlName="warehouseId" nzPlaceHolder="Select Warehouse">
                    <nz-option *ngFor="let wh of warehouses()" [nzValue]="wh.id" [nzLabel]="wh.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-divider nzText="Stock loaded on the van"></nz-divider>

            <div class="items-add-bar">
              <nz-select [(ngModel)]="selectedAddItemId" [ngModelOptions]="{standalone: true}" style="flex: 2" nzShowSearch nzPlaceHolder="Choose an item to load" aria-label="Item to load">
                <nz-option *ngFor="let itm of catalogItems()" [nzValue]="itm.id" [nzLabel]="itm.name + ' (' + itm.sku + ')'"></nz-option>
              </nz-select>
              <nz-input-number [(ngModel)]="selectedAddQty" [ngModelOptions]="{standalone: true}" [nzMin]="1" style="width: 120px" nzPlaceHolder="Quantity" aria-label="Quantity to load"></nz-input-number>
              <button nz-button nzType="dashed" type="button" [disabled]="!selectedAddItemId || !selectedAddQty" (click)="addLoadedItem()">
                <span nz-icon nzType="plus"></span> Add To Van
              </button>
            </div>

            <nz-table #loadedTable [nzData]="loadedItems()" nzSize="small" [nzBordered]="true" [nzShowPagination]="false" [nzFrontPagination]="false" nzNoResult="Nothing loaded yet. Choose an item above." style="margin-top: 12px">
              <thead>
                <tr>
                  <th>Item</th>
                  <th nzAlign="right">Loaded</th>
                  <th nzAlign="right">Unit price</th>
                  <th nzAlign="right">Value</th>
                  <th nzAlign="right"><span class="sr-only">Remove</span></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of loadedTable.data; let idx = index">
                  <td>
                    <strong>{{ row.itemName }}</strong>
                    <div><small class="mono-code">{{ row.itemCode }}</small></div>
                  </td>
                  <td nzAlign="right">{{ row.loadedQuantity }} {{ row.uom }}</td>
                  <td nzAlign="right" class="num">{{ currency() }} {{ row.unitPrice | number:'1.2-2' }}</td>
                  <td nzAlign="right" class="font-bold">{{ currency() }} {{ (row.loadedQuantity * row.unitPrice) | number:'1.2-2' }}</td>
                  <td nzAlign="right">
                    <button aria-label="Remove loaded item" nz-tooltip nzTooltipTitle="Remove" nz-button nzType="link" nzDanger type="button" (click)="removeLoadedItem(idx)">
                      <span nz-icon nzType="delete"></span>
                    </button>
                  </td>
                </tr>
              </tbody>
            </nz-table>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Trip Reconciliation Modal -->
      <nz-modal
        [(nzVisible)]="isReconcileModalVisible"
        nzTitle="Reconcile and complete trip"
        nzOkText="Complete trip"
        [nzOkLoading]="isSavingReconcile()"
        (nzOnCancel)="isReconcileModalVisible.set(false)"
        (nzOnOk)="saveReconciliation()"
        nzWidth="840px">
        <ng-container *nzModalContent>
          <div *ngIf="reconcileTrip() as trip" class="reconcile-container">
            <div class="reconcile-header">
              <div>
                <strong>Trip {{ trip.tripNumber }} &mdash; {{ trip.routeName }}</strong>
                <div class="text-secondary">Driver: {{ trip.driverName }} | Van: {{ trip.vehicleNumber }}</div>
              </div>
            </div>

            <nz-table #recTable [nzData]="reconcileLines" nzSize="small" [nzBordered]="true" [nzShowPagination]="false" [nzFrontPagination]="false" nzNoResult="No items were recorded as loaded on this trip.">
              <thead>
                <tr>
                  <th>Item</th>
                  <th nzAlign="right">Loaded</th>
                  <th nzAlign="right" style="width: 110px">Sold</th>
                  <th nzAlign="right" style="width: 110px">Returned</th>
                  <th nzAlign="right" style="width: 110px">Damaged</th>
                  <th nzAlign="right">Sales</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of recTable.data">
                  <td><strong>{{ item.itemName }}</strong></td>
                  <td nzAlign="right">{{ item.loadedQuantity }}</td>
                  <td nzAlign="right">
                    <nz-input-number [(ngModel)]="item.soldQuantity" [nzMin]="0" [nzMax]="item.loadedQuantity" nzSize="small" (ngModelChange)="calculateTripTotals()" [attr.aria-label]="'Sold quantity of ' + item.itemName"></nz-input-number>
                  </td>
                  <td nzAlign="right">
                    <nz-input-number [(ngModel)]="item.returnedQuantity" [nzMin]="0" nzSize="small" [attr.aria-label]="'Returned quantity of ' + item.itemName"></nz-input-number>
                  </td>
                  <td nzAlign="right">
                    <nz-input-number [(ngModel)]="item.damagedQuantity" [nzMin]="0" nzSize="small" [attr.aria-label]="'Damaged quantity of ' + item.itemName"></nz-input-number>
                  </td>
                  <td nzAlign="right" class="font-bold">
                    {{ currency() }} {{ (item.soldQuantity * item.unitPrice) | number:'1.2-2' }}
                  </td>
                </tr>
              </tbody>
            </nz-table>
            <p class="text-secondary">Sold, returned and damaged together cannot be more than what was loaded. Reconciling takes what was sold or damaged out of stock.</p>

            <div class="reconcile-totals">
              <div class="total-block">
                <span>Total sales</span>
                <span class="font-bold" style="font-size: 18px">{{ currency() }} {{ calcTotalSales | number:'1.2-2' }}</span>
              </div>
              <div class="total-block">
                <label for="trip-collected">Collected from customers</label>
                <nz-input-number nzId="trip-collected" [(ngModel)]="reconcileCollections" [nzMin]="0" style="width: 180px"></nz-input-number>
              </div>
            </div>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Trip Detail Drawer -->
      <nz-drawer
        [nzVisible]="isDrawerVisible()"
        [nzTitle]="selectedTrip() ? 'Trip ' + selectedTrip()!.tripNumber : 'Trip'"
        [nzWidth]="720"
        (nzOnClose)="isDrawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedTrip() as trip" class="drawer-content">
            <div class="detail-banner">
              <div>
                <h3>{{ trip.routeName }}</h3>
                <span>Left on {{ trip.departureDate | erpDate }}@if (trip.returnDate) {, back on {{ trip.returnDate | erpDate }}}</span>
              </div>
              <div style="display: flex; gap: 8px; align-items: center;">
                <a [href]="'/print/manifest/' + trip.id" target="_blank" nz-button nzType="default" nzSize="small">
                  <span nz-icon nzType="printer"></span> Print Manifest
                </a>
                <erp-status [value]="trip.status" />
              </div>
            </div>

            <div class="stats-row">
              <div class="stat-box">
                <span class="text-secondary">Driver</span>
                <strong>{{ trip.driverName }}</strong>
              </div>
              <div class="stat-box">
                <span class="text-secondary">Vehicle</span>
                <strong>{{ trip.vehicleNumber }}</strong>
              </div>
              <div class="stat-box">
                <span class="text-secondary">Sales</span>
                <strong>{{ currency() }} {{ trip.totalSalesAmount | number:'1.2-2' }}</strong>
              </div>
              <div class="stat-box">
                <span class="text-secondary">Collected</span>
                <strong>{{ currency() }} {{ trip.totalCollectionsAmount | number:'1.2-2' }}</strong>
              </div>
            </div>

            <div class="manifest-section">
              <h4>Stock on this trip</h4>
              <nz-table #manTable [nzData]="trip.inventoryItems" nzSize="small" [nzBordered]="true" [nzShowPagination]="false" [nzFrontPagination]="false" nzNoResult="No items were recorded as loaded on this trip.">
                <thead>
                  <tr>
                    <th>Item</th>
                    <th nzAlign="right">Loaded</th>
                    <th nzAlign="right">Sold</th>
                    <th nzAlign="right">Returned</th>
                    <th nzAlign="right">Damaged</th>
                    <th nzAlign="right">Rate</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let it of manTable.data">
                    <td>
                      <strong>{{ it.itemName }}</strong>
                      <div><small class="mono-code">{{ it.itemCode }}</small></div>
                    </td>
                    <td nzAlign="right">{{ it.loadedQuantity }}</td>
                    <td nzAlign="right" class="font-bold">{{ it.soldQuantity }}</td>
                    <td nzAlign="right">{{ it.returnedQuantity }}</td>
                    <td nzAlign="right">{{ it.damagedQuantity }}</td>
                    <td nzAlign="right" class="num">{{ currency() }} {{ it.unitPrice | number:'1.2-2' }}</td>
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
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .row-3 {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 16px;
    }
    .items-add-bar {
      display: flex;
      gap: 12px;
      align-items: center;
    }
    .text-center {
      text-align: center;
    }
    .py-3 {
      padding-top: 12px;
      padding-bottom: 12px;
    }
    .reconcile-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .reconcile-totals {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px;
      background: var(--erp-color-bg-subtle);
      border-radius: 6px;
    }
    .total-block {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .drawer-content {
      display: flex;
      flex-direction: column;
      gap: 20px;
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
    .stats-row {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
    }
    .stat-box {
      background: var(--erp-color-bg-subtle);
      padding: 12px;
      border-radius: 6px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
  `]
})
export class VanTripsPageComponent implements OnInit {
  private readonly tripsApi = inject(DistributionTripsApi);
  private readonly routesApi = inject(DistributionRoutesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly routesList = signal<DistributionRouteSummaryDto[]>([]);
  readonly catalogItems = signal<{ id: string; name: string; sku: string; uom: string; salesPrice?: number }[]>([]);
  readonly loadedItems = signal<{ itemId: string; itemCode: string; itemName: string; uom: string; loadedQuantity: number; unitPrice: number }[]>([]);
  readonly selectedTrip = signal<VanSalesTripDto | null>(null);
  readonly reconcileTrip = signal<VanSalesTripDto | null>(null);

  readonly isSaving = signal(false);
  readonly isSavingReconcile = signal(false);

  readonly isDispatchModalVisible = signal(false);
  readonly isReconcileModalVisible = signal(false);
  readonly isDrawerVisible = signal(false);

  selectedAddItemId: string | null = null;
  selectedAddQty = 1;

  reconcileLines: { itemId: string; itemName: string; loadedQuantity: number; soldQuantity: number; returnedQuantity: number; damagedQuantity: number; unitPrice: number }[] = [];
  calcTotalSales = 0;
  reconcileCollections = 0;

  readonly list = createPagedList<VanSalesTripSummaryDto>((q) => this.tripsApi.list(q), { sort: '-createdAt' });

  readonly columns: TableColumn<VanSalesTripSummaryDto>[] = [
    { key: 'tripNumber', label: 'Trip', sortField: 'tripNumber', width: '165px' },
    { key: 'routeName', label: 'Route', width: '190px' },
    { key: 'driverName', label: 'Driver and van' },
    { key: 'vehicleNumber', label: 'Van plate', hidden: true },
    { key: 'departureDate', label: 'Departure' },
    { key: 'status', label: 'Status' },
    { key: 'totalSalesAmount', label: 'Sales', align: 'right' },
    { key: 'totalCollectionsAmount', label: 'Collected', align: 'right' },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, width: '205px', value: () => '' }
  ];

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    routeId: ['', [Validators.required]],
    driverUserId: [null],
    driverName: ['', [Validators.required]],
    vehicleNumber: ['', [Validators.required]],
    warehouseId: ['', [Validators.required]],
    departureDate: [new Date(), [Validators.required]]
  });

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.warehouses.set(res.items)
    });
    this.routesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.routesList.set(res.items)
    });
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.catalogItems.set(res.items.filter(i => i.type === 'product' && i.isActive))
    });
  }

  onRouteSelected(routeId: string): void {
    const route = this.routesList().find(r => r.id === routeId);
    if (route) {
      this.form.patchValue({
        driverName: route.driverName || '',
        vehicleNumber: route.vehicleNumber || '',
        warehouseId: route.warehouseId || this.warehouses()[0]?.id || ''
      });
    }
  }

  openDispatchModal(): void {
    const user = this.sessionStore.me()?.user;
    if (!user?.id) {
      this.msg.error('User authentication is missing.');
      return;
    }
    const legalEntityId = this.entities()[0]?.id || '';
    if (!legalEntityId) {
      this.msg.error('No legal entity available.');
      return;
    }

    this.loadedItems.set([]);
    this.form.reset({
      legalEntityId,
      routeId: this.routesList()[0]?.id || '',
      driverUserId: user.id,
      driverName: this.routesList()[0]?.driverName || user.fullName || '',
      vehicleNumber: this.routesList()[0]?.vehicleNumber || '',
      warehouseId: this.routesList()[0]?.warehouseId || this.warehouses()[0]?.id || '',
      departureDate: new Date()
    });
    this.isDispatchModalVisible.set(true);
  }

  addLoadedItem(): void {
    if (!this.selectedAddItemId || this.selectedAddQty <= 0) return;
    const item = this.catalogItems().find(i => i.id === this.selectedAddItemId);
    if (!item) return;

    const current = this.loadedItems();
    const existingIdx = current.findIndex(i => i.itemId === item.id);
    if (existingIdx >= 0) {
      current[existingIdx].loadedQuantity += this.selectedAddQty;
      this.loadedItems.set([...current]);
    } else {
      this.loadedItems.set([
        ...current,
        {
          itemId: item.id,
          itemCode: item.sku,
          itemName: item.name,
          uom: item.uom || 'PCS',
          loadedQuantity: this.selectedAddQty,
          unitPrice: item.salesPrice ?? 0
        }
      ]);
    }
  }

  removeLoadedItem(idx: number): void {
    const current = [...this.loadedItems()];
    current.splice(idx, 1);
    this.loadedItems.set(current);
  }

  saveTrip(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach(c => { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); });
      return;
    }
    if (this.loadedItems().length === 0) {
      this.msg.warning('Load at least one item onto the van.');
      return;
    }

    this.isSaving.set(true);
    const val = this.form.value;
    const user = this.sessionStore.me()?.user;
    const driverUserId = val.driverUserId || user?.id;
    if (!driverUserId) {
      this.isSaving.set(false);
      this.msg.error('Driver user ID is required.');
      return;
    }
    if (!val.legalEntityId) {
      this.isSaving.set(false);
      this.msg.error('Legal entity is required.');
      return;
    }
    const input: CreateTripInput = {
      legalEntityId: val.legalEntityId,
      routeId: val.routeId,
      driverUserId,
      driverName: val.driverName,
      vehicleNumber: val.vehicleNumber,
      warehouseId: val.warehouseId,
      departureDate: toIsoDate(new Date(val.departureDate)),
      items: this.loadedItems().map(i => ({
        itemId: i.itemId,
        itemCode: i.itemCode,
        itemName: i.itemName,
        uom: i.uom,
        loadedQuantity: i.loadedQuantity,
        unitPrice: i.unitPrice
      }))
    };

    this.tripsApi.create(input).subscribe({
      next: () => {
        this.msg.success('Van trip dispatched');
        this.isSaving.set(false);
        this.isDispatchModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The trip could not be dispatched'));
        this.isSaving.set(false);
      }
    });
  }

  viewDetail(row: VanSalesTripSummaryDto): void {
    this.tripsApi.getDetail(row.id).subscribe({
      next: detail => {
        this.selectedTrip.set(detail);
        this.isDrawerVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The trip could not be loaded'))
    });
  }

  openReconcileModal(row: VanSalesTripSummaryDto): void {
    this.tripsApi.getDetail(row.id).subscribe({
      next: detail => {
        this.reconcileTrip.set(detail);
        this.reconcileLines = detail.inventoryItems.map(i => ({
          itemId: i.itemId,
          itemName: i.itemName,
          loadedQuantity: i.loadedQuantity,
          soldQuantity: i.soldQuantity || i.loadedQuantity,
          returnedQuantity: i.returnedQuantity || 0,
          damagedQuantity: i.damagedQuantity || 0,
          unitPrice: i.unitPrice
        }));
        this.calculateTripTotals();
        // A trip with no lines keeps the sales it already has, rather than dropping them to zero.
        if (this.reconcileLines.length === 0) this.calcTotalSales = detail.totalSalesAmount;
        this.reconcileCollections = detail.totalCollectionsAmount || this.calcTotalSales;
        this.isReconcileModalVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The trip could not be loaded'))
    });
  }

  calculateTripTotals(): void {
    // Rounded to cents: the server refuses amounts with more decimals.
    this.calcTotalSales = Math.round(this.reconcileLines.reduce((acc, curr) => acc + (curr.soldQuantity * curr.unitPrice), 0) * 100) / 100;
  }

  saveReconciliation(): void {
    const trip = this.reconcileTrip();
    if (!trip) return;
    const over = this.reconcileLines.find(l => l.soldQuantity + l.returnedQuantity + l.damagedQuantity > l.loadedQuantity);
    if (over) {
      this.msg.warning(`${over.itemName}: sold, returned and damaged are more than the ${over.loadedQuantity} loaded.`);
      return;
    }

    this.isSavingReconcile.set(true);
    const input: CompleteTripInput = {
      tripId: trip.id,
      totalSalesAmount: this.calcTotalSales,
      totalCollectionsAmount: this.reconcileCollections,
      items: this.reconcileLines.map(l => ({
        itemId: l.itemId,
        soldQuantity: l.soldQuantity,
        returnedQuantity: l.returnedQuantity,
        damagedQuantity: l.damagedQuantity
      })),
      notes: 'End of day field van reconciliation'
    };

    this.tripsApi.completeTrip(trip.id, input).subscribe({
      next: () => {
        this.msg.success('Trip reconciled and completed');
        this.isSavingReconcile.set(false);
        this.isReconcileModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The trip could not be reconciled'));
        this.isSavingReconcile.set(false);
      }
    });
  }
}
