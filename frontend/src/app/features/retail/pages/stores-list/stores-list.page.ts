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
import { createPagedList } from '@core/api/paged-list';
import { RetailStoreDto, RetailStoreInput, RetailStoresApi } from '@core/api/retail.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { defaultEntityId, revealInvalid, StoreFormValue } from '../../retail-forms';
import { RetailLookups } from '../../retail-lookups';

/** The fields of a store that can change after it is created (legal entity and code are fixed). */
function storeChanges(val: StoreFormValue, customFields: string | null | undefined): Partial<RetailStoreInput> {
  return {
    name: val.name,
    warehouseId: val.warehouseId,
    storeManagerName: val.storeManagerName,
    phone: val.phone,
    email: val.email,
    address: val.address,
    isActive: val.isActive,
    customFields,
  };
}

/** A new store from the form. */
function newStore(val: StoreFormValue): RetailStoreInput {
  return {
    legalEntityId: val.legalEntityId,
    code: val.code,
    name: val.name,
    warehouseId: val.warehouseId,
    storeManagerName: val.storeManagerName,
    phone: val.phone,
    email: val.email,
    address: val.address,
    isActive: val.isActive,
  };
}

/** Retail stores: the paged list and the create/edit modal (warehouse, manager and contact details). */
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
  providers: [RetailLookups],
  templateUrl: './stores-list.page.html',
  styleUrl: './stores-list.page.scss'
})
export class StoresListPageComponent implements OnInit {
  private readonly storesApi = inject(RetailStoresApi);
  private readonly lookups = inject(RetailLookups);
  private readonly session = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = this.lookups.entities;
  readonly warehouses = this.lookups.warehouses;
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
    { key: 'warehouse', label: 'Warehouse', value: (r) => this.lookups.warehouseName(r.warehouseId) },
    { key: 'phone', label: 'Phone' },
    { key: 'email', label: 'Email' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  readonly list = createPagedList<RetailStoreDto>((q) => this.storesApi.list(q), { sort: '-createdAt' });

  ngOnInit(): void {
    this.lookups.loadEntities();
    this.lookups.loadWarehouses();
  }

  openCreateModal(): void {
    this.isEditing.set(false);
    this.selectedStore = null;
    this.form.get('legalEntityId')!.enable({ emitEvent: false });
    this.form.reset({
      legalEntityId: defaultEntityId(this.entities(), this.session.currentEntityId()),
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
      revealInvalid(this.form);
      return;
    }

    this.isSaving.set(true);
    const val = this.form.getRawValue() as StoreFormValue;
    const editing = this.isEditing() ? this.selectedStore : null;
    const request$ = editing ? this.storesApi.update(editing.id, storeChanges(val, editing.customFields)) : this.storesApi.create(newStore(val));

    request$.subscribe({
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
