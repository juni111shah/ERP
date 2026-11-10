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
import { OrderInput, OrderSummaryDto, SalesKpiApi, SalesKpiDto, SalesOrdersApi } from '@core/api/sales.api';
import { toUtcIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { loadInto } from '@shared/utils/load-into';
import { DocumentTotals, PricedLine, documentTotals, lineTotal } from '@shared/forms/line-totals';
import { SalesLineValue, fillLineFromItem, newSalesLine, toLineInputs } from '../../sales-lines';
import { SalesLookups } from '../../sales-lookups';

/** The sales-order form's raw value. */
interface OrderFormValue {
  legalEntityId: string;
  number: string;
  customerPartyId: string | null;
  customerName: string;
  customerReference: string;
  orderDate: Date;
  expectedDeliveryDate: Date | null;
  currency: string;
  paymentTerms: string;
  shippingAddress: string;
  notes: string;
  lines: SalesLineValue[];
}

/** Sales orders: KPI cards, the paged list and the new-order drawer. */
@Component({
  selector: 'erp-orders-list',
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
  providers: [SalesLookups],
  templateUrl: './orders-list.page.html',
  styleUrl: './orders-list.page.scss',
})
export class OrdersListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly kpiApi = inject(SalesKpiApi);
  private readonly lookups = inject(SalesLookups);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.ordersApi.list(q), { sort: 'number' });
  readonly kpis = signal<SalesKpiDto | null>(null);
  readonly entities = this.lookups.entities;
  readonly customers = this.lookups.customers;
  readonly items = this.lookups.items;
  readonly taxCodes = this.lookups.taxCodes;
  readonly defaultTaxRate = this.lookups.defaultTaxRate;
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
  readonly calculatedTotals = (): DocumentTotals => documentTotals(this.linesArray.value as PricedLine[]);

  constructor() {
    this.loadKpis();
    this.lookups.load();
  }

  openOrder(row: OrderSummaryDto): void {
    this.router.navigate(['/sales/orders', row.id]);
  }

  loadKpis(): void {
    loadInto(this.kpis, this.kpiApi.getSummary());
  }

  /** Copies the picked customer's name and currency onto the order. */
  onCustomerSelected(partyId: string): void {
    const customer = this.lookups.customer(partyId);
    if (!customer) return;
    this.form.patchValue({
      customerName: customer.name,
      currency: customer.currencyCode || this.form.value.currency || this.currencyService.code(),
    });
  }

  onItemSelected(index: number, itemId: string): void {
    const item = this.lookups.item(itemId);
    if (item) fillLineFromItem(this.linesArray.at(index) as FormGroup, item, this.taxCodes(), this.defaultTaxRate());
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
    this.linesArray.push(newSalesLine(this.fb, this.defaultTaxRate()));
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
    const raw = this.form.value as OrderFormValue;
    const input: OrderInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      customerPartyId: raw.customerPartyId || null,
      customerName: raw.customerName || (this.lookups.customer(raw.customerPartyId)?.name ?? ''),
      customerReference: raw.customerReference || null,
      orderDate: toUtcIsoDate(raw.orderDate),
      expectedDeliveryDate: raw.expectedDeliveryDate ? toUtcIsoDate(raw.expectedDeliveryDate) : null,
      currency: raw.currency,
      paymentTerms: raw.paymentTerms || null,
      shippingAddress: raw.shippingAddress || null,
      notes: raw.notes || null,
      lines: toLineInputs(raw.lines),
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
}
