import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { createPagedList } from '../../core/api/paged-list';
import {
  CreateWholesalePriceInput,
  DistributionWholesaleApi,
  WholesalePriceMatrixDto,
  WholesaleRateCalculationDto,
  WholesaleTier
} from '../../core/api/distribution.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ErpLabelPipe } from '../../shared/display.pipes';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { problemMessage } from '../../core/http/problem-details';

@Component({
  selector: 'erp-wholesale-pricing-page',
  standalone: true,
  imports: [ErpLabelPipe, 
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzSwitchModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzAlertModule,
    NzDividerModule,
    NzPopconfirmModule,
    StatusTagComponent,
    HasPermissionDirective,
    PagedTableComponent
  ],
  template: `
    <!-- The server allows no search or sorting on price rules yet, so neither is offered. -->
    <erp-paged-table [list]="list" [columns]="columns" [searchable]="false" [clickable]="false" emptyText="No wholesale price rules yet. Add one to give a tier a price by quantity.">
      <div toolbar style="display: flex; align-items: center; gap: 8px;">
        <button nz-button nzType="default" (click)="openSimulatorModal()">
          <span nz-icon nzType="calculator"></span> Calculate Wholesale Rate
        </button>
        <button nz-button nzType="primary" *erpHasPermission="'distribution.pricing.manage'" (click)="openCreateModal()" data-testid="wholesale-new">
          <span nz-icon nzType="plus"></span> Add Pricing Tier
        </button>
      </div>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('itemCode') { <span class="mono-code font-bold">{{ row.itemCode }}</span> }
          @case ('tier') { <nz-tag [nzColor]="getTierColor(row.tier)">{{ row.tier | erpLabel }}</nz-tag> }
          @case ('bracket') { <span class="num">{{ bracket(row) }}</span> }
          @case ('discountPercentage') { <span class="num">{{ row.discountPercentage | number:'1.0-2' }}%</span> }
          @case ('wholesaleUnitPrice') { <span class="num font-bold">{{ currency() }} {{ row.wholesaleUnitPrice | number:'1.2-2' }}</span> }
          @case ('isActive') { <erp-status [value]="row.isActive ? 'active' : 'inactive'" /> }
          @case ('actions') {
            <div class="table-actions">
              <button nz-button nzType="link" nzSize="small" (click)="testRate(row)">
                <span nz-icon nzType="calculator"></span> Quote
              </button>
              <button *erpHasPermission="'distribution.pricing.manage'" nz-button nzType="link" nzSize="small" (click)="openEditModal(row)">
                <span nz-icon nzType="edit"></span> Edit
              </button>
              <button *erpHasPermission="'distribution.pricing.manage'" nz-button nzType="link" nzDanger nzSize="small"
                      nz-popconfirm nzPopconfirmTitle="Delete this price rule?" (nzOnConfirm)="deleteRule(row)">
                <span nz-icon nzType="delete"></span> Delete
              </button>
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

      <!-- Add Pricing Tier Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        [nzTitle]="editingId() ? 'Edit wholesale price rule' : 'New wholesale price rule'"
        nzOkText="Save"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="isModalVisible.set(false)"
        (nzOnOk)="saveRule()"
        nzWidth="680px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
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
                <nz-form-label nzRequired>Wholesale tier</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="tier">
                    <nz-option nzValue="standard" nzLabel="Standard"></nz-option>
                    <nz-option nzValue="bronze" nzLabel="Bronze"></nz-option>
                    <nz-option nzValue="silver" nzLabel="Silver"></nz-option>
                    <nz-option nzValue="gold" nzLabel="Gold"></nz-option>
                    <nz-option nzValue="platinum" nzLabel="Platinum"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Item</nz-form-label>
              <nz-form-control nzErrorTip="Item is required">
                <nz-select formControlName="itemId" nzShowSearch (ngModelChange)="onItemChosen($event)" nzPlaceHolder="Choose an item">
                  <nz-option *ngFor="let itm of catalogItems()" [nzValue]="itm.id" [nzLabel]="itm.name + ' (' + itm.sku + ')'"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>From quantity</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="minQuantity" [nzMin]="1" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>To quantity</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="maxQuantity" [nzMin]="1" style="width: 100%" nzPlaceHolder="No limit"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Unit price ({{ currency() }})</nz-form-label>
                <nz-form-control nzErrorTip="Price is required">
                  <nz-input-number formControlName="wholesaleUnitPrice" [nzMin]="0.01" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Discount %</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="discountPercentage" [nzMin]="0" [nzMax]="100" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="notes" placeholder="Packaging, contract terms"></textarea>
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

      <!-- Calculator Simulator Modal -->
      <nz-modal
        [(nzVisible)]="isSimulatorVisible"
        nzTitle="Calculate wholesale rate"
        [nzFooter]="null"
        (nzOnCancel)="isSimulatorVisible.set(false)">
        <ng-container *nzModalContent>
          <div class="sim-container">
            <p class="text-secondary">See which price a tier gets for a quantity. Nothing is saved.</p>

            <div class="sim-inputs">
              <div class="form-row">
                <label for="sim-item">Item</label>
                <nz-select nzId="sim-item" [(ngModel)]="simItemId" style="width: 100%" nzShowSearch nzPlaceHolder="Choose an item">
                  <nz-option *ngFor="let itm of catalogItems()" [nzValue]="itm.id" [nzLabel]="itm.name + ' (' + itm.sku + ')'"></nz-option>
                </nz-select>
              </div>

              <div class="row-2">
                <div class="form-row">
                  <label for="sim-tier">Wholesale tier</label>
                  <nz-select nzId="sim-tier" [(ngModel)]="simTier" style="width: 100%">
                    <nz-option nzValue="standard" nzLabel="Standard"></nz-option>
                    <nz-option nzValue="bronze" nzLabel="Bronze"></nz-option>
                    <nz-option nzValue="silver" nzLabel="Silver"></nz-option>
                    <nz-option nzValue="gold" nzLabel="Gold"></nz-option>
                    <nz-option nzValue="platinum" nzLabel="Platinum"></nz-option>
                  </nz-select>
                </div>
                <div class="form-row">
                  <label for="sim-qty">Quantity</label>
                  <nz-input-number nzId="sim-qty" [(ngModel)]="simQuantity" [nzMin]="1" style="width: 100%"></nz-input-number>
                </div>
              </div>

              <button nz-button nzType="primary" [nzLoading]="isCalculating()" (click)="calculateRate()" [disabled]="!simItemId">
                <span nz-icon nzType="calculator"></span> Calculate
              </button>
            </div>

            <nz-divider *ngIf="simResult()"></nz-divider>

            <div *ngIf="simResult() as res" class="sim-result">
              <nz-alert
                [nzType]="res.tierApplied ? 'success' : 'warning'"
                [nzMessage]="res.tierApplied ? 'A price rule applies' : 'No price rule applies'"
                [nzDescription]="res.tierApplied ? null : 'No active rule matches this item, tier and quantity.'"
                nzShowIcon>
              </nz-alert>

              <div *ngIf="res.tierApplied" class="discount-breakdown">
                <div class="breakdown-row">
                  <span>Unit price</span>
                  <span class="font-bold">{{ currency() }} {{ res.unitPrice | number:'1.2-2' }}</span>
                </div>
                <div class="breakdown-row">
                  <span>Quantity</span>
                  <span>{{ res.quantity | number:'1.0-4' }}</span>
                </div>
                <nz-divider style="margin: 8px 0"></nz-divider>
                <div class="breakdown-row total-row font-bold">
                  <span>Total</span>
                  <span>{{ currency() }} {{ res.extendedTotal | number:'1.2-2' }}</span>
                </div>
              </div>
            </div>
          </div>
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
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .row-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    .sim-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .sim-inputs {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .form-row {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .form-row label {
      font-size: 13px;
      font-weight: 500;
    }
    .discount-breakdown {
      margin-top: 16px;
      padding: 12px 16px;
      background: var(--erp-color-bg-subtle);
      border-radius: 6px;
    }
    .breakdown-row {
      display: flex;
      justify-content: space-between;
      margin-bottom: 6px;
      font-size: 14px;
    }
    .total-row {
      font-size: 16px;
    }
  `]
})
export class WholesalePricingPageComponent implements OnInit {
  private readonly wholesaleApi = inject(DistributionWholesaleApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly session = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly currency = this.currencyService.currency;

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly catalogItems = signal<{ id: string; name: string; sku: string; salesPrice?: number }[]>([]);
  readonly isSaving = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly isCalculating = signal(false);
  readonly simResult = signal<WholesaleRateCalculationDto | null>(null);

  readonly isModalVisible = signal(false);
  readonly isSimulatorVisible = signal(false);

  simItemId: string | null = null;
  private simEntityId: string | null = null;
  simTier: WholesaleTier = 'standard';
  simQuantity = 1;

  readonly list = createPagedList<WholesalePriceMatrixDto>((q) => this.wholesaleApi.list(q), { sort: '-createdAt' });

  readonly columns: TableColumn<WholesalePriceMatrixDto>[] = [
    { key: 'itemCode', label: 'Item code' },
    { key: 'itemName', label: 'Item' },
    { key: 'tier', label: 'Tier' },
    { key: 'bracket', label: 'Quantity', filter: false, value: (r) => this.bracket(r) },
    { key: 'discountPercentage', label: 'Discount', align: 'right' },
    { key: 'wholesaleUnitPrice', label: 'Unit price', align: 'right' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    itemId: ['', [Validators.required]],
    itemCode: [''],
    itemName: [''],
    tier: ['standard', [Validators.required]],
    minQuantity: [1, [Validators.required, Validators.min(0.0001)]],
    maxQuantity: [null],
    discountPercentage: [0],
    wholesaleUnitPrice: [null, [Validators.required, Validators.min(0.01)]],
    isActive: [true],
    notes: ['']
  });

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.catalogItems.set(res.items.filter(i => i.isActive))
    });
  }

  getTierColor(tier: WholesaleTier): string {
    switch (tier) {
      case 'bronze': return 'orange';
      case 'silver': return 'default';
      case 'gold': return 'gold';
      case 'platinum': return 'purple';
      default: return 'blue';
    }
  }

  onItemChosen(itemId: string): void {
    const itm = this.catalogItems().find(i => i.id === itemId);
    if (itm) {
      this.form.patchValue({
        itemCode: itm.sku,
        itemName: itm.name,
        // A starting point from the catalog price; the person can overtype it.
        wholesaleUnitPrice: itm.salesPrice ? this.roundMoney(itm.salesPrice * (1 - (this.form.value.discountPercentage ?? 0) / 100)) : null
      });
    }
  }

  private roundMoney(value: number): number {
    return Math.round(value * 100) / 100;
  }

  bracket(row: WholesalePriceMatrixDto): string {
    return row.maxQuantity ? `${row.minQuantity} to ${row.maxQuantity}` : `${row.minQuantity} and up`;
  }

  private setKeyFieldsEnabled(enabled: boolean): void {
    for (const key of ['legalEntityId', 'itemId']) {
      const control = this.form.get(key)!;
      if (enabled) control.enable({ emitEvent: false });
      else control.disable({ emitEvent: false });
    }
  }

  openCreateModal(): void {
    this.editingId.set(null);
    this.setKeyFieldsEnabled(true);
    this.form.reset({
      legalEntityId: this.entities().find(e => e.id === this.session.currentEntityId())?.id || this.entities()[0]?.id || '',
      itemId: '',
      itemCode: '',
      itemName: '',
      tier: 'standard',
      minQuantity: 1,
      maxQuantity: null,
      discountPercentage: 0,
      wholesaleUnitPrice: null,
      isActive: true,
      notes: ''
    });
    this.isModalVisible.set(true);
  }

  /** A rule can point at an item outside the loaded catalog page; it still has to show in the pickers. */
  private ensureCatalogItem(row: WholesalePriceMatrixDto): void {
    if (this.catalogItems().some(i => i.id === row.itemId)) return;
    this.catalogItems.update(items => [...items, { id: row.itemId, name: row.itemName, sku: row.itemCode }]);
  }

  openEditModal(row: WholesalePriceMatrixDto): void {
    this.ensureCatalogItem(row);
    this.editingId.set(row.id);
    this.form.reset({
      legalEntityId: row.legalEntityId,
      itemId: row.itemId,
      itemCode: row.itemCode,
      itemName: row.itemName,
      tier: row.tier,
      minQuantity: row.minQuantity,
      maxQuantity: row.maxQuantity ?? null,
      discountPercentage: row.discountPercentage,
      wholesaleUnitPrice: row.wholesaleUnitPrice,
      isActive: row.isActive,
      notes: row.notes ?? ''
    });
    this.setKeyFieldsEnabled(false);
    this.isModalVisible.set(true);
  }

  deleteRule(row: WholesalePriceMatrixDto): void {
    this.wholesaleApi.delete(row.id).subscribe({
      next: () => {
        this.msg.success('Price rule deleted');
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price rule could not be deleted'))
    });
  }

  openSimulatorModal(): void {
    this.simItemId = this.catalogItems()[0]?.id || null;
    this.simEntityId = null;
    this.simTier = 'standard';
    this.simQuantity = 1;
    this.simResult.set(null);
    this.isSimulatorVisible.set(true);
  }

  /** Opens the calculator on this rule's own item, tier and quantity. */
  testRate(row: WholesalePriceMatrixDto): void {
    this.ensureCatalogItem(row);
    this.simItemId = row.itemId;
    this.simEntityId = row.legalEntityId;
    this.simTier = row.tier;
    this.simQuantity = row.minQuantity;
    this.simResult.set(null);
    this.isSimulatorVisible.set(true);
    this.calculateRate();
  }

  calculateRate(): void {
    if (!this.simItemId) return;
    const entityId = this.simEntityId || this.session.currentEntityId() || this.entities()[0]?.id;
    if (!entityId) return;

    this.isCalculating.set(true);
    this.wholesaleApi.calculateRate(entityId, this.simItemId, this.simTier, this.simQuantity).subscribe({
      next: res => {
        this.simResult.set(res);
        this.isCalculating.set(false);
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The rate could not be calculated'));
        this.isCalculating.set(false);
      }
    });
  }

  saveRule(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach(c => { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); });
      return;
    }
    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const input: CreateWholesalePriceInput = {
      legalEntityId: val.legalEntityId,
      itemId: val.itemId,
      itemCode: val.itemCode,
      itemName: val.itemName,
      tier: val.tier,
      minQuantity: val.minQuantity,
      maxQuantity: val.maxQuantity,
      discountPercentage: val.discountPercentage ?? 0,
      wholesaleUnitPrice: val.wholesaleUnitPrice,
      isActive: val.isActive,
      notes: val.notes
    };

    const id = this.editingId();
    (id ? this.wholesaleApi.update(id, input) : this.wholesaleApi.create(input)).subscribe({
      next: () => {
        this.msg.success('Price rule saved');
        this.isSaving.set(false);
        this.isModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The price rule could not be saved'));
        this.isSaving.set(false);
      }
    });
  }
}
