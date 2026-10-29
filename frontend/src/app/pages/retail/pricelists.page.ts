import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
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
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CreatePriceListInput,
  PriceListType,
  RetailPriceListDetailDto,
  RetailPriceListsApi,
  RetailPriceListSummaryDto
} from '../../core/api/retail.api';
import { createPagedList } from '../../core/api/paged-list';
import { SessionStore } from '../../core/session/session.store';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-pricelists-page',
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
    NzDatePickerModule,
    NzSelectModule,
    NzTableModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns">
      <button toolbar *erpHasPermission="'retail.pricelists.manage'" nz-button nzType="primary" (click)="openCreateModal()">
        <span nz-icon nzType="plus"></span> Create Price List
      </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'code'" class="mono-code font-bold">{{ row.code }}</span>
              <span *ngSwitchCase="'name'">
                <a (click)="viewDetail(row)">{{ row.name }}</a>
              </span>
              <span *ngSwitchCase="'type'">
                <nz-tag [nzColor]="getTypeColor(row.type)">{{ row.type }}</nz-tag>
              </span>
              <span *ngSwitchCase="'currency'">
                <strong>{{ row.currency }}</strong>
              </span>
              <span *ngSwitchCase="'itemCount'">
                <nz-badge [nzCount]="row.itemCount" [nzStyle]="{ backgroundColor: '#52c41a' }"></nz-badge> items
              </span>
              <span *ngSwitchCase="'isActive'">
                <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
                  {{ row.isActive ? 'Active' : 'Archived' }}
                </nz-tag>
                <nz-tag *ngIf="row.isDefault" nzColor="purple">Default</nz-tag>
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row)">
                  <span nz-icon nzType="eye"></span> View Items
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Price List Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        nzTitle="New Retail Price List"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="savePriceList()"
        nzWidth="720px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Price List Code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. PL-RETAIL-2026" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Price List Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Standard Store Retail AED" />
                </nz-form-control>
              </nz-form-item>
            </div>

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
                <nz-form-label nzRequired>Pricing Tier</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option nzValue="Standard" nzLabel="Standard Store"></nz-option>
                    <nz-option nzValue="Wholesale" nzLabel="Wholesale B2B"></nz-option>
                    <nz-option nzValue="Member" nzLabel="Loyalty Member"></nz-option>
                    <nz-option nzValue="Seasonal" nzLabel="Seasonal Special"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Currency</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="currency">
                    <nz-option nzValue="AED" nzLabel="AED"></nz-option>
                    <nz-option nzValue="SAR" nzLabel="SAR"></nz-option>
                    <nz-option nzValue="USD" nzLabel="USD"></nz-option>
                    <nz-option nzValue="EUR" nzLabel="EUR"></nz-option>
                    <nz-option nzValue="GBP" nzLabel="GBP"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label>Valid From</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="validFrom" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Valid To</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="validTo" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2 switches-row">
              <nz-form-item>
                <nz-form-control>
                  <label>
                    <nz-switch formControlName="isDefault"></nz-switch>
                    <span style="margin-left: 8px;">Default Price List for New Stores</span>
                  </label>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-control>
                  <label>
                    <nz-switch formControlName="isActive"></nz-switch>
                    <span style="margin-left: 8px;">Active</span>
                  </label>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Notes & Guidelines</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="notes" placeholder="Applies across all GCC outlets..."></textarea>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- View Detail Drawer -->
      <nz-drawer
        [nzVisible]="isDrawerVisible"
        nzTitle="Price List Detail & Items"
        [nzWidth]="640"
        (nzOnClose)="isDrawerVisible = false">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedDetail() as pl" class="drawer-content">
            <div class="detail-banner">
              <h3>{{ pl.name }}</h3>
              <div class="banner-badges">
                <nz-tag [nzColor]="getTypeColor(pl.type)">{{ pl.type }}</nz-tag>
                <nz-tag nzColor="blue">{{ pl.currency }}</nz-tag>
                <nz-tag *ngIf="pl.isDefault" nzColor="purple">Default</nz-tag>
              </div>
            </div>

            <p *ngIf="pl.notes" class="text-secondary">{{ pl.notes }}</p>

            <div class="table-section">
              <h4>Item Pricing Rates ({{ pl.items.length }} Items)</h4>
              <nz-table #itemsTable [nzData]="pl.items" nzSize="small" [nzBordered]="true">
                <thead>
                  <tr>
                    <th>Item / SKU</th>
                    <th nzAlign="right">Standard Price</th>
                    <th nzAlign="right">Min Qty</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let itm of itemsTable.data">
                    <td>
                      <div><strong>{{ itm.itemName || itm.itemId }}</strong></div>
                      <small class="mono-code">{{ itm.itemCode }}</small>
                    </td>
                    <td nzAlign="right" class="font-bold">{{ pl.currency }} {{ itm.price | number:'1.2-2' }}</td>
                    <td nzAlign="right">{{ itm.minQuantity || 1 }}</td>
                  </tr>
                  <tr *ngIf="pl.items.length === 0">
                    <td colspan="3" class="text-center text-secondary py-4">No specific item rates defined. Standard catalog prices apply.</td>
                  </tr>
                </tbody>
              </nz-table>
            </div>
          </div>
        </ng-container>
      </nz-drawer>
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
    .switches-row {
      margin-bottom: 8px;
    }
    .drawer-content {
      display: flex;
      flex-direction: column;
      gap: 16px;
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
    .banner-badges {
      display: flex;
      gap: 8px;
    }
    .table-section {
      margin-top: 16px;
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
export class PriceListsPageComponent implements OnInit {
  private readonly priceListsApi = inject(RetailPriceListsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly currency = computed(() => this.sessionStore.me()?.workspace?.baseCurrency || 'AED');
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly itemsList = signal<{ id: string; name: string; sku: string; salesPrice?: number }[]>([]);
  readonly isSaving = signal(false);
  readonly selectedDetail = signal<RetailPriceListDetailDto | null>(null);

  isModalVisible = false;
  isDrawerVisible = false;

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    type: ['Standard', [Validators.required]],
    currency: [this.currency(), [Validators.required]],
    validFrom: [null],
    validTo: [null],
    isDefault: [false],
    isActive: [true],
    notes: ['']
  });

  readonly columns: TableColumn<RetailPriceListSummaryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Price List Name', sortField: 'name' },
    { key: 'type', label: 'Type' },
    { key: 'currency', label: 'Currency' },
    { key: 'itemCount', label: 'Items' },
    { key: 'isActive', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  readonly list = createPagedList<RetailPriceListSummaryDto>((q: any) => this.priceListsApi.list(q));

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.itemsList.set(res.items)
    });
  }

  getTypeColor(type: PriceListType): string {
    switch (type) {
      case 'Standard': return 'blue';
      case 'Wholesale': return 'green';
      case 'Member': return 'gold';
      case 'Seasonal': return 'magenta';
      default: return 'default';
    }
  }

  openCreateModal(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id || '',
      code: '',
      name: '',
      type: 'Standard',
      currency: 'AED',
      validFrom: null,
      validTo: null,
      isDefault: false,
      isActive: true,
      notes: ''
    });
    this.isModalVisible = true;
  }

  closeModal(): void {
    this.isModalVisible = false;
  }

  viewDetail(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.getDetail(row.id).subscribe({
      next: detail => {
        this.selectedDetail.set(detail);
        this.isDrawerVisible = true;
      },
      error: () => this.msg.error('Failed to load price list details')
    });
  }

  savePriceList(): void {
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
    const input: CreatePriceListInput = {
      legalEntityId: val.legalEntityId,
      code: val.code,
      name: val.name,
      type: val.type,
      currency: val.currency,
      validFrom: val.validFrom ? new Date(val.validFrom).toISOString().split('T')[0] : null,
      validTo: val.validTo ? new Date(val.validTo).toISOString().split('T')[0] : null,
      isDefault: val.isDefault,
      isActive: val.isActive,
      notes: val.notes,
      items: []
    };

    this.priceListsApi.create(input).subscribe({
      next: () => {
        this.msg.success('Price list created successfully');
        this.isSaving.set(false);
        this.closeModal();
        window.location.reload();
      },
      error: () => this.isSaving.set(false)
    });
  }
}
