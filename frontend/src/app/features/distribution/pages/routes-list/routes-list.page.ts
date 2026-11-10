import { doneText } from '@core/format/done-text';
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { map, Observable, switchMap } from 'rxjs';
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
import { LegalEntitiesApi } from '@core/api/admin.api';
import { PartiesApi, PartyDto } from '@features/parties/pages/parties/parties.page';
import { createPagedList } from '@core/api/paged-list';
import {
  CreateRouteInput,
  DeliveryStopDto,
  DeliveryStopInput,
  DistributionRouteDto,
  DistributionRoutesApi,
  DistributionRouteSummaryDto
} from '@core/api/distribution.api';
import { WarehousesApi } from '@core/api/inventory.api';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';

@Component({
  selector: 'erp-routes-list-page',
  standalone: true,
  imports: [
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
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzTableModule,
    NzPopconfirmModule,
    NzToolTipModule,
    StatusTagComponent,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  templateUrl: './routes-list.page.html',
  styleUrl: './routes-list.page.scss'
})
export class RoutesListPageComponent implements OnInit {
  private readonly routesApi = inject(DistributionRoutesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly session = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly customers = signal<{ id: string; name: string }[]>([]);
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
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.warehouses.set(res.items)
    });
    this.partiesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => {
        const customers = res.items.filter(p => !p.roles?.length || p.roles.some(r => /customer/i.test(r)));
        this.customers.set((customers.length > 0 ? customers : res.items).map(p => ({ id: p.id, name: p.name })));
      }
    });
  }

  private setEntityEnabled(enabled: boolean): void {
    const control = this.form.get('legalEntityId')!;
    if (enabled) control.enable({ emitEvent: false });
    else control.disable({ emitEvent: false });
  }

  openEditModal(row: DistributionRouteSummaryDto): void {
    this.routesApi.getDetail(row.id).subscribe({
      next: detail => {
        this.editing.set(detail);
        this.form.reset({
          legalEntityId: detail.legalEntityId,
          code: detail.code,
          name: detail.name,
          description: detail.description ?? '',
          assignedDriverUserId: detail.assignedDriverUserId ?? null,
          driverName: detail.driverName ?? '',
          vehicleNumber: detail.vehicleNumber ?? '',
          vehicleCapacityKg: detail.vehicleCapacityKg ?? null,
          warehouseId: detail.warehouseId ?? null,
          isActive: detail.isActive
        });
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
    this.form.reset({
      legalEntityId: this.entities().find(e => e.id === this.session.currentEntityId())?.id || this.entities()[0]?.id || '',
      code: '',
      name: '',
      description: '',
      assignedDriverUserId: null,
      driverName: '',
      vehicleNumber: '',
      vehicleCapacityKg: null,
      warehouseId: null,
      isActive: true
    });
    this.isModalVisible.set(true);
  }

  viewDetail(row: DistributionRouteSummaryDto): void {
    this.routesApi.getDetail(row.id).subscribe({
      next: detail => {
        this.selectedRoute.set(detail);
        this.isDrawerVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The route could not be loaded'))
    });
  }

  saveRoute(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach(c => {
        if (c.invalid) {
          c.markAsDirty();
          c.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const editing = this.editing();
    const input: CreateRouteInput = {
      legalEntityId: val.legalEntityId,
      code: val.code,
      name: val.name,
      description: val.description,
      assignedDriverUserId: val.assignedDriverUserId,
      driverName: val.driverName,
      vehicleNumber: val.vehicleNumber,
      vehicleCapacityKg: val.vehicleCapacityKg,
      warehouseId: val.warehouseId,
      isActive: val.isActive,
      // Editing without stops leaves the stops as they are.
      stops: editing ? undefined : [],
      customFields: editing?.customFields ?? null
    };

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
    const cust = this.customers().find(c => c.id === customerId);
    if (cust) {
      this.stopForm.patchValue({ customerName: cust.name });
    }
  }

  saveStop(): void {
    if (this.stopForm.invalid) return;
    const route = this.selectedRoute();
    if (!route) return;

    this.isSavingStop.set(true);
    const stopVal = this.stopForm.value;
    const existingStops = this.stopInputs(route.stops);

    existingStops.push({
      customerPartyId: stopVal.customerPartyId,
      customerName: stopVal.customerName,
      sequenceOrder: stopVal.sequenceOrder,
      address: stopVal.address,
      contactPhone: stopVal.contactPhone,
      preferredTimeWindow: stopVal.preferredTimeWindow,
      specialInstructions: ''
    });

    this.saveStops(route, existingStops).subscribe({
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
    this.saveStops(route, this.stopInputs(route.stops.filter(s => s.id !== stop.id))).subscribe({
      next: () => {
        this.msg.success(`Stop ${stop.customerName} removed from route ${route.name}`);
        this.viewDetail(route);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The stop could not be removed'))
    });
  }

  /**
   * Saves, then reads the route back and fails if the change is not there, so a change the server did not keep is never
   * reported as saved.
   */
  private updateAndVerify(id: string, input: Partial<CreateRouteInput>): Observable<DistributionRouteDto> {
    const stopKey = (stops: { customerPartyId: string; sequenceOrder: number }[]) =>
      stops.map(s => `${s.sequenceOrder}:${s.customerPartyId}`).sort().join();
    return this.routesApi.update(id, input).pipe(
      switchMap(() => this.routesApi.getDetail(id)),
      map(saved => {
        const kept = saved.name === input.name?.trim()
          && (saved.driverName ?? '') === (input.driverName ?? '').trim()
          && (saved.vehicleNumber ?? '') === (input.vehicleNumber ?? '').trim()
          && saved.isActive === input.isActive
          && (!input.stops || stopKey(saved.stops) === stopKey(input.stops));
        if (!kept) throw new Error('The server did not keep the change.');
        return saved;
      })
    );
  }

  private stopInputs(stops: DeliveryStopDto[]): DeliveryStopInput[] {
    return stops.map(s => ({
      customerPartyId: s.customerPartyId,
      customerName: s.customerName,
      sequenceOrder: s.sequenceOrder,
      address: s.address,
      contactPhone: s.contactPhone,
      preferredTimeWindow: s.preferredTimeWindow,
      specialInstructions: s.specialInstructions
    }));
  }

  /** An update replaces the whole route, so every field goes back with the changed stops. */
  private saveStops(route: DistributionRouteDto, stops: DeliveryStopInput[]) {
    return this.updateAndVerify(route.id, {
      name: route.name,
      description: route.description,
      assignedDriverUserId: route.assignedDriverUserId,
      driverName: route.driverName,
      vehicleNumber: route.vehicleNumber,
      vehicleCapacityKg: route.vehicleCapacityKg,
      warehouseId: route.warehouseId,
      isActive: route.isActive,
      stops,
      customFields: route.customFields
    });
  }
}
