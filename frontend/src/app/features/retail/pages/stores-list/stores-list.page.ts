import { doneText } from '@core/format/done-text';
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
import { LegalEntitiesApi } from '@core/api/admin.api';
import { WarehousesApi, WarehouseRow } from '@core/api/inventory.api';
import { RetailStoreDto, RetailStoreInput, RetailStoresApi } from '@core/api/retail.api';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';

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
  templateUrl: './stores-list.page.html',
  styleUrl: './stores-list.page.scss'
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
        this.msg.success(doneText('deleted', 'Store', store.name));
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
          this.msg.success(doneText('saved', 'Store', val.name));
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
          this.msg.success(doneText('saved', 'Store', val.name));
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
