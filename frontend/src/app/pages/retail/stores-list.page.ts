import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { WarehousesApi, WarehouseRow } from '../../modules/inventory/inventory.api';
import { RetailStoreDto, RetailStoreInput, RetailStoresApi } from '../../core/api/retail.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-stores-list-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzSwitchModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    HasPermissionDirective,
    PagedTableComponent
  ],
  template: `
    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns">
      <button toolbar nz-button nzType="primary" *erpHasPermission="'retail.stores.manage'" (click)="openCreateModal()">
        <span nz-icon nzType="plus"></span> Add Store
      </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'code'" class="mono-code font-bold">{{ row.code }}</span>
              <span *ngSwitchCase="'name'">
                <div class="store-name-cell">
                  <strong>{{ row.name }}</strong>
                  <small *ngIf="row.storeManagerName" class="text-secondary">Mgr: {{ row.storeManagerName }}</small>
                </div>
              </span>
              <span *ngSwitchCase="'phone'">
                <span>{{ row.phone || '-' }}</span>
              </span>
              <span *ngSwitchCase="'email'">
                <span>{{ row.email || '-' }}</span>
              </span>
              <span *ngSwitchCase="'isActive'">
                <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
                  {{ row.isActive ? 'Active' : 'Inactive' }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" *erpHasPermission="'retail.stores.manage'" (click)="openEditModal(row)">
                  <span nz-icon nzType="edit"></span> Edit
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Store Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        [nzTitle]="isEditing() ? 'Edit Retail Store' : 'Add New Retail Store'"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="saveStore()">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Store Code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. STR-DXB-01" [readonly]="isEditing()" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Store Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Dubai Mall Flagship" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Workspace / Entity</nz-form-label>
                <nz-form-control nzErrorTip="Entity is required">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Fulfillment Warehouse</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="warehouseId" nzAllowClear nzPlaceHolder="Select Warehouse">
                    <nz-option *ngFor="let wh of warehouses()" [nzValue]="wh.id" [nzLabel]="wh.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label>Store Manager</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="storeManagerName" placeholder="e.g. Ahmed Al-Rashid" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Phone</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="phone" placeholder="+971 4 123 4567" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Email</nz-form-label>
              <nz-form-control>
                <input nz-input type="email" formControlName="email" placeholder="store.dubai@example.com" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Address</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="address" placeholder="Ground Level, Unit 120..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-control>
                <label>
                  <nz-switch formControlName="isActive"></nz-switch>
                  <span style="margin-left: 8px;">Active Store Status</span>
                </label>
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
    .store-name-cell {
      display: flex;
      flex-direction: column;
    }
    .row-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
  `]
})
export class StoresListPageComponent implements OnInit {
  private readonly storesApi = inject(RetailStoresApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly isSaving = signal(false);
  readonly isEditing = signal(false);

  isModalVisible = false;
  selectedStoreId: string | null = null;

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    warehouseId: [null],
    storeManagerName: [''],
    phone: [''],
    email: [''],
    address: [''],
    isActive: [true]
  });

  readonly columns: TableColumn<RetailStoreDto>[] = [
    { key: 'code', label: 'Store Code', sortField: 'code' },
    { key: 'name', label: 'Store Name', sortField: 'name' },
    { key: 'phone', label: 'Phone' },
    { key: 'email', label: 'Email' },
    { key: 'isActive', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  readonly list = createPagedList<RetailStoreDto>((q: any) => this.storesApi.list(q));

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.warehouses.set(res.items)
    });
  }

  openCreateModal(): void {
    this.isEditing.set(false);
    this.selectedStoreId = null;
    this.form.reset({
      legalEntityId: this.entities()[0]?.id || '',
      code: '',
      name: '',
      warehouseId: null,
      storeManagerName: '',
      phone: '',
      email: '',
      address: '',
      isActive: true
    });
    this.isModalVisible = true;
  }

  openEditModal(store: RetailStoreDto): void {
    this.isEditing.set(true);
    this.selectedStoreId = store.id;
    this.form.patchValue({
      legalEntityId: store.legalEntityId,
      code: store.code,
      name: store.name,
      warehouseId: store.warehouseId,
      storeManagerName: store.storeManagerName,
      phone: store.phone,
      email: store.email,
      address: store.address,
      isActive: store.isActive
    });
    this.isModalVisible = true;
  }

  closeModal(): void {
    this.isModalVisible = false;
  }

  saveStore(): void {
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

    if (this.isEditing() && this.selectedStoreId) {
      this.storesApi.update(this.selectedStoreId, {
        name: val.name,
        warehouseId: val.warehouseId,
        storeManagerName: val.storeManagerName,
        phone: val.phone,
        email: val.email,
        address: val.address,
        isActive: val.isActive
      }).subscribe({
        next: () => {
          this.msg.success('Store updated successfully');
          this.isSaving.set(false);
          this.closeModal();
          window.location.reload();
        },
        error: () => this.isSaving.set(false)
      });
    } else {
      const input: RetailStoreInput = {
        legalEntityId: val.legalEntityId,
        code: val.code,
        name: val.name,
        warehouseId: val.warehouseId,
        storeManagerName: val.storeManagerName,
        phone: val.phone,
        email: val.email,
        address: val.address,
        isActive: val.isActive
      };

      this.storesApi.create(input).subscribe({
        next: () => {
          this.msg.success('Store created successfully');
          this.isSaving.set(false);
          this.closeModal();
          window.location.reload();
        },
        error: () => this.isSaving.set(false)
      });
    }
  }
}
