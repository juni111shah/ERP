import { doneText } from '@core/format/done-text';
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
import { ItemsApi, LegalEntitiesApi } from '@core/api/admin.api';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { createPagedList } from '@core/api/paged-list';
import {
  CreateWholesalePriceInput,
  DistributionWholesaleApi,
  WholesalePriceMatrixDto,
  WholesaleRateCalculationDto,
  WholesaleTier
} from '@core/api/distribution.api';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { problemMessage } from '@core/http/problem-details';

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
  templateUrl: './wholesale-pricing.page.html',
  styleUrl: './wholesale-pricing.page.scss'
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
        this.msg.success(doneText('deleted', 'Price rule', row.itemName));
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
        this.msg.success(doneText('saved', 'Price rule', this.catalogItems().find(i => i.id === val.itemId)?.name));
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
