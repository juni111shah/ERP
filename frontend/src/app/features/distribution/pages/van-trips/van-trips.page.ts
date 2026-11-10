import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { ItemsApi } from '@core/api/admin.api';
import {
  CreateTripInput,
  DistributionRouteSummaryDto,
  DistributionRoutesApi,
  DistributionTripsApi,
  VanSalesTripDto,
  VanSalesTripSummaryDto,
} from '@core/api/distribution.api';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { toIsoDate } from '@core/format/dates';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { DistributionLookups, NamedOption } from '../../distribution-lookups';
import {
  LoadedItem,
  ReconcileLine,
  TripCatalogItem,
  TripFormValue,
  addToLoad,
  overAccountedLine,
  toCompleteTripInput,
  toReconcileLines,
  toTripItems,
  tripSalesTotal,
  vanProducts,
} from '../../distribution-trips';

/** Van sales trips: the paged list, dispatching a loaded van, a trip's detail, and the end-of-day reconciliation. */
@Component({
  selector: 'erp-van-trips-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NzBadgeModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzDividerModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
    NzToolTipModule,
    ErpDatePipe,
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  providers: [DistributionLookups],
  templateUrl: './van-trips.page.html',
  styleUrl: './van-trips.page.scss'
})
export class VanTripsPageComponent implements OnInit {
  private readonly tripsApi = inject(DistributionTripsApi);
  private readonly routesApi = inject(DistributionRoutesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly lookups = inject(DistributionLookups);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly entities = this.lookups.entities;
  readonly warehouses = this.lookups.warehouses;
  readonly routesList = signal<DistributionRouteSummaryDto[]>([]);
  readonly catalogItems = signal<TripCatalogItem[]>([]);
  readonly loadedItems = signal<LoadedItem[]>([]);
  readonly selectedTrip = signal<VanSalesTripDto | null>(null);
  readonly reconcileTrip = signal<VanSalesTripDto | null>(null);

  readonly isSaving = signal(false);
  readonly isSavingReconcile = signal(false);
  readonly isDispatchModalVisible = signal(false);
  readonly isReconcileModalVisible = signal(false);
  readonly isDrawerVisible = signal(false);

  reconcileCollections = 0;
  reconcileCustomer: string | null = null;
  reconcileAllowUnaccounted = false;
  readonly customers = signal<NamedOption[]>([]);

  selectedAddItemId: string | null = null;
  selectedAddQty = 1;

  reconcileLines: ReconcileLine[] = [];
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
    this.lookups.loadEntities();
    this.lookups.loadWarehouses();
    this.routesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => this.routesList.set(res.items)
    });
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => this.catalogItems.set(vanProducts(res.items))
    });
  }

  searchCustomers(term: string): void {
    loadInto(this.customers, this.partiesApi.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Customer' } }), (r) =>
      r.items.map((p) => ({ id: p.id, name: p.name })));
  }

  onRouteSelected(routeId: string): void {
    const route = this.routesList().find((r) => r.id === routeId);
    if (!route) return;
    this.form.patchValue({
      driverName: route.driverName || '',
      vehicleNumber: route.vehicleNumber || '',
      warehouseId: route.warehouseId || this.warehouses()[0]?.id || ''
    });
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

    const firstRoute = this.routesList()[0];
    this.loadedItems.set([]);
    this.form.reset({
      legalEntityId,
      routeId: firstRoute?.id || '',
      driverUserId: user.id,
      driverName: firstRoute?.driverName || user.fullName || '',
      vehicleNumber: firstRoute?.vehicleNumber || '',
      warehouseId: firstRoute?.warehouseId || this.warehouses()[0]?.id || '',
      departureDate: new Date()
    });
    this.isDispatchModalVisible.set(true);
  }

  addLoadedItem(): void {
    if (!this.selectedAddItemId || this.selectedAddQty <= 0) return;
    const item = this.catalogItems().find((i) => i.id === this.selectedAddItemId);
    if (!item) return;
    this.loadedItems.set(addToLoad(this.loadedItems(), item, this.selectedAddQty));
  }

  removeLoadedItem(idx: number): void {
    const current = [...this.loadedItems()];
    current.splice(idx, 1);
    this.loadedItems.set(current);
  }

  saveTrip(): void {
    if (this.form.invalid) {
      revealErrors(this.form, { onlySelf: true });
      return;
    }
    if (this.loadedItems().length === 0) {
      this.msg.warning('Load at least one item onto the van.');
      return;
    }

    this.isSaving.set(true);
    const val = this.form.value as TripFormValue;
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
      items: toTripItems(this.loadedItems())
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
      next: (detail) => {
        this.selectedTrip.set(detail);
        this.isDrawerVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The trip could not be loaded'))
    });
  }

  openReconcileModal(row: VanSalesTripSummaryDto): void {
    this.tripsApi.getDetail(row.id).subscribe({
      next: (detail) => {
        this.reconcileTrip.set(detail);
        this.reconcileLines = toReconcileLines(detail);
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
    this.calcTotalSales = tripSalesTotal(this.reconcileLines);
  }

  saveReconciliation(): void {
    const trip = this.reconcileTrip();
    if (!trip) return;
    const over = overAccountedLine(this.reconcileLines);
    if (over) {
      this.msg.warning(`${over.itemName}: sold, returned and damaged are more than the ${over.loadedQuantity} loaded.`);
      return;
    }

    this.isSavingReconcile.set(true);
    const input = toCompleteTripInput(
      trip.id,
      this.reconcileLines,
      { sales: this.calcTotalSales, collections: this.reconcileCollections },
      this.reconcileCustomer,
      this.reconcileAllowUnaccounted,
    );

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
