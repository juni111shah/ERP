import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
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
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import {
  CompleteTripInput,
  CreateTripInput,
  DistributionRoutesApi,
  DistributionRouteSummaryDto,
  DistributionTripsApi,
  TripStatus,
  VanSalesTripDto,
  VanSalesTripSummaryDto
} from '../../core/api/distribution.api';
import { WarehousesApi } from '../../modules/inventory/inventory.api';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-van-trips-page',
  standalone: true,
  imports: [
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
    PagedTableComponent
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Van Sales & Mobile Dispatch Trips</h2>
          <p class="text-secondary">Dispatch stock onto mobile delivery vans, track en-route field sales and reconcile end-of-day stock and cash</p>
        </div>
        <button nz-button nzType="primary" (click)="openDispatchModal()">
          <span nz-icon nzType="send"></span> Dispatch Van Trip
        </button>
      </div>

      <nz-card [nzBordered]="false">
        <erp-paged-table
          #table
          [list]="list"
          [columns]="columns">
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'tripNumber'" class="mono-code font-bold">{{ row.tripNumber }}</span>
              <span *ngSwitchCase="'routeName'">
                <a (click)="viewDetail(row)">{{ row.routeName }}</a>
              </span>
              <span *ngSwitchCase="'driverName'">
                <span>{{ row.driverName }}</span>
              </span>
              <span *ngSwitchCase="'vehicleNumber'">
                <nz-tag nzColor="blue">{{ row.vehicleNumber }}</nz-tag>
              </span>
              <span *ngSwitchCase="'status'">
                <nz-tag [nzColor]="getStatusColor(row.status)">{{ row.status }}</nz-tag>
              </span>
              <span *ngSwitchCase="'totalSalesAmount'" class="font-bold">
                AED {{ row.totalSalesAmount | number:'1.2-2' }}
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row)">
                  <span nz-icon nzType="eye"></span> Detail
                </button>
                <button *ngIf="row.status === 'InProgress'" nz-button nzType="link" nzSize="small" (click)="openReconcileModal(row)">
                  <span nz-icon nzType="check-circle"></span> Reconcile
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>
      </nz-card>

      <!-- Dispatch Van Trip Modal -->
      <nz-modal
        [(nzVisible)]="isDispatchModalVisible"
        nzTitle="Dispatch New Van Sales Trip"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="isDispatchModalVisible = false"
        (nzOnOk)="saveTrip()"
        nzWidth="820px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-3">
              <nz-form-item>
                <nz-form-label nzRequired>Workspace Entity</nz-form-label>
                <nz-form-control nzErrorTip="Entity is required">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Delivery Route</nz-form-label>
                <nz-form-control nzErrorTip="Route is required">
                  <nz-select formControlName="routeId" (ngModelChange)="onRouteSelected($event)" nzPlaceHolder="Select Route">
                    <nz-option *ngFor="let rt of routesList()" [nzValue]="rt.id" [nzLabel]="rt.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Departure Date</nz-form-label>
                <nz-form-control nzErrorTip="Date is required">
                  <nz-date-picker formControlName="departureDate" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label nzRequired>Driver Name</nz-form-label>
                <nz-form-control nzErrorTip="Driver is required">
                  <input nz-input formControlName="driverName" placeholder="e.g. Tariq Mahmoud" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Vehicle Plate No.</nz-form-label>
                <nz-form-control nzErrorTip="Vehicle plate is required">
                  <input nz-input formControlName="vehicleNumber" placeholder="e.g. DXB-4521" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Fulfillment Warehouse</nz-form-label>
                <nz-form-control nzErrorTip="Warehouse is required">
                  <nz-select formControlName="warehouseId" nzPlaceHolder="Select Warehouse">
                    <nz-option *ngFor="let wh of warehouses()" [nzValue]="wh.id" [nzLabel]="wh.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-divider nzText="Van Loaded Stock Manifest"></nz-divider>

            <div class="items-add-bar">
              <nz-select [(ngModel)]="selectedAddItemId" [ngModelOptions]="{standalone: true}" style="flex: 2" nzPlaceHolder="Select item to load onto van">
                <nz-option *ngFor="let itm of catalogItems()" [nzValue]="itm.id" [nzLabel]="itm.name + ' (' + itm.sku + ')'"></nz-option>
              </nz-select>
              <nz-input-number [(ngModel)]="selectedAddQty" [ngModelOptions]="{standalone: true}" [nzMin]="1" style="width: 120px" placeholder="Qty"></nz-input-number>
              <button nz-button nzType="dashed" (click)="addLoadedItem()">
                <span nz-icon nzType="plus"></span> Add To Van
              </button>
            </div>

            <nz-table #loadedTable [nzData]="loadedItems()" nzSize="small" [nzBordered]="true" style="margin-top: 12px">
              <thead>
                <tr>
                  <th>Item SKU & Description</th>
                  <th nzAlign="right">Loaded Qty</th>
                  <th nzAlign="right">Unit Price</th>
                  <th nzAlign="right">Est. Value</th>
                  <th nzAlign="right">Remove</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of loadedTable.data; let idx = index">
                  <td>
                    <strong>{{ row.itemName }}</strong>
                    <div><small class="mono-code">{{ row.itemCode }}</small></div>
                  </td>
                  <td nzAlign="right">{{ row.loadedQuantity }} {{ row.uom }}</td>
                  <td nzAlign="right">AED {{ row.unitPrice | number:'1.2-2' }}</td>
                  <td nzAlign="right" class="font-bold">AED {{ (row.loadedQuantity * row.unitPrice) | number:'1.2-2' }}</td>
                  <td nzAlign="right">
                    <button nz-button nzType="link" nzDanger (click)="removeLoadedItem(idx)">
                      <span nz-icon nzType="delete"></span>
                    </button>
                  </td>
                </tr>
                <tr *ngIf="loadedItems().length === 0">
                  <td colspan="5" class="text-center text-secondary py-3">No inventory items loaded onto the van yet.</td>
                </tr>
              </tbody>
            </nz-table>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Trip Reconciliation Modal -->
      <nz-modal
        [(nzVisible)]="isReconcileModalVisible"
        nzTitle="Reconcile & Complete Van Sales Trip"
        [nzOkLoading]="isSavingReconcile()"
        (nzOnCancel)="isReconcileModalVisible = false"
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

            <nz-table #recTable [nzData]="reconcileLines" nzSize="small" [nzBordered]="true" [nzShowPagination]="false">
              <thead>
                <tr>
                  <th>Item Description</th>
                  <th nzAlign="right">Loaded</th>
                  <th nzAlign="right" style="width: 110px">Sold Qty</th>
                  <th nzAlign="right" style="width: 110px">Returned</th>
                  <th nzAlign="right" style="width: 110px">Damaged</th>
                  <th nzAlign="right">Line Sales</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of recTable.data">
                  <td><strong>{{ item.itemName }}</strong></td>
                  <td nzAlign="right">{{ item.loadedQuantity }}</td>
                  <td nzAlign="right">
                    <nz-input-number [(ngModel)]="item.soldQuantity" [nzMin]="0" [nzMax]="item.loadedQuantity" nzSize="small" (ngModelChange)="calculateTripTotals()"></nz-input-number>
                  </td>
                  <td nzAlign="right">
                    <nz-input-number [(ngModel)]="item.returnedQuantity" [nzMin]="0" nzSize="small"></nz-input-number>
                  </td>
                  <td nzAlign="right">
                    <nz-input-number [(ngModel)]="item.damagedQuantity" [nzMin]="0" nzSize="small"></nz-input-number>
                  </td>
                  <td nzAlign="right" class="font-bold">
                    AED {{ (item.soldQuantity * item.unitPrice) | number:'1.2-2' }}
                  </td>
                </tr>
              </tbody>
            </nz-table>

            <div class="reconcile-totals">
              <div class="total-block">
                <span>Total Net Sales:</span>
                <span class="font-bold" style="font-size: 18px; color: #1890ff">AED {{ calcTotalSales | number:'1.2-2' }}</span>
              </div>
              <div class="total-block">
                <span>Cash/Tender Collections Reconciled:</span>
                <nz-input-number [(ngModel)]="reconcileCollections" [nzMin]="0" style="width: 180px"></nz-input-number>
              </div>
            </div>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Trip Detail Drawer -->
      <nz-drawer
        [nzVisible]="isDrawerVisible"
        nzTitle="Van Sales Trip Manifest & Actuals"
        [nzWidth]="720"
        (nzOnClose)="isDrawerVisible = false">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedTrip() as trip" class="drawer-content">
            <div class="detail-banner">
              <div>
                <h3>Trip {{ trip.tripNumber }}</h3>
                <span>{{ trip.routeName }} &bull; {{ trip.departureDate }}</span>
              </div>
              <div>
                <nz-tag [nzColor]="getStatusColor(trip.status)">{{ trip.status }}</nz-tag>
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
                <span class="text-secondary">Total Sales</span>
                <strong style="color: #52c41a">AED {{ trip.totalSalesAmount | number:'1.2-2' }}</strong>
              </div>
              <div class="stat-box">
                <span class="text-secondary">Collections</span>
                <strong style="color: #1890ff">AED {{ trip.totalCollectionsAmount | number:'1.2-2' }}</strong>
              </div>
            </div>

            <div class="manifest-section">
              <h4>Stock Reconciliation Manifest</h4>
              <nz-table #manTable [nzData]="trip.inventoryItems" nzSize="small" [nzBordered]="true">
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
                    <td nzAlign="right">AED {{ it.unitPrice | number:'1.2-2' }}</td>
                  </tr>
                </tbody>
              </nz-table>
            </div>
          </div>
        </ng-container>
      </nz-drawer>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 24px;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .page-header h2 {
      margin: 0;
      font-size: 24px;
      font-weight: 600;
    }
    .text-secondary {
      color: #8c8c8c;
      margin: 4px 0 0;
    }
    .mono-code {
      font-family: monospace;
      color: #1890ff;
    }
    .font-bold {
      font-weight: 600;
    }
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
      background: #fafafa;
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
      border-bottom: 1px solid #f0f0f0;
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
      background: #f5f5f5;
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
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly routesList = signal<DistributionRouteSummaryDto[]>([]);
  readonly catalogItems = signal<{ id: string; name: string; sku: string; salesPrice?: number }[]>([]);
  readonly loadedItems = signal<{ itemId: string; itemCode: string; itemName: string; uom: string; loadedQuantity: number; unitPrice: number }[]>([]);
  readonly selectedTrip = signal<VanSalesTripDto | null>(null);
  readonly reconcileTrip = signal<VanSalesTripDto | null>(null);

  readonly isSaving = signal(false);
  readonly isSavingReconcile = signal(false);

  isDispatchModalVisible = false;
  isReconcileModalVisible = false;
  isDrawerVisible = false;

  selectedAddItemId: string | null = null;
  selectedAddQty = 20;

  reconcileLines: { itemId: string; itemName: string; loadedQuantity: number; soldQuantity: number; returnedQuantity: number; damagedQuantity: number; unitPrice: number }[] = [];
  calcTotalSales = 0;
  reconcileCollections = 0;

  readonly list = createPagedList<VanSalesTripSummaryDto>((q: any) => this.tripsApi.list(q));

  readonly columns: TableColumn<VanSalesTripSummaryDto>[] = [
    { key: 'tripNumber', label: 'Trip #', sortField: 'tripNumber' },
    { key: 'routeName', label: 'Route' },
    { key: 'driverName', label: 'Driver' },
    { key: 'vehicleNumber', label: 'Van Plate' },
    { key: 'departureDate', label: 'Departure' },
    { key: 'status', label: 'Status' },
    { key: 'totalSalesAmount', label: 'Sales Total', align: 'right' },
    { key: 'actions', label: 'Actions', align: 'right' }
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
      next: res => this.catalogItems.set(res.items)
    });
  }

  getStatusColor(status: TripStatus): string {
    switch (status) {
      case 'InProgress': return 'processing';
      case 'Completed': return 'success';
      case 'Cancelled': return 'error';
      default: return 'default';
    }
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
    this.loadedItems.set([]);
    this.form.reset({
      legalEntityId: this.entities()[0]?.id || '',
      routeId: this.routesList()[0]?.id || '',
      driverUserId: '00000000-0000-0000-0000-000000000001',
      driverName: this.routesList()[0]?.driverName || 'Driver 1',
      vehicleNumber: this.routesList()[0]?.vehicleNumber || 'DXB-5510',
      warehouseId: this.warehouses()[0]?.id || '',
      departureDate: new Date()
    });
    this.isDispatchModalVisible = true;
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
          uom: 'PCS',
          loadedQuantity: this.selectedAddQty,
          unitPrice: item.salesPrice || 15
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
    if (this.form.invalid) return;
    if (this.loadedItems().length === 0) {
      this.msg.warning('Please load at least one product line onto the van.');
      return;
    }

    this.isSaving.set(true);
    const val = this.form.value;
    const input: CreateTripInput = {
      legalEntityId: val.legalEntityId,
      routeId: val.routeId,
      driverUserId: val.driverUserId || '00000000-0000-0000-0000-000000000001',
      driverName: val.driverName,
      vehicleNumber: val.vehicleNumber,
      warehouseId: val.warehouseId,
      departureDate: new Date(val.departureDate).toISOString().split('T')[0],
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
        this.msg.success('Van sales trip dispatched');
        this.isSaving.set(false);
        this.isDispatchModalVisible = false;
        window.location.reload();
      },
      error: () => this.isSaving.set(false)
    });
  }

  viewDetail(row: VanSalesTripSummaryDto): void {
    this.tripsApi.getDetail(row.id).subscribe({
      next: detail => {
        this.selectedTrip.set(detail);
        this.isDrawerVisible = true;
      },
      error: () => this.msg.error('Failed to load trip manifest')
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
        this.reconcileCollections = this.calcTotalSales;
        this.isReconcileModalVisible = true;
      }
    });
  }

  calculateTripTotals(): void {
    this.calcTotalSales = this.reconcileLines.reduce((acc, curr) => acc + (curr.soldQuantity * curr.unitPrice), 0);
  }

  saveReconciliation(): void {
    const trip = this.reconcileTrip();
    if (!trip) return;

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
        this.msg.success('Trip reconciled and marked completed');
        this.isSavingReconcile.set(false);
        this.isReconcileModalVisible = false;
        window.location.reload();
      },
      error: () => this.isSavingReconcile.set(false)
    });
  }
}
