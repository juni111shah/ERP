import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { createPagedList } from '@core/api/paged-list';
import { PurchaseOrderInput, PurchaseOrderSummaryDto, PurchaseOrdersApi, PurchasingKpiApi, PurchasingKpiDto } from '@core/api/purchasing.api';
import { toUtcIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { DocumentTotals, PricedLine, documentTotals, lineTotal } from '@shared/forms/line-totals';
import { loadInto } from '@shared/utils/load-into';
import { PurchaseLineValue, fillPurchaseLineFromItem, newPurchaseLine } from '../../purchasing-lines';
import { PurchasingLookups } from '../../purchasing-lookups';

/** The purchase-order form's raw value. */
interface PurchaseOrderFormValue {
  legalEntityId: string;
  number: string;
  vendorPartyId: string | null;
  vendorName: string;
  vendorReference: string;
  orderDate: Date;
  expectedDeliveryDate: Date | null;
  currency: string;
  paymentTerms: string;
  deliveryAddress: string;
  notes: string;
  lines: PurchaseLineValue[];
}

/** Purchase orders: KPI cards, the paged list and the new-order drawer. */
@Component({
  selector: 'erp-purchase-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzFormModule,
    NzGridModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
    NzToolTipModule,
    HasPermissionDirective,
    PagedTableComponent,
    StatCardComponent,
    StatusTagComponent,
  ],
  providers: [PurchasingLookups],
  templateUrl: './purchase-orders-list.page.html',
  styleUrl: './purchase-orders-list.page.scss',
})
export class PurchaseOrdersListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly kpiApi = inject(PurchasingKpiApi);
  private readonly lookups = inject(PurchasingLookups);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.ordersApi.list(q), { sort: 'number' });
  readonly kpis = signal<PurchasingKpiDto | null>(null);
  readonly entities = this.lookups.entities;
  readonly vendors = this.lookups.vendors;
  readonly items = this.lookups.items;
  readonly taxCodes = this.lookups.taxCodes;
  readonly defaultTaxRate = this.lookups.defaultTaxRate;
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', Validators.required],
    number: [''],
    vendorPartyId: [null, Validators.required],
    vendorName: ['', Validators.required],
    vendorReference: [''],
    orderDate: [new Date(), Validators.required],
    expectedDeliveryDate: [null],
    currency: [this.currencyService.code(), Validators.required],
    paymentTerms: ['Net 30'],
    deliveryAddress: ['Central Warehouse'],
    notes: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<PurchaseOrderSummaryDto>[] = [
    { key: 'number', label: 'PO #', sortField: 'number', width: '140px' },
    { key: 'vendorName', label: 'Vendor / Supplier', sortField: 'vendorName' },
    { key: 'orderDate', label: 'Order Date', sortField: 'orderDate', width: '120px', filterField: 'orderDate' },
    { key: 'expectedDeliveryDate', label: 'Expected Due', sortField: 'expectedDeliveryDate', width: '120px', filterField: 'expectedDeliveryDate' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '160px', align: 'right', filter: false },
    { key: 'status', label: 'Status', width: '140px', filterField: 'status', filterOptions: ['draft', 'submitted', 'approved', 'partiallyReceived', 'received', 'billed', 'cancelled'] },
    { key: 'invoiced', label: 'Bill', width: '110px', filter: false, value: (r) => (r.invoicedBillId ? 'Billed' : 'Pending') },
    { key: 'actions', label: 'Actions', width: '140px' },
  ];

  /** A plain function: the form array is not a signal, so a computed would keep its first result. */
  readonly calculatedTotals = (): DocumentTotals => documentTotals(this.linesArray.value as PricedLine[]);

  constructor() {
    this.loadKpis();
    this.lookups.loadForOrders();
  }

  loadKpis(): void {
    loadInto(this.kpis, this.kpiApi.getSummary());
  }

  openOrder(row: PurchaseOrderSummaryDto): void {
    this.router.navigate(['/purchasing/orders', row.id]);
  }

  /** Copies the picked vendor's name and currency onto the order. */
  onVendorSelected(partyId: string | null): void {
    if (!partyId) return;
    const vendor = this.lookups.vendor(partyId);
    if (!vendor) return;
    this.form.patchValue({
      vendorName: vendor.name,
      currency: vendor.currencyCode || this.form.value.currency || this.currencyService.code(),
    });
  }

  onItemSelected(index: number, itemId: string | null): void {
    if (!itemId) return;
    const item = this.lookups.item(itemId);
    if (item) fillPurchaseLineFromItem(this.linesArray.at(index), item, this.taxCodes(), this.defaultTaxRate());
  }

  openCreateDrawer(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id ?? '',
      number: '',
      vendorPartyId: null,
      vendorName: '',
      vendorReference: '',
      orderDate: new Date(),
      expectedDeliveryDate: null,
      currency: this.currencyService.code(),
      paymentTerms: 'Net 30',
      deliveryAddress: '',
      notes: '',
    });
    this.linesArray.clear();
    this.addLine();
    this.drawerVisible.set(true);
  }

  addLine(): void {
    this.linesArray.push(newPurchaseLine(this.fb, this.defaultTaxRate()));
  }

  removeLine(index: number): void {
    this.linesArray.removeAt(index);
  }

  calculateLineTotal(line: PricedLine): number {
    return lineTotal(line);
  }

  submitForm(): void {
    if (this.form.invalid) {
      revealErrors(this.form, { onlySelf: true });
      return;
    }

    this.isSubmitting.set(true);
    this.ordersApi.create(this.toInput(this.form.value as PurchaseOrderFormValue)).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(doneText('created', 'Purchase order', created.number));
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/purchasing/orders', created.id]);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(problemMessage(err, 'Failed to create purchase order'));
      },
    });
  }

  deleteOrder(row: { id: string; number?: string | null }): void {
    this.ordersApi.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Purchase order', row.number));
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete order')),
    });
  }

  /** The form as the API takes it; the lines go as entered. */
  private toInput(raw: PurchaseOrderFormValue): PurchaseOrderInput {
    return {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      vendorPartyId: raw.vendorPartyId || null,
      vendorName: raw.vendorName,
      vendorReference: raw.vendorReference || null,
      orderDate: toUtcIsoDate(raw.orderDate),
      expectedDeliveryDate: raw.expectedDeliveryDate ? toUtcIsoDate(raw.expectedDeliveryDate) : null,
      currency: raw.currency,
      paymentTerms: raw.paymentTerms || null,
      deliveryAddress: raw.deliveryAddress || null,
      notes: raw.notes || null,
      lines: raw.lines,
    };
  }
}
