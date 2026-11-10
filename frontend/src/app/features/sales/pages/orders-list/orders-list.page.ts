import { doneText } from '@core/format/done-text';
import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
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
import { ItemsApi, ItemDto, LegalEntitiesApi, TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { createPagedList } from '@core/api/paged-list';
import { OrderInput, OrderSummaryDto, SalesKpiApi, SalesKpiDto, SalesOrdersApi } from '@core/api/sales.api';
import { PartiesApi, PartyDto } from '@features/parties/pages/parties/parties.page';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';

import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

@Component({
  selector: 'erp-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatusTagComponent,
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzDatePickerModule,
    NzTableModule,
    NzPopconfirmModule,
    PagedTableComponent,
    StatCardComponent,
    HasPermissionDirective,
  ],
  templateUrl: './orders-list.page.html',
  styleUrl: './orders-list.page.scss',
})
export class OrdersListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly kpiApi = inject(SalesKpiApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly taxCodesApi = inject(TaxCodesApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.ordersApi.list(q), { sort: 'number' });
  readonly kpis = signal<SalesKpiDto | null>(null);
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly customers = signal<PartyDto[]>([]);
  readonly items = signal<ItemDto[]>([]);
  readonly taxCodes = signal<TaxCodeDto[]>([]);
  readonly defaultTaxRate = signal<number>(0);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', Validators.required],
    number: [''],
    customerPartyId: [null, Validators.required],
    customerName: ['', Validators.required],
    customerReference: [''],
    orderDate: [new Date(), Validators.required],
    expectedDeliveryDate: [null],
    currency: [this.currencyService.code(), Validators.required],
    paymentTerms: ['Net 30'],
    shippingAddress: [''],
    notes: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<OrderSummaryDto>[] = [
    { key: 'number', label: 'Order #', sortField: 'number', width: '140px' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'orderDate', label: 'Order Date', sortField: 'orderDate', width: '120px', filterField: 'orderDate' },
    { key: 'expectedDeliveryDate', label: 'Delivery Due', sortField: 'expectedDeliveryDate', width: '120px', filterField: 'expectedDeliveryDate' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '160px', align: 'right', filter: false },
    { key: 'status', label: 'Status', width: '120px', filterField: 'status', filterOptions: ['draft', 'confirmed', 'processing', 'delivered', 'invoiced', 'cancelled'] },
    { key: 'invoiced', label: 'Invoice', width: '110px', filter: false, value: (r) => (r.invoicedInvoiceId ? 'Invoiced' : 'Pending') },
    { key: 'actions', label: 'Actions', width: '140px' },
  ];

  /** A plain function: the form array is not a signal, so a computed would keep its first result. */
  readonly calculatedTotals = () => {
    let sub = 0;
    let tax = 0;
    const lines = this.linesArray.value as { quantity: number; unitPrice: number; discountPercent: number; taxRate: number }[];
    for (const l of lines) {
      const base = (l.quantity || 0) * (l.unitPrice || 0);
      const disc = base * ((l.discountPercent || 0) / 100);
      const afterDisc = base - disc;
      const taxAmt = afterDisc * ((l.taxRate || 0) / 100);
      sub += afterDisc;
      tax += taxAmt;
    }
    return { subTotal: sub, taxTotal: tax, total: sub + tax };
  };

  constructor() {
    this.loadKpis();
    this.loadEntities();
    this.loadCustomers();
    this.loadItems();
    this.loadTaxCodes();
  }

  openOrder(row: OrderSummaryDto): void {
    this.router.navigate(['/sales/orders', row.id]);
  }

  loadTaxCodes(): void {
    this.taxCodesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        const items = res.items || [];
        this.taxCodes.set(items);
        const def = items.find((t) => t.isDefault) || items.find((t) => t.kind === 'vat' || t.kind === 'salesTax') || items[0];
        if (def && typeof def.rate === 'number') {
          this.defaultTaxRate.set(def.rate);
        }
      },
      error: () => {},
    });
  }

  loadKpis(): void {
    this.kpiApi.getSummary().subscribe({
      next: (k) => this.kpis.set(k),
      error: () => {},
    });
  }

  loadEntities(): void {
    this.entitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.entities.set(res.items.map((x) => ({ id: x.id, name: x.name }))),
      error: () => {},
    });
  }

  loadCustomers(): void {
    this.partiesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.customers.set(res.items.filter((p) => !p.roles || p.roles.length === 0 || p.roles.some((r) => r.toLowerCase() === 'customer'))),
      error: () => {},
    });
  }

  loadItems(): void {
    this.itemsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.items.set(res.items),
      error: () => {},
    });
  }

  onCustomerSelected(partyId: string): void {
    const p = this.customers().find((x) => x.id === partyId);
    if (p) {
      this.form.patchValue({
        customerName: p.name,
        currency: p.currencyCode || this.form.value.currency || this.currencyService.code(),
      });
    }
  }

  onItemSelected(index: number, itemId: string): void {
    const it = this.items().find((x) => x.id === itemId);
    if (it) {
      let taxRate = this.defaultTaxRate();
      if (it.taxCodeId) {
        const tc = this.taxCodes().find((t) => t.id === it.taxCodeId);
        if (tc && typeof tc.rate === 'number') {
          taxRate = tc.rate;
        }
      }
      const row = this.linesArray.at(index);
      row.patchValue({
        itemCode: it.sku,
        description: it.name,
        unitOfMeasure: it.uom || 'EA',
        unitPrice: it.salesPrice ?? row.value.unitPrice ?? 0,
        taxRate,
      });
    }
  }

  openCreateDrawer(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id ?? '',
      number: '',
      customerPartyId: null,
      customerName: '',
      customerReference: '',
      orderDate: new Date(),
      expectedDeliveryDate: null,
      currency: this.currencyService.code(),
      paymentTerms: 'Net 30',
      shippingAddress: '',
      notes: '',
    });
    this.linesArray.clear();
    this.addLine();
    this.drawerVisible.set(true);
  }

  addLine(): void {
    this.linesArray.push(
      this.fb.group({
        itemId: [null],
        itemCode: [''],
        description: ['', Validators.required],
        quantity: [1, [Validators.required, Validators.min(0.01)]],
        unitOfMeasure: ['EA'],
        unitPrice: [0, [Validators.required, Validators.min(0)]],
        discountPercent: [0, [Validators.min(0), Validators.max(100)]],
        taxRate: [this.defaultTaxRate(), [Validators.min(0), Validators.max(100)]],
      })
    );
  }

  removeLine(index: number): void {
    this.linesArray.removeAt(index);
  }

  calculateLineTotal(val: { quantity: number; unitPrice: number; discountPercent: number; taxRate: number }): number {
    const base = (val.quantity || 0) * (val.unitPrice || 0);
    const disc = base * ((val.discountPercent || 0) / 100);
    const afterDisc = base - disc;
    const tax = afterDisc * ((val.taxRate || 0) / 100);
    return afterDisc + tax;
  }

  submitForm(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach((c) => {
        c.markAsDirty();
        c.updateValueAndValidity({ onlySelf: true });
      });
      return;
    }

    this.isSubmitting.set(true);
    const raw = this.form.value;
    const input: OrderInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      customerPartyId: raw.customerPartyId || null,
      customerName: raw.customerName || (this.customers().find((x) => x.id === raw.customerPartyId)?.name ?? ''),
      customerReference: raw.customerReference || null,
      orderDate: this.formatDate(raw.orderDate),
      expectedDeliveryDate: raw.expectedDeliveryDate ? this.formatDate(raw.expectedDeliveryDate) : null,
      currency: raw.currency,
      paymentTerms: raw.paymentTerms || null,
      shippingAddress: raw.shippingAddress || null,
      notes: raw.notes || null,
      lines: raw.lines.map((l: any) => ({
        itemId: l.itemId || null,
        itemCode: l.itemCode,
        description: l.description,
        quantity: l.quantity,
        unitOfMeasure: l.unitOfMeasure || 'EA',
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent || 0,
        taxRate: l.taxRate || 0,
      })),
    };

    this.ordersApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(doneText('created', 'Sales order', created.number));
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/sales/orders', created.id]);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(problemMessage(err, 'Failed to create sales order'));
      },
    });
  }

  deleteOrder(row: { id: string; number?: string | null }): void {
    this.ordersApi.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Sales order', row.number));
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete order')),
    });
  }

  private formatDate(d: Date | string): string {
    const date = new Date(d);
    return date.toISOString().split('T')[0];
  }
}
