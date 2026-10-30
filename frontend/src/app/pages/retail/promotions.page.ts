import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CreatePromotionInput,
  PromotionType,
  PromoValidationResultDto,
  RetailPromotionDto,
  RetailPromotionsApi
} from '../../core/api/retail.api';
import { createPagedList } from '../../core/api/paged-list';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-promotions-page',
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
    NzDatePickerModule,
    NzSelectModule,
    NzAlertModule,
    NzDividerModule,
    NzProgressModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns">
      <div toolbar style="display: flex; align-items: center; gap: 8px;">
        <button nz-button nzType="default" (click)="openSimulatorModal()">
          <span nz-icon nzType="thunderbolt"></span> Test Promo Code
        </button>
        <button *erpHasPermission="'retail.promotions.manage'" nz-button nzType="primary" (click)="openCreateModal()">
          <span nz-icon nzType="plus"></span> Create Promotion
        </button>
      </div>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'code'" class="mono-code promo-badge">{{ row.code }}</span>
              <span *ngSwitchCase="'name'">
                <div><strong>{{ row.name }}</strong></div>
                <small *ngIf="row.description" class="text-secondary">{{ row.description }}</small>
              </span>
              <span *ngSwitchCase="'type'">
                <nz-tag [nzColor]="row.type === 'PercentageDiscount' ? 'geekblue' : 'cyan'">
                  {{ row.type === 'PercentageDiscount' ? 'Percent %' : 'Flat Amount' }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'discountValue'" class="font-bold">
                {{ row.type === 'PercentageDiscount' ? (row.discountValue + '%') : (currency() + ' ' + (row.discountValue | number:'1.2-2')) }}
              </span>
              <span *ngSwitchCase="'validity'">
                <small class="text-secondary">{{ row.startDate }} &rarr; {{ row.endDate }}</small>
              </span>
              <span *ngSwitchCase="'usage'">
                <div class="usage-cell">
                  <span class="text-secondary">{{ row.timesUsed }} / {{ row.usageLimit || '&infin;' }}</span>
                  <nz-progress
                    *ngIf="row.usageLimit"
                    [nzPercent]="getUsagePercent(row.timesUsed, row.usageLimit)"
                    nzSize="small"
                    [nzShowInfo]="false">
                  </nz-progress>
                </div>
              </span>
              <span *ngSwitchCase="'isActive'">
                <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
                  {{ row.isActive ? 'Active' : 'Expired' }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" (click)="testCode(row.code)">
                  <span nz-icon nzType="experiment"></span> Test
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Promotion Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        nzTitle="Create Retail Promotion"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="savePromotion()"
        nzWidth="680px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Promo Code / Coupon</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. FLASH20" style="text-transform: uppercase;" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Campaign Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Ramadan Super Saver" />
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
                <nz-form-label nzRequired>Discount Type</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option nzValue="PercentageDiscount" nzLabel="Percentage (%)"></nz-option>
                    <nz-option nzValue="FixedAmountDiscount" [nzLabel]="'Fixed Amount (' + currency() + ')'"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Discount Value</nz-form-label>
                <nz-form-control nzErrorTip="Value must be greater than 0">
                  <nz-input-number formControlName="discountValue" [nzMin]="0.01" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label>Min Order ({{ currency() }})</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="minimumOrderAmount" [nzMin]="0" style="width: 100%" placeholder="No min"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Start Date</nz-form-label>
                <nz-form-control nzErrorTip="Start date is required">
                  <nz-date-picker formControlName="startDate" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>End Date</nz-form-label>
                <nz-form-control nzErrorTip="End date is required">
                  <nz-date-picker formControlName="endDate" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label>Usage Limit (Redemptions)</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="usageLimit" [nzMin]="1" style="width: 100%" placeholder="Unlimited"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Status</nz-form-label>
                <nz-form-control>
                  <label>
                    <nz-switch formControlName="isActive"></nz-switch>
                    <span style="margin-left: 8px;">Active Campaign</span>
                  </label>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Description & Customer Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="description" placeholder="Terms and conditions..."></textarea>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Promo Code Simulator Modal -->
      <nz-modal
        [(nzVisible)]="isSimulatorVisible"
        nzTitle="Promotion Code Simulator"
        [nzFooter]="null"
        (nzOnCancel)="isSimulatorVisible = false">
        <ng-container *nzModalContent>
          <div class="sim-container">
            <p class="text-secondary">Simulate how a promotional code evaluates against basket total at checkout</p>

            <div class="sim-inputs">
              <div class="form-row">
                <label>Promo Code:</label>
                <input aria-label="Enter coupon code" nz-input [(ngModel)]="simCode" placeholder="Enter coupon code" style="text-transform: uppercase;" />
              </div>
              <div class="form-row">
                <label>Basket Total ({{ currency() }}):</label>
                <nz-input-number [(ngModel)]="simTotal" [nzMin]="1" style="width: 100%"></nz-input-number>
              </div>
              <button nz-button nzType="primary" [nzLoading]="isSimulating()" (click)="runSimulation()" [disabled]="!simCode">
                <span nz-icon nzType="check"></span> Evaluate Code
              </button>
            </div>

            <nz-divider *ngIf="simResult()"></nz-divider>

            <div *ngIf="simResult() as res" class="sim-result">
              <nz-alert
                [nzType]="res.isValid ? 'success' : 'error'"
                [nzMessage]="res.isValid ? 'Coupon Valid' : 'Coupon Rejected'"
                [nzDescription]="res.message || ''"
                nzShowIcon>
              </nz-alert>

              <div *ngIf="res.isValid" class="discount-breakdown">
                <div class="breakdown-row">
                  <span>Gross Basket Amount:</span>
                  <span>{{ currency() }} {{ simTotal | number:'1.2-2' }}</span>
                </div>
                <div class="breakdown-row discount-row">
                  <span>Promotion Discount:</span>
                  <span>- {{ currency() }} {{ res.calculatedDiscount | number:'1.2-2' }}</span>
                </div>
                <nz-divider style="margin: 8px 0;"></nz-divider>
                <div class="breakdown-row total-row font-bold">
                  <span>Payable Total:</span>
                  <span>{{ currency() }} {{ (simTotal - res.calculatedDiscount) | number:'1.2-2' }}</span>
                </div>
              </div>
            </div>
          </div>
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
    }
    .promo-badge {
      background: #f0f5ff;
      color: #2f54eb;
      border: 1px dashed #adc6ff;
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: bold;
    }
    .font-bold {
      font-weight: 600;
    }
    .usage-cell {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 100px;
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
    .discount-row {
      color: #52c41a;
      font-weight: 500;
    }
    .total-row {
      font-size: 16px;
    }
  `]
})
export class PromotionsPageComponent implements OnInit {
  private readonly promotionsApi = inject(RetailPromotionsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly isSaving = signal(false);
  readonly isSimulating = signal(false);
  readonly simResult = signal<PromoValidationResultDto | null>(null);

  isModalVisible = false;
  isSimulatorVisible = false;
  simCode = '';
  simTotal = 250;

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    type: ['PercentageDiscount', [Validators.required]],
    discountValue: [10, [Validators.required, Validators.min(0.01)]],
    minimumOrderAmount: [null],
    startDate: [new Date(), [Validators.required]],
    endDate: [new Date(Date.now() + 30 * 86400000), [Validators.required]],
    usageLimit: [null],
    isActive: [true],
    description: ['']
  });

  readonly columns: TableColumn<RetailPromotionDto>[] = [
    { key: 'code', label: 'Promo Code', sortField: 'code' },
    { key: 'name', label: 'Campaign' },
    { key: 'type', label: 'Type' },
    { key: 'discountValue', label: 'Discount', align: 'right' },
    { key: 'validity', label: 'Validity Period' },
    { key: 'usage', label: 'Redemptions' },
    { key: 'isActive', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  readonly list = createPagedList<RetailPromotionDto>((q: any) => this.promotionsApi.list(q));

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
  }

  getUsagePercent(used: number, limit: number): number {
    if (!limit || limit <= 0) return 0;
    return Math.min(100, Math.round((used / limit) * 100));
  }

  openCreateModal(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id || '',
      code: '',
      name: '',
      type: 'PercentageDiscount',
      discountValue: 10,
      minimumOrderAmount: null,
      startDate: new Date(),
      endDate: new Date(Date.now() + 30 * 86400000),
      usageLimit: null,
      isActive: true,
      description: ''
    });
    this.isModalVisible = true;
  }

  closeModal(): void {
    this.isModalVisible = false;
  }

  openSimulatorModal(): void {
    this.simResult.set(null);
    this.isSimulatorVisible = true;
  }

  testCode(code: string): void {
    this.simCode = code;
    this.openSimulatorModal();
    this.runSimulation();
  }

  runSimulation(): void {
    if (!this.simCode) return;
    const entityId = this.entities()[0]?.id;
    if (!entityId) {
      this.msg.warning('No active workspace entity found');
      return;
    }

    this.isSimulating.set(true);
    this.promotionsApi.validateCode(entityId, this.simCode, this.simTotal).subscribe({
      next: res => {
        this.simResult.set(res);
        this.isSimulating.set(false);
      },
      error: () => {
        this.msg.error('Error testing promotion code');
        this.isSimulating.set(false);
      }
    });
  }

  savePromotion(): void {
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
    const input: CreatePromotionInput = {
      legalEntityId: val.legalEntityId,
      code: val.code.toUpperCase(),
      name: val.name,
      type: val.type,
      discountValue: val.discountValue,
      minimumOrderAmount: val.minimumOrderAmount,
      startDate: new Date(val.startDate).toISOString().split('T')[0],
      endDate: new Date(val.endDate).toISOString().split('T')[0],
      usageLimit: val.usageLimit,
      isActive: val.isActive,
      description: val.description
    };

    this.promotionsApi.create(input).subscribe({
      next: () => {
        this.msg.success('Promotion created successfully');
        this.isSaving.set(false);
        this.closeModal();
        window.location.reload();
      },
      error: () => this.isSaving.set(false)
    });
  }
}
