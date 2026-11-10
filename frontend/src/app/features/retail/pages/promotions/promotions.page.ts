import { doneText } from '@core/format/done-text';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
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
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { displayLabel } from '@core/format/display';
import {
  CreatePromotionInput,
  PromotionType,
  PromoValidationResultDto,
  RetailPromotionDto,
  RetailPromotionsApi
} from '@core/api/retail.api';
import { createPagedList } from '@core/api/paged-list';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { problemMessage } from '@core/http/problem-details';

/** The end date field must not fall before the start date field of the same form. */
const notBeforeStart = (startKey: string) => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

@Component({
  selector: 'erp-promotions-page',
  standalone: true,
  imports: [ErpDatePipe, ErpLabelPipe, NzPopconfirmModule, StatusTagComponent, 
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
    <erp-paged-table [list]="list" [columns]="columns" [clickable]="false" searchPlaceholder="Search by code or name" emptyText="No promotions yet. Create one to offer a discount code.">
      <div toolbar style="display: flex; align-items: center; gap: 8px;">
        <button nz-button nzType="default" (click)="openSimulatorModal()">
          <span nz-icon nzType="thunderbolt"></span> Test Promo Code
        </button>
        <button *erpHasPermission="'retail.promotions.manage'" nz-button nzType="primary" (click)="openCreateModal()" data-testid="promotion-new">
          <span nz-icon nzType="plus"></span> Create Promotion
        </button>
      </div>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') { <span class="mono-code font-bold">{{ row.code }}</span> }
          @case ('name') {
            <div>{{ row.name }}</div>
            @if (row.description) { <div class="sub">{{ row.description }}</div> }
          }
          @case ('type') { {{ row.type | erpLabel }} }
          @case ('discountValue') {
            <span class="num font-bold">{{ row.type === 'percentageDiscount' ? ((row.discountValue | number:'1.0-2') + '%') : (currency() + ' ' + (row.discountValue | number:'1.2-2')) }}</span>
          }
          @case ('validity') { <span class="nowrap">{{ row.startDate | erpDate }} to {{ row.endDate | erpDate }}</span> }
          @case ('usage') {
            <div class="usage-cell">
              <span class="num">{{ row.timesUsed }}{{ row.usageLimit ? ' of ' + row.usageLimit : '' }}</span>
              @if (row.usageLimit) {
                <nz-progress [nzPercent]="getUsagePercent(row.timesUsed, row.usageLimit)" nzSize="small" [nzShowInfo]="false"></nz-progress>
              }
            </div>
          }
          @case ('status') { <erp-status [value]="promoStatus(row)" /> }
          @case ('actions') {
            <div class="table-actions">
              <button nz-button nzType="link" nzSize="small" (click)="testCode(row.code)">
                <span nz-icon nzType="experiment"></span> Test
              </button>
              <button *erpHasPermission="'retail.promotions.manage'" nz-button nzType="link" nzSize="small" (click)="openEditModal(row)">
                <span nz-icon nzType="edit"></span> Edit
              </button>
              <button *erpHasPermission="'retail.promotions.manage'" nz-button nzType="link" nzDanger nzSize="small"
                      nz-popconfirm [nzPopconfirmTitle]="'Delete promotion ' + row.code + '?'" (nzOnConfirm)="deletePromotion(row)">
                <span nz-icon nzType="delete"></span> Delete
              </button>
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

      <!-- Create Promotion Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        [nzTitle]="editingId() ? 'Edit promotion' : 'Create promotion'"
        nzOkText="Save"
        [nzOkLoading]="isSaving()"
        (nzOnCancel)="closeModal()"
        (nzOnOk)="savePromotion()"
        nzWidth="680px">
        <ng-container *nzModalContent>
          <form nz-form [formGroup]="form" nzLayout="vertical">
            <div class="row-2">
              <nz-form-item>
                <nz-form-label nzRequired>Promo code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. FLASH20" style="text-transform: uppercase;" [readonly]="!!editingId()" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Summer sale" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label nzRequired>Legal entity</nz-form-label>
                <nz-form-control nzErrorTip="Entity is required">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Discount type</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option nzValue="percentageDiscount" nzLabel="Percentage (%)"></nz-option>
                    <nz-option nzValue="fixedAmountDiscount" [nzLabel]="'Fixed amount (' + currency() + ')'"></nz-option>
                    @if (otherType(); as other) { <nz-option [nzValue]="other" [nzLabel]="other | erpLabel"></nz-option> }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Discount value</nz-form-label>
                <nz-form-control nzErrorTip="Value must be greater than 0">
                  <nz-input-number formControlName="discountValue" [nzMin]="0.01" style="width: 100%"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-3">
              <nz-form-item>
                <nz-form-label>Minimum order ({{ currency() }})</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="minimumOrderAmount" [nzMin]="0" style="width: 100%" nzPlaceHolder="No minimum"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>Start date</nz-form-label>
                <nz-form-control nzErrorTip="Start date is required">
                  <nz-date-picker formControlName="startDate" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired>End date</nz-form-label>
                <nz-form-control [nzErrorTip]="form.get('endDate')?.hasError('beforeStart') ? 'The end date cannot be before the start date' : 'End date is required'">
                  <nz-date-picker formControlName="endDate" style="width: 100%"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="row-2">
              <nz-form-item>
                <nz-form-label>Usage limit</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="usageLimit" [nzMin]="1" style="width: 100%" nzPlaceHolder="No limit"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label>Status</nz-form-label>
                <nz-form-control>
                  <label>
                    <nz-switch formControlName="isActive"></nz-switch>
                    <span style="margin-inline-start: 8px;">Active</span>
                  </label>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Description</nz-form-label>
              <nz-form-control>
                <textarea nz-input rows="2" formControlName="description" placeholder="Terms and conditions"></textarea>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Promo Code Simulator Modal -->
      <nz-modal
        [(nzVisible)]="isSimulatorVisible"
        nzTitle="Test promo code"
        [nzFooter]="null"
        (nzOnCancel)="isSimulatorVisible.set(false)">
        <ng-container *nzModalContent>
          <div class="sim-container">
            <p class="text-secondary">See how a code applies to a basket. Nothing is saved or used up.</p>

            <div class="sim-inputs">
              <div class="form-row">
                <label for="sim-code">Promo code</label>
                <input id="sim-code" nz-input [(ngModel)]="simCode" style="text-transform: uppercase;" />
              </div>
              <div class="form-row">
                <label for="sim-total">Basket total ({{ currency() }})</label>
                <nz-input-number nzId="sim-total" [(ngModel)]="simTotal" [nzMin]="1" style="width: 100%"></nz-input-number>
              </div>
              <button nz-button nzType="primary" [nzLoading]="isSimulating()" (click)="runSimulation()" [disabled]="!simCode">
                <span nz-icon nzType="check"></span> Test
              </button>
            </div>

            <nz-divider *ngIf="simResult()"></nz-divider>

            <div *ngIf="simResult() as res" class="sim-result">
              <nz-alert
                [nzType]="res.isValid ? 'success' : 'error'"
                [nzMessage]="res.isValid ? 'Code is valid' : 'Code is not valid'"
                [nzDescription]="res.message || ''"
                nzShowIcon>
              </nz-alert>

              <div *ngIf="res.isValid" class="discount-breakdown">
                <div class="breakdown-row">
                  <span>Basket total</span>
                  <span>{{ currency() }} {{ simTotal | number:'1.2-2' }}</span>
                </div>
                <div class="breakdown-row discount-row">
                  <span>Discount</span>
                  <span>- {{ currency() }} {{ res.calculatedDiscount | number:'1.2-2' }}</span>
                </div>
                <nz-divider style="margin: 8px 0;"></nz-divider>
                <div class="breakdown-row total-row font-bold">
                  <span>To pay</span>
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
      color: var(--erp-color-text-muted);
      margin: 4px 0 0;
    }
    .mono-code {
      font-family: var(--erp-font-family-mono);
      white-space: nowrap;
    }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
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
      background: var(--erp-color-bg-subtle);
      border-radius: 6px;
    }
    .breakdown-row {
      display: flex;
      justify-content: space-between;
      margin-bottom: 6px;
      font-size: 14px;
    }
    .discount-row {
      color: var(--erp-color-success-text);
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
  readonly editingId = signal<string | null>(null);
  /** A type this form cannot create, kept selectable when editing a promotion that has it. */
  readonly otherType = signal<PromotionType | null>(null);
  private editingCustomFields: string | null = null;
  readonly isSimulating = signal(false);
  readonly simResult = signal<PromoValidationResultDto | null>(null);

  readonly isModalVisible = signal(false);
  readonly isSimulatorVisible = signal(false);
  simCode = '';
  simTotal = 250;

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    type: ['percentageDiscount', [Validators.required]],
    discountValue: [10, [Validators.required, Validators.min(0.01)]],
    minimumOrderAmount: [null],
    startDate: [new Date(), [Validators.required]],
    endDate: [new Date(Date.now() + 30 * 86400000), [Validators.required, notBeforeStart('startDate')]],
    usageLimit: [null],
    isActive: [true],
    description: ['']
  });

  readonly columns: TableColumn<RetailPromotionDto>[] = [
    { key: 'code', label: 'Promo code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type', hidden: true },
    { key: 'discountValue', label: 'Discount', align: 'right' },
    { key: 'validity', label: 'Valid', sortField: 'startDate', filter: false, value: (r) => `${r.startDate} to ${r.endDate}` },
    { key: 'usage', label: 'Times used', filter: false, value: (r) => (r.usageLimit ? `${r.timesUsed} of ${r.usageLimit}` : r.timesUsed) },
    { key: 'status', label: 'Status', value: (r) => displayLabel(this.promoStatus(r)) },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  readonly list = createPagedList<RetailPromotionDto>((q) => this.promotionsApi.list(q), { sort: '-createdAt' });

  ngOnInit(): void {
    this.form.get('startDate')!.valueChanges.subscribe(() => this.form.get('endDate')!.updateValueAndValidity());
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => this.entities.set(res.items)
    });
  }

  /** What the promotion is doing today: the switch alone does not say whether it has ended or used up its limit. */
  promoStatus(row: RetailPromotionDto): string {
    if (!row.isActive) return 'inactive';
    const today = toIsoDate(new Date());
    if (row.startDate > today) return 'scheduled';
    if (row.endDate < today) return 'expired';
    if (row.usageLimit && row.timesUsed >= row.usageLimit) return 'limitReached';
    return 'active';
  }

  getUsagePercent(used: number, limit: number): number {
    if (!limit || limit <= 0) return 0;
    return Math.min(100, Math.round((used / limit) * 100));
  }

  openEditModal(row: RetailPromotionDto): void {
    this.editingId.set(row.id);
    this.editingCustomFields = row.customFields ?? null;
    this.otherType.set(row.type === 'percentageDiscount' || row.type === 'fixedAmountDiscount' ? null : row.type);
    this.form.reset({
      legalEntityId: row.legalEntityId,
      code: row.code,
      name: row.name,
      type: row.type,
      discountValue: row.discountValue,
      minimumOrderAmount: row.minimumOrderAmount ?? null,
      startDate: new Date(`${row.startDate}T00:00:00`),
      endDate: new Date(`${row.endDate}T00:00:00`),
      usageLimit: row.usageLimit ?? null,
      isActive: row.isActive,
      description: row.description ?? ''
    });
    this.form.get('legalEntityId')!.disable({ emitEvent: false });
    this.isModalVisible.set(true);
  }

  deletePromotion(row: RetailPromotionDto): void {
    this.promotionsApi.delete(row.id).subscribe({
      next: () => {
        this.msg.success(doneText('deleted', 'Promotion', row.name));
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The promotion could not be deleted'))
    });
  }

  openCreateModal(): void {
    this.editingId.set(null);
    this.editingCustomFields = null;
    this.otherType.set(null);
    this.form.get('legalEntityId')!.enable({ emitEvent: false });
    this.form.reset({
      legalEntityId: this.entities().find(e => e.id === this.sessionStore.currentEntityId())?.id || this.entities()[0]?.id || '',
      code: '',
      name: '',
      type: 'percentageDiscount',
      discountValue: 10,
      minimumOrderAmount: null,
      startDate: new Date(),
      endDate: new Date(Date.now() + 30 * 86400000),
      usageLimit: null,
      isActive: true,
      description: ''
    });
    this.isModalVisible.set(true);
  }

  closeModal(): void {
    this.isModalVisible.set(false);
  }

  openSimulatorModal(): void {
    this.simResult.set(null);
    this.isSimulatorVisible.set(true);
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
      error: (err) => {
        this.msg.error(problemMessage(err, 'The code could not be tested'));
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
    const val = this.form.getRawValue();
    const input: CreatePromotionInput = {
      legalEntityId: val.legalEntityId,
      code: val.code.toUpperCase(),
      name: val.name,
      type: val.type,
      discountValue: val.discountValue,
      minimumOrderAmount: val.minimumOrderAmount,
      startDate: toIsoDate(new Date(val.startDate)),
      endDate: toIsoDate(new Date(val.endDate)),
      usageLimit: val.usageLimit,
      isActive: val.isActive,
      description: val.description,
      customFields: this.editingCustomFields
    };

    const id = this.editingId();
    (id ? this.promotionsApi.update(id, input) : this.promotionsApi.create(input)).subscribe({
      next: () => {
        this.msg.success(doneText('saved', 'Promotion', val.name));
        this.isSaving.set(false);
        this.closeModal();
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The promotion could not be saved'));
        this.isSaving.set(false);
      }
    });
  }
}
