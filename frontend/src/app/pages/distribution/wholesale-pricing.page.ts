import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
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
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import {
  CreateWholesalePriceInput,
  DistributionWholesaleApi,
  WholesalePriceMatrixDto,
  WholesaleRateCalculationDto,
  WholesaleTier
} from '../../core/api/distribution.api';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-wholesale-pricing-page',
  standalone: true,
  imports: [
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
    PagedTableComponent
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Wholesale Pricing & Volume Tiers</h2>
          <p class="text-secondary">Configure wholesale price matrices, volume discounts (Bronze, Silver, Gold, Platinum) and calculate order rates</p>
        </div>
        <div class="header-actions">
          <button nz-button nzType="default" (click)="openSimulatorModal()">
            <span nz-icon nzType="calculator"></span> Calculate Wholesale Rate
          </button>
          <button nz-button nzType="primary" (click)="openCreateModal()">
            <span nz-icon nzType="plus"></span> Add Pricing Tier
          </button>
        </div>
      </div>

      <nz-card [nzBordered]="false">
        <erp-paged-table
          #table
          [list]="list"
          [columns]="columns">
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'itemCode'" class="mono-code font-bold">{{ row.itemCode }}</span>
              <span *ngSwitchCase="'itemName'">
                <strong>{{ row.itemName }}</strong>
              </span>
              <span *ngSwitchCase="'tier'">
                <nz-tag [nzColor]="getTierColor(row.tier)">{{ row.tier }}</nz-tag>
              </span>
              <span *ngSwitchCase="'bracket'">
                <span>{{ row.minQuantity }} &mdash; {{ row.maxQuantity ? row.maxQuantity : '&infin;' }} units</span>
              </span>
              <span *ngSwitchCase="'discountPercentage'">
                <nz-tag nzColor="green">{{ row.discountPercentage }}% Off</nz-tag>
              </span>
              <span *ngSwitchCase="'wholesaleUnitPrice'" class="font-bold">
                AED {{ row.wholesaleUnitPrice | number:'1.2-2' }}
              </span>
              <span *ngSwitchCase="'isActive'">
                <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
                  {{ row.isActive ? 'Active' : 'Inactive' }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" (click)="testRate(row)">
                  <span nz-icon nzType="thunderbolt"></span> Quote
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>
      </nz-card>

      <!-- Add Pricing Tier Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        nzTitle="New Wholesale Volume Pricing Rule"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="isModalVisible = false"
        (nzOnOk)="saveRule()"
        nzWidth="680px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
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
                <nz-form-label nzRequired>Wholesale Account Tier</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="tier">
                    <nz-option nzValue="Standard" nzLabel="Standard Wholesale"></nz-option>
                    <nz-option nzValue="Bronze" nzLabel="Bronze Tier (10+ units)"></nz-option>
                    <nz-option nzValue="Silver" nzLabel="Silver Tier (50+ units)"></nz-option>
                    <nz-option nzValue="Gold" nzLabel="Gold Tier (100+ units)"></nz-option>
                    <nz-option nzValue="Platinum" nzLabel="Platinum Tier (500+ units)"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Catalog Product / Item</nz-form-label>
              <nz-form-control nzErrorTip="Item is required">
                <nz-select formControlName="itemId" (ngModelChange)="onItemChosen($event)" nzPlaceHolder="Select Item">
                  <nz-option *ngFor="let itm of catalogItems()" [nzValue]="itm.id" [nzLabel]="itm.name + ' (' + itm.sku + ')'"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Min Order Quantity</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="minQuantity" [nzMin]="1" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Max Order Quantity</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="maxQuantity" [nzMin]="1" style="width: 100%" placeholder="No cap"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Wholesale Unit Price (AED)</nz-form-label>
                <nz-form-control nzErrorTip="Price is required">
                  <nz-input-number formControlName="wholesaleUnitPrice" [nzMin]="0.01" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Volume Discount %</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="discountPercentage" [nzMin]="0" [nzMax]="100" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Notes & Contract Terms</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="notes" placeholder="Bulk packaging requirements..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-control>
                <label>
                  <nz-switch formControlName="isActive"></nz-switch>
                  <span style="margin-left: 8px;">Active Pricing Rule</span>
                </label>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Calculator Simulator Modal -->
      <nz-modal
        [(nzVisible)]="isSimulatorVisible"
        nzTitle="Wholesale Volume Rate Calculator"
        [nzFooter]="null"
        (nzOnCancel)="isSimulatorVisible = false">
        <ng-container *nzModalContent>
          <div class="sim-container">
            <p class="text-secondary">Simulate net wholesale rates across different tiers and bulk order order quantities</p>

            <div class="sim-inputs">
              <div class="form-row">
                <label>Select Item:</label>
                <nz-select [(ngModel)]="simItemId" style="width: 100%" nzPlaceHolder="Choose product">
                  <nz-option *ngFor="let itm of catalogItems()" [nzValue]="itm.id" [nzLabel]="itm.name + ' (' + itm.sku + ')'"></nz-option>
                </nz-select>
              </div>

              <div class="row-2">
                <div class="form-row">
                  <label>Wholesale Tier:</label>
                  <nz-select [(ngModel)]="simTier" style="width: 100%">
                    <nz-option nzValue="Standard" nzLabel="Standard"></nz-option>
                    <nz-option nzValue="Bronze" nzLabel="Bronze"></nz-option>
                    <nz-option nzValue="Silver" nzLabel="Silver"></nz-option>
                    <nz-option nzValue="Gold" nzLabel="Gold"></nz-option>
                    <nz-option nzValue="Platinum" nzLabel="Platinum"></nz-option>
                  </nz-select>
                </div>
                <div class="form-row">
                  <label>Order Qty (Units):</label>
                  <nz-input-number [(ngModel)]="simQuantity" [nzMin]="1" style="width: 100%"></nz-input-number>
                </div>
              </div>

              <button nz-button nzType="primary" [nzLoading]="isCalculating()" (click)="calculateRate()" [disabled]="!simItemId">
                <span nz-icon nzType="calculator"></span> Calculate Rate
              </button>
            </div>

            <nz-divider *ngIf="simResult()"></nz-divider>

            <div *ngIf="simResult() as res" class="sim-result">
              <nz-alert
                [nzType]="res.tierApplied ? 'success' : 'warning'"
                [nzMessage]="res.tierApplied ? 'Tier Rule Applied' : 'No Specific Tier Rule Found'"
                [nzDescription]="res.tierApplied ? ('Applied tier price of AED ' + res.unitPrice + ' per unit.') : 'Standard catalog pricing applies without volume bracket discount.'"
                nzShowIcon>
              </nz-alert>

              <div *ngIf="res.tierApplied" class="discount-breakdown">
                <div class="breakdown-row">
                  <span>Unit Wholesale Price:</span>
                  <span class="font-bold">AED {{ res.unitPrice | number:'1.2-2' }}</span>
                </div>
                <div class="breakdown-row">
                  <span>Order Quantity:</span>
                  <span>{{ res.quantity }} units</span>
                </div>
                <nz-divider style="margin: 8px 0"></nz-divider>
                <div class="breakdown-row total-row font-bold">
                  <span>Total Wholesale Quotation:</span>
                  <span style="color: #1890ff">AED {{ res.extendedTotal | number:'1.2-2' }}</span>
                </div>
              </div>
            </div>
          </div>
        </ng-container>
      </nz-modal>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 24px;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .page-header h2 {
      margin: 0;
      font-size: 24px;
      font-weight: 600;
    }
    .header-actions {
      display: flex;
      gap: 12px;
    }
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
      background: #fafafa;
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
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly catalogItems = signal<{ id: string; name: string; sku: string; salesPrice?: number }[]>([]);
  readonly isSaving = signal(false);
  readonly isCalculating = signal(false);
  readonly simResult = signal<WholesaleRateCalculationDto | null>(null);

  isModalVisible = false;
  isSimulatorVisible = false;

  simItemId: string | null = null;
  simTier: WholesaleTier = 'Bronze';
  simQuantity = 100;

  readonly list = createPagedList<WholesalePriceMatrixDto>((q: any) => this.wholesaleApi.list(q));

  readonly columns: TableColumn<WholesalePriceMatrixDto>[] = [
    { key: 'itemCode', label: 'Item Code', sortField: 'itemCode' },
    { key: 'itemName', label: 'Item Description', sortField: 'itemName' },
    { key: 'tier', label: 'Wholesale Tier' },
    { key: 'bracket', label: 'Quantity Bracket' },
    { key: 'discountPercentage', label: 'Discount %' },
    { key: 'wholesaleUnitPrice', label: 'Wholesale Price', align: 'right' },
    { key: 'isActive', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    itemId: ['', [Validators.required]],
    itemCode: [''],
    itemName: [''],
    tier: ['Standard', [Validators.required]],
    minQuantity: [10, [Validators.required, Validators.min(1)]],
    maxQuantity: [null],
    discountPercentage: [10],
    wholesaleUnitPrice: [15, [Validators.required, Validators.min(0.01)]],
    isActive: [true],
    notes: ['']
  });

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.catalogItems.set(res.items)
    });
  }

  getTierColor(tier: WholesaleTier): string {
    switch (tier) {
      case 'Bronze': return 'orange';
      case 'Silver': return 'default';
      case 'Gold': return 'gold';
      case 'Platinum': return 'purple';
      default: return 'blue';
    }
  }

  onItemChosen(itemId: string): void {
    const itm = this.catalogItems().find(i => i.id === itemId);
    if (itm) {
      this.form.patchValue({
        itemCode: itm.sku,
        itemName: itm.name,
        wholesaleUnitPrice: itm.salesPrice ? (itm.salesPrice * 0.85) : 15
      });
    }
  }

  openCreateModal(): void {
    const firstItem = this.catalogItems()[0];
    this.form.reset({
      legalEntityId: this.entities()[0]?.id || '',
      itemId: firstItem?.id || '',
      itemCode: firstItem?.sku || '',
      itemName: firstItem?.name || '',
      tier: 'Bronze',
      minQuantity: 10,
      maxQuantity: 49,
      discountPercentage: 10,
      wholesaleUnitPrice: firstItem?.salesPrice ? (firstItem.salesPrice * 0.9) : 20,
      isActive: true,
      notes: ''
    });
    this.isModalVisible = true;
  }

  openSimulatorModal(): void {
    this.simItemId = this.catalogItems()[0]?.id || null;
    this.simTier = 'Bronze';
    this.simQuantity = 100;
    this.simResult.set(null);
    this.isSimulatorVisible = true;
  }

  testRate(row: WholesalePriceMatrixDto): void {
    this.simItemId = row.itemId;
    this.simTier = row.tier;
    this.simQuantity = row.minQuantity;
    this.openSimulatorModal();
    this.calculateRate();
  }

  calculateRate(): void {
    if (!this.simItemId) return;
    const entityId = this.entities()[0]?.id;
    if (!entityId) return;

    this.isCalculating.set(true);
    this.wholesaleApi.calculateRate(entityId, this.simItemId, this.simTier, this.simQuantity).subscribe({
      next: res => {
        this.simResult.set(res);
        this.isCalculating.set(false);
      },
      error: () => {
        this.msg.error('Failed to calculate wholesale rate');
        this.isCalculating.set(false);
      }
    });
  }

  saveRule(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.value;
    const input: CreateWholesalePriceInput = {
      legalEntityId: val.legalEntityId,
      itemId: val.itemId,
      itemCode: val.itemCode,
      itemName: val.itemName,
      tier: val.tier,
      minQuantity: val.minQuantity,
      maxQuantity: val.maxQuantity,
      discountPercentage: val.discountPercentage,
      wholesaleUnitPrice: val.wholesaleUnitPrice,
      isActive: val.isActive,
      notes: val.notes
    };

    this.wholesaleApi.create(input).subscribe({
      next: () => {
        this.msg.success('Wholesale pricing rule saved');
        this.isSaving.set(false);
        this.isModalVisible = false;
        window.location.reload();
      },
      error: () => this.isSaving.set(false)
    });
  }
}
