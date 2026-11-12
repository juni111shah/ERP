import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, map, switchMap } from 'rxjs';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
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
import {
  CreateRouteInput,
  DeliveryStopDto,
  DeliveryStopInput,
  DistributionRouteDto,
  DistributionRouteSummaryDto,
  DistributionRoutesApi,
} from '@core/api/distribution.api';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { setControlsEnabled } from '@shared/forms/set-controls-enabled';
import { DistributionLookups, NamedOption, customerOptions } from '../../distribution-lookups';
import {
  RouteFormValue,
  StopFormValue,
  emptyRouteForm,
  routeFormOf,
  routeKeptChange,
  routeWithStops,
  toNewStopInput,
  toRouteInput,
  toStopInputs,
} from '../../distribution-routes';

/** Delivery routes: the paged list, the route form, and a route's stops in a drawer (add and remove). */
@Component({
  selector: 'erp-routes-list-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzBadgeModule,
    NzButtonModule,
    NzCardModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
    NzTagModule,
    NzToolTipModule,
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  providers: [DistributionLookups],
  templateUrl: './routes-list.page.html',
  styleUrl: './routes-list.page.scss'
})
export class RoutesListPageComponent implements OnInit {
  private readonly routesApi = inject(DistributionRoutesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly lookups = inject(DistributionLookups);
  private readonly session = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = this.lookups.entities;
  readonly warehouses = this.lookups.warehouses;
  readonly customers = signal<NamedOption[]>([]);
  readonly selectedRoute = signal<DistributionRouteDto | null>(null);
  readonly isSaving = signal(false);
  readonly editing = signal<DistributionRouteDto | null>(null);
  readonly isSavingStop = signal(false);

  readonly isModalVisible = signal(false);
  readonly isDrawerVisible = signal(false);
  readonly isStopModalVisible = signal(false);

  readonly list = createPagedList<DistributionRouteSummaryDto>((q) => this.routesApi.list(q), { sort: '-createdAt' });

  readonly columns: TableColumn<DistributionRouteSummaryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'driverName', label: 'Driver' },
    { key: 'vehicleNumber', label: 'Vehicle plate' },
    { key: 'stopCount', label: 'Stops', align: 'right' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    description: [''],
    assignedDriverUserId: [null],
    driverName: [''],
    vehicleNumber: [''],
    vehicleCapacityKg: [null],
    warehouseId: [null],
    isActive: [true]
  });

  stopForm: FormGroup = this.fb.group({
    customerPartyId: ['', [Validators.required]],
    customerName: ['', [Validators.required]],
    sequenceOrder: [1, [Validators.required]],
    address: [''],
    contactPhone: [''],
    preferredTimeWindow: ['']
  });

  ngOnInit(): void {
    this.lookups.loadEntities();
    this.lookups.loadWarehouses();
    this.partiesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => this.customers.set(customerOptions(res.items))
    });
  }

  openEditModal(row: DistributionRouteSummaryDto): void {
    this.routesApi.getDetail(row.id).subscribe({
      next: (detail) => {
        this.editing.set(detail);
        this.form.reset(routeFormOf(detail));
        this.setEntityEnabled(false);
        this.isModalVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The route could not be loaded'))
    });
  }

  deleteRoute(row: DistributionRouteSummaryDto): void {
    this.routesApi.delete(row.id).subscribe({
      next: () => {
        this.msg.success(doneText('deleted', 'Route', row.name));
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The route could not be deleted'))
    });
  }

  openCreateModal(): void {
    this.editing.set(null);
    this.setEntityEnabled(true);
    this.form.reset(emptyRouteForm(this.lookups.defaultEntityId(this.session.currentEntityId())));
    this.isModalVisible.set(true);
  }

  viewDetail(row: DistributionRouteSummaryDto): void {
    this.routesApi.getDetail(row.id).subscribe({
      next: (detail) => {
        this.selectedRoute.set(detail);
        this.isDrawerVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The route could not be loaded'))
    });
  }

  saveRoute(): void {
    if (this.form.invalid) {
      // Only the invalid fields are marked.
      Object.values(this.form.controls).forEach((c) => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    this.isSaving.set(true);
    const val = this.form.getRawValue() as RouteFormValue;
    const editing = this.editing();
    const input = toRouteInput(val, editing);

    (editing ? this.updateAndVerify(editing.id, input) : this.routesApi.create(input)).subscribe({
      next: () => {
        this.msg.success(doneText('saved', 'Route', val.name));
        this.isSaving.set(false);
        this.isModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The route could not be saved'));
        this.isSaving.set(false);
      }
    });
  }

  openAddStopModal(): void {
    const route = this.selectedRoute();
    const nextSeq = (route?.stops.length || 0) + 1;
    this.stopForm.reset({
      customerPartyId: this.customers()[0]?.id || '',
      customerName: this.customers()[0]?.name || '',
      sequenceOrder: nextSeq,
      address: '',
      contactPhone: '',
      preferredTimeWindow: ''
    });
    this.isStopModalVisible.set(true);
  }

  onCustomerSelected(customerId: string): void {
    const cust = this.customers().find((c) => c.id === customerId);
    if (cust) this.stopForm.patchValue({ customerName: cust.name });
  }

  saveStop(): void {
    if (this.stopForm.invalid) return;
    const route = this.selectedRoute();
    if (!route) return;

    this.isSavingStop.set(true);
    const stopVal = this.stopForm.value as StopFormValue;
    const stops = [...toStopInputs(route.stops), toNewStopInput(stopVal)];

    this.saveStops(route, stops).subscribe({
      next: () => {
        this.msg.success(`Stop ${stopVal.customerName} added to route ${route.name}`);
        this.isSavingStop.set(false);
        this.isStopModalVisible.set(false);
        this.viewDetail(route);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The stop could not be added'));
        this.isSavingStop.set(false);
      }
    });
  }

  removeStop(stop: DeliveryStopDto): void {
    const route = this.selectedRoute();
    if (!route) return;
    this.saveStops(route, toStopInputs(route.stops.filter((s) => s.id !== stop.id))).subscribe({
      next: () => {
        this.msg.success(`Stop ${stop.customerName} removed from route ${route.name}`);
        this.viewDetail(route);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The stop could not be removed'))
    });
  }

  /** The legal entity of an existing route cannot change. */
  private setEntityEnabled(enabled: boolean): void {
    setControlsEnabled(this.form, ['legalEntityId'], enabled);
  }

  /**
   * Saves, then reads the route back and fails if the change is not there, so a change the server did not keep is never
   * reported as saved.
   */
  private updateAndVerify(id: string, input: Partial<CreateRouteInput>): Observable<DistributionRouteDto> {
    return this.routesApi.update(id, input).pipe(
      switchMap(() => this.routesApi.getDetail(id)),
      map((saved) => {
        if (!routeKeptChange(saved, input)) throw new Error('The server did not keep the change.');
        return saved;
      })
    );
  }

  private saveStops(route: DistributionRouteDto, stops: DeliveryStopInput[]): Observable<DistributionRouteDto> {
    return this.updateAndVerify(route.id, routeWithStops(route, stops));
  }
}
