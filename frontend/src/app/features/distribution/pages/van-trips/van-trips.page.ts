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
import { ItemsApi, LegalEntitiesApi } from '@core/api/admin.api';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { createPagedList } from '@core/api/paged-list';
import {
  CompleteTripInput,
  CreateTripInput,
  DistributionRoutesApi,
  DistributionRouteSummaryDto,
  DistributionTripsApi,
  VanSalesTripDto,
  VanSalesTripSummaryDto
} from '@core/api/distribution.api';
import { WarehousesApi } from '@core/api/inventory.api';
import { PartiesApi } from '@core/api/parties.api';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { toIsoDate } from '@core/format/dates';
import { problemMessage } from '@core/http/problem-details';

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
  templateUrl: './van-trips.page.html',
  styleUrl: './van-trips.page.scss'
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

  reconcileCollections = 0;
  reconcileCustomer: string | null = null;
  reconcileAllowUnaccounted = false;
  readonly customers = signal<{ id: string; name: string }[]>([]);
  private readonly partiesApi = inject(PartiesApi);

  searchCustomers(term: string): void {
    this.partiesApi.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Customer' } }).subscribe({ next: (r) => this.customers.set(r.items.map((p) => ({ id: p.id, name: p.name }))), error: () => undefined });
  }
  readonly isReconcileModalVisible = signal(false);
  readonly isDrawerVisible = signal(false);

  selectedAddItemId: string | null = null;
  selectedAddQty = 1;

  reconcileLines: { itemId: string; itemName: string; loadedQuantity: number; soldQuantity: number; returnedQuantity: number; damagedQuantity: number; unitPrice: number }[] = [];
  calcTotalSales = 0;

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
      next: (created) => {
        this.msg.success(`Van trip ${created.tripNumber} dispatched`);
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
        this.reconcileCustomer = null;
        this.reconcileAllowUnaccounted = false;
        this.searchCustomers('');
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
      customerPartyId: this.reconcileCustomer,
      allowUnaccounted: this.reconcileAllowUnaccounted,
      notes: 'End of day field van reconciliation'
    };

    this.tripsApi.completeTrip(trip.id, input).subscribe({
      next: (done) => {
        this.msg.success(done.invoiceNumber ? `Trip ${trip.tripNumber} completed and invoice ${done.invoiceNumber} issued` : `Trip ${trip.tripNumber} reconciled and completed`);
        for (const w of done.warnings ?? []) this.msg.warning(w);
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
