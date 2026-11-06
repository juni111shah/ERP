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
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { WarehousesApi, WarehouseRow } from '../../modules/inventory/inventory.api';
import { RetailStoreDto, RetailStoreInput, RetailStoresApi } from '../../core/api/retail.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { problemMessage } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';

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
    NzPopconfirmModule,
    StatusTagComponent,
    HasPermissionDirective,
    PagedTableComponent
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" [clickable]="false" searchPlaceholder="Search by code, name or manager" emptyText="No stores yet. Add your first store.">
      <button toolbar nz-button nzType="primary" *erpHasPermission="'retail.stores.manage'" (click)="openCreateModal()" data-testid="store-new">
        <span nz-icon nzType="plus"></span> Add Store
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') { <span class="mono-code font-bold">{{ row.code }}</span> }
          @case ('name') {
            <div>{{ row.name }}</div>
            @if (row.storeManagerName) { <div class="sub">Manager: {{ row.storeManagerName }}</div> }
          }
          @case ('phone') { <span class="nowrap">{{ row.phone || '-' }}</span> }
          @case ('email') { {{ row.email || '-' }} }
          @case ('warehouse') { {{ value || '-' }} }
          @case ('isActive') { <erp-status [value]="row.isActive ? 'active' : 'inactive'" /> }
          @case ('actions') {
            <div class="table-actions" *erpHasPermission="'retail.stores.manage'">
              <button nz-button nzType="link" nzSize="small" (click)="openEditModal(row)">
                <span nz-icon nzType="edit"></span> Edit
              </button>
              <button nz-button nzType="link" nzDanger nzSize="small" nz-popconfirm [nzPopconfirmTitle]="'Delete store ' + row.code + '?'" (nzOnConfirm)="deleteStore(row)">
                <span nz-icon nzType="delete"></span> Delete
              </button>
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

      <!-- Store Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        [nzTitle]="isEditing() ? 'Edit store' : 'Add store'"
        nzOkText="Save"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="saveStore()">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. STR-01" [readonly]="isEditing()" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. City centre store" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Legal entity</nz-form-label>
                <nz-form-control nzErrorTip="Entity is required">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Warehouse</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="warehouseId" nzAllowClear nzPlaceHolder="Select Warehouse">
                    <nz-option *ngFor="let wh of warehouses()" [nzValue]="wh.id" [nzLabel]="wh.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label>Manager</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="storeManagerName" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Phone</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="phone" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Email</nz-form-label>
              <nz-form-control nzErrorTip="Enter a valid email address">
                <input nz-input type="email" formControlName="email" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Address</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="address" placeholder="Floor, unit, street"></textarea>
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
          </form>
        </ng-container>
      </nz-modal>
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
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
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
  private readonly session = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly isSaving = signal(false);
  readonly isEditing = signal(false);

  readonly isModalVisible = signal(false);
  private selectedStore: RetailStoreDto | null = null;

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    warehouseId: [null],
    storeManagerName: [''],
    phone: [''],
    email: ['', [Validators.email]],
    address: [''],
    isActive: [true]
  });

  readonly columns: TableColumn<RetailStoreDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'warehouse', label: 'Warehouse', value: (r) => this.warehouses().find((w) => w.id === r.warehouseId)?.name ?? '' },
    { key: 'phone', label: 'Phone' },
    { key: 'email', label: 'Email' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  readonly list = createPagedList<RetailStoreDto>((q) => this.storesApi.list(q), { sort: '-createdAt' });

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
    this.selectedStore = null;
    this.form.get('legalEntityId')!.enable({ emitEvent: false });
    this.form.reset({
      legalEntityId: this.entities().find((e) => e.id === this.session.currentEntityId())?.id || this.entities()[0]?.id || '',
      code: '',
      name: '',
      warehouseId: null,
      storeManagerName: '',
      phone: '',
      email: '',
      address: '',
      isActive: true
    });
    this.isModalVisible.set(true);
  }

  openEditModal(store: RetailStoreDto): void {
    this.isEditing.set(true);
    this.selectedStore = store;
    this.form.get('legalEntityId')!.disable({ emitEvent: false });
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
    this.isModalVisible.set(true);
  }

  closeModal(): void {
    this.isModalVisible.set(false);
  }

  deleteStore(store: RetailStoreDto): void {
    this.storesApi.delete(store.id).subscribe({
      next: () => {
        this.msg.success('Store deleted');
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The store could not be deleted'))
    });
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
    const val = this.form.getRawValue();

    if (this.isEditing() && this.selectedStore) {
      this.storesApi.update(this.selectedStore.id, {
        name: val.name,
        warehouseId: val.warehouseId,
        storeManagerName: val.storeManagerName,
        phone: val.phone,
        email: val.email,
        address: val.address,
        isActive: val.isActive,
        customFields: this.selectedStore.customFields
      }).subscribe({
        next: () => {
          this.msg.success('Store saved');
          this.isSaving.set(false);
          this.closeModal();
          this.list.reload();
        },
        error: (err) => {
          this.msg.error(problemMessage(err, 'The store could not be saved'));
          this.isSaving.set(false);
        }
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
          this.msg.success('Store saved');
          this.isSaving.set(false);
          this.closeModal();
          this.list.reload();
        },
        error: (err) => {
          this.msg.error(problemMessage(err, 'The store could not be saved'));
          this.isSaving.set(false);
        }
      });
    }
  }
}
