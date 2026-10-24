import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { PartiesApi, PartyDto } from '../parties.page';
import { createPagedList } from '../../core/api/paged-list';
import {
  CreateRouteInput,
  DeliveryStopInput,
  DistributionRouteDto,
  DistributionRoutesApi,
  DistributionRouteSummaryDto
} from '../../core/api/distribution.api';
import { WarehousesApi } from '../../modules/inventory/inventory.api';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

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
    PagedTableComponent
  ],
  template: `
    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns">
      <button toolbar nz-button nzType="primary" (click)="openCreateModal()">
        <span nz-icon nzType="plus"></span> Add Route
      </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'code'" class="mono-code font-bold">{{ row.code }}</span>
              <span *ngSwitchCase="'name'">
                <a (click)="viewDetail(row)">{{ row.name }}</a>
              </span>
              <span *ngSwitchCase="'driverName'">
                <span>{{ row.driverName || 'Unassigned' }}</span>
              </span>
              <span *ngSwitchCase="'vehicleNumber'">
                <nz-tag nzColor="blue">{{ row.vehicleNumber || '-' }}</nz-tag>
              </span>
              <span *ngSwitchCase="'stopCount'">
                <nz-badge [nzCount]="row.stopCount" [nzStyle]="{ backgroundColor: '#1890ff' }"></nz-badge> stops
              </span>
              <span *ngSwitchCase="'isActive'">
                <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
                  {{ row.isActive ? 'Active' : 'Inactive' }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row)">
                  <span nz-icon nzType="environment"></span> View Stops
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Add Route Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        nzTitle="New Distribution Route"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="isModalVisible = false"
        (nzOnOk)="saveRoute()"
        nzWidth="680px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Route Code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. RT-DXB-CENTRAL" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Route Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Dubai Central Supermarkets" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Workspace Entity</nz-form-label>
                <nz-form-control nzErrorTip="Entity is required">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Dispatch Warehouse</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="warehouseId" nzAllowClear nzPlaceHolder="Select Warehouse">
                    <nz-option *ngFor="let wh of warehouses()" [nzValue]="wh.id" [nzLabel]="wh.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label>Assigned Driver</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="driverName" placeholder="e.g. Khalid Al-Zubair" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Vehicle Plate No.</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="vehicleNumber" placeholder="DXB-33921" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Max Capacity (Kg)</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="vehicleCapacityKg" [nzMin]="100" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Description & Route Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="description" placeholder="Covers Deira, Bur Dubai and surrounding areas..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-control>
                <label>
                  <nz-switch formControlName="isActive"></nz-switch>
                  <span style="margin-left: 8px;">Active Delivery Route</span>
                </label>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Route Stops Detail Drawer -->
      <nz-drawer
        [nzVisible]="isDrawerVisible"
        nzTitle="Route Delivery Stops & Customer Sequence"
        [nzWidth]="680"
        (nzOnClose)="isDrawerVisible = false">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedRoute() as r" class="drawer-content">
            <div class="route-banner">
              <div>
                <h3>{{ r.name }}</h3>
                <span class="mono-code">{{ r.code }}</span>
              </div>
              <div>
                <nz-tag nzColor="blue">Driver: {{ r.driverName || 'None' }}</nz-tag>
                <nz-tag nzColor="geekblue">Van: {{ r.vehicleNumber || '-' }}</nz-tag>
              </div>
            </div>

            <div class="stops-section">
              <div class="section-header">
                <h4>Customer Drop Stops ({{ r.stops.length }})</h4>
                <button nz-button nzType="dashed" nzSize="small" (click)="openAddStopModal()">
                  <span nz-icon nzType="plus"></span> Add Customer Drop
                </button>
              </div>

              <nz-table #stopsTable [nzData]="r.stops" nzSize="small" [nzBordered]="true">
                <thead>
                  <tr>
                    <th nzWidth="60px">Seq</th>
                    <th>Customer Drop / Store</th>
                    <th>Time Window</th>
                    <th>Contact</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let s of stopsTable.data">
                    <td class="font-bold text-center">#{{ s.sequenceOrder }}</td>
                    <td>
                      <div><strong>{{ s.customerName }}</strong></div>
                      <small *ngIf="s.address" class="text-secondary">{{ s.address }}</small>
                    </td>
                    <td><nz-tag nzColor="orange">{{ s.preferredTimeWindow || 'Anytime' }}</nz-tag></td>
                    <td><small>{{ s.contactPhone || '-' }}</small></td>
                  </tr>
                  <tr *ngIf="r.stops.length === 0">
                    <td colspan="4" class="text-center text-secondary py-4">No customer delivery stops configured for this route yet.</td>
                  </tr>
                </tbody>
              </nz-table>
            </div>
          </div>
        </ng-container>
      </nz-drawer>

      <!-- Add Customer Stop Modal -->
      <nz-modal
        [(nzVisible)]="isStopModalVisible"
        nzTitle="Add Customer Drop Stop"
        [nzOkLoading]="isSavingStop()"
        (nzOnCancel)="isStopModalVisible = false"
        (nzOnOk)="saveStop()">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="stopForm" nzLayout="vertical">
            <nz-form-item>
              <nz-form-label nzRequired>Select Customer</nz-form-label>
              <nz-form-control nzErrorTip="Customer is required">
                <nz-select formControlName="customerPartyId" (ngModelChange)="onCustomerSelected($event)" nzPlaceHolder="Choose Customer">
                  <nz-option *ngFor="let cust of customers()" [nzValue]="cust.id" [nzLabel]="cust.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Drop Sequence</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="sequenceOrder" [nzMin]="1" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Preferred Window</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="preferredTimeWindow" placeholder="e.g. 09:00 - 11:30" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Contact Phone</nz-form-label>
              <nz-form-control>
                <input nz-input formControlName="contactPhone" placeholder="+971 50 123 4567" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Drop-off Address</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="address" placeholder="Store loading dock, gate 2..."></textarea>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
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
    .drawer-content {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .route-banner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-bottom: 12px;
      border-bottom: 1px solid #f0f0f0;
    }
    .route-banner h3 {
      margin: 0;
    }
    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
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
export class RoutesListPageComponent implements OnInit {
  private readonly routesApi = inject(DistributionRoutesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly customers = signal<{ id: string; name: string }[]>([]);
  readonly selectedRoute = signal<DistributionRouteDto | null>(null);
  readonly isSaving = signal(false);
  readonly isSavingStop = signal(false);

  isModalVisible = false;
  isDrawerVisible = false;
  isStopModalVisible = false;

  readonly list = createPagedList<DistributionRouteSummaryDto>((q: any) => this.routesApi.list(q));

  readonly columns: TableColumn<DistributionRouteSummaryDto>[] = [
    { key: 'code', label: 'Route Code', sortField: 'code' },
    { key: 'name', label: 'Route Name', sortField: 'name' },
    { key: 'driverName', label: 'Driver' },
    { key: 'vehicleNumber', label: 'Vehicle Plate' },
    { key: 'stopCount', label: 'Stops' },
    { key: 'isActive', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    description: [''],
    assignedDriverUserId: [null],
    driverName: [''],
    vehicleNumber: [''],
    vehicleCapacityKg: [2500],
    warehouseId: [null],
    isActive: [true]
  });

  stopForm: FormGroup = this.fb.group({
    customerPartyId: ['', [Validators.required]],
    customerName: ['', [Validators.required]],
    sequenceOrder: [1, [Validators.required]],
    address: [''],
    contactPhone: [''],
    preferredTimeWindow: ['09:00 - 12:00']
  });

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.warehouses.set(res.items)
    });
    this.partiesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.customers.set(res.items.map(p => ({ id: p.id, name: p.name })))
    });
  }

  openCreateModal(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id || '',
      code: '',
      name: '',
      description: '',
      assignedDriverUserId: null,
      driverName: '',
      vehicleNumber: '',
      vehicleCapacityKg: 2500,
      warehouseId: null,
      isActive: true
    });
    this.isModalVisible = true;
  }

  viewDetail(row: DistributionRouteSummaryDto): void {
    this.routesApi.getDetail(row.id).subscribe({
      next: detail => {
        this.selectedRoute.set(detail);
        this.isDrawerVisible = true;
      },
      error: () => this.msg.error('Failed to load route details')
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
    const val = this.form.value;
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
      stops: []
    };

    this.routesApi.create(input).subscribe({
      next: () => {
        this.msg.success('Route created successfully');
        this.isSaving.set(false);
        this.isModalVisible = false;
        window.location.reload();
      },
      error: () => this.isSaving.set(false)
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
      preferredTimeWindow: '09:00 - 12:00'
    });
    this.isStopModalVisible = true;
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
    const existingStops: DeliveryStopInput[] = route.stops.map(s => ({
      customerPartyId: s.customerPartyId,
      customerName: s.customerName,
      sequenceOrder: s.sequenceOrder,
      address: s.address,
      contactPhone: s.contactPhone,
      preferredTimeWindow: s.preferredTimeWindow,
      specialInstructions: s.specialInstructions
    }));

    existingStops.push({
      customerPartyId: stopVal.customerPartyId,
      customerName: stopVal.customerName,
      sequenceOrder: stopVal.sequenceOrder,
      address: stopVal.address,
      contactPhone: stopVal.contactPhone,
      preferredTimeWindow: stopVal.preferredTimeWindow,
      specialInstructions: ''
    });

    this.routesApi.update(route.id, {
      name: route.name,
      stops: existingStops
    }).subscribe({
      next: () => {
        this.msg.success('Customer stop added');
        this.isSavingStop.set(false);
        this.isStopModalVisible = false;
        this.viewDetail({ ...route, stopCount: existingStops.length } as any);
      },
      error: () => this.isSavingStop.set(false)
    });
  }
}
