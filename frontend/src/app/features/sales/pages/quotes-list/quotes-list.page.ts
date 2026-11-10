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
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { createPagedList } from '@core/api/paged-list';
import { QuoteInput, QuoteSummaryDto, SalesKpiApi, SalesKpiDto, SalesQuotesApi } from '@core/api/sales.api';
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
import { DocumentTotals, PricedLine, SalesLineValue, documentTotals, fillLineFromItem, lineTotal, newSalesLine, toLineInputs } from '../../sales-lines';
import { SalesLookups } from '../../sales-lookups';

/** The quote form's raw value. */
interface QuoteFormValue {
  legalEntityId: string;
  number: string;
  customerPartyId: string | null;
  customerName: string;
  customerEmail: string;
  quoteDate: Date;
  validUntil: Date | null;
  currency: string;
  terms: string;
  notes: string;
  lines: SalesLineValue[];
}

/** Sales quotes: KPI cards, the paged list and the new-quote drawer. */
@Component({
  selector: 'erp-quotes-list',
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
    NzToolTipModule,
    HasPermissionDirective,
    PagedTableComponent,
    StatCardComponent,
    StatusTagComponent,
  ],
  providers: [SalesLookups],
  templateUrl: './quotes-list.page.html',
  styleUrl: './quotes-list.page.scss',
})
export class QuotesListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly quotesApi = inject(SalesQuotesApi);
  private readonly kpiApi = inject(SalesKpiApi);
  private readonly lookups = inject(SalesLookups);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.quotesApi.list(q), { sort: 'number' });
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
    customerEmail: [''],
    quoteDate: [new Date(), Validators.required],
    validUntil: [null],
    currency: [this.currencyService.code(), Validators.required],
    terms: [''],
    notes: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<QuoteSummaryDto>[] = [
    { key: 'number', label: 'Quote #', sortField: 'number', width: '140px' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'quoteDate', label: 'Quote Date', sortField: 'quoteDate', width: '120px', filterField: 'quoteDate' },
    { key: 'validUntil', label: 'Valid Until', sortField: 'validUntil', width: '120px', filterField: 'validUntil' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '160px', align: 'right', filter: false },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: ['draft', 'sent', 'accepted', 'declined', 'expired', 'converted'] },
    { key: 'actions', label: 'Actions', width: '160px' },
  ];

  /** A plain function: the form array is not a signal, so a computed would keep its first result. */
  readonly calculatedTotals = (): DocumentTotals => documentTotals(this.linesArray.value as PricedLine[]);

  constructor() {
    this.loadKpis();
    this.lookups.load();
  }

  openQuote(row: QuoteSummaryDto): void {
    this.router.navigate(['/sales/quotes', row.id]);
  }

  loadKpis(): void {
    loadInto(this.kpis, this.kpiApi.getSummary());
  }

  /** Copies the picked customer's name, email and currency onto the quote. */
  onCustomerSelected(partyId: string): void {
    const customer = this.lookups.customer(partyId);
    if (!customer) return;
    this.form.patchValue({
      customerName: customer.name,
      customerEmail: customer.email || this.form.value.customerEmail,
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
      customerEmail: '',
      quoteDate: new Date(),
      validUntil: null,
      currency: this.currencyService.code(),
      terms: '',
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
    const raw = this.form.value as QuoteFormValue;
    const input: QuoteInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      customerPartyId: raw.customerPartyId || null,
      customerName: raw.customerName || (this.lookups.customer(raw.customerPartyId)?.name ?? ''),
      customerEmail: raw.customerEmail || null,
      quoteDate: toUtcIsoDate(raw.quoteDate),
      validUntil: raw.validUntil ? toUtcIsoDate(raw.validUntil) : null,
      currency: raw.currency,
      terms: raw.terms || null,
      notes: raw.notes || null,
      lines: toLineInputs(raw.lines),
    };

    this.quotesApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(doneText('created', 'Quote', created.number));
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(problemMessage(err, 'Failed to create quote'));
      },
    });
  }

  convertToOrder(quoteId: string): void {
    this.quotesApi.convertToOrder(quoteId).subscribe({
      next: (order) => {
        this.message.success(`Quote converted into sales order ${order.number}`);
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/sales/orders', order.id]);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to convert quote')),
    });
  }

  deleteQuote(row: { id: string; number?: string | null }): void {
    this.quotesApi.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Quote', row.number));
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete quote')),
    });
  }
}
