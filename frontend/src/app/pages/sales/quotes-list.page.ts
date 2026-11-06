import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
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
import { ItemsApi, ItemDto, LegalEntitiesApi, TaxCodeDto, TaxCodesApi } from '../../core/api/admin.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { createPagedList } from '../../core/api/paged-list';
import { QuoteInput, QuoteSummaryDto, SalesKpiApi, SalesKpiDto, SalesQuotesApi } from '../../core/api/sales.api';
import { PartiesApi, PartyDto } from '../parties.page';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

import { StatCardComponent } from '../../shared/stat-card.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

@Component({
  selector: 'erp-quotes-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatusTagComponent,
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
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
  template: `
    <h1 class="erp-page-title">Sales Quotes</h1>

    <!-- KPI Stat Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="quote-stats">
      <erp-stat-card
        label="Open Quotes"
        [value]="kpis()?.openQuotesCount ?? 0"
        format="number"
        caption="draft or sent"
        [tone]="1" />
      <erp-stat-card
        label="Open Quote Value"
        [value]="kpis()?.openQuotesAmount ?? 0"
        format="money"
        caption="draft and sent quotes"
        [tone]="0" />
      <erp-stat-card
        label="Converted to Orders"
        [value]="kpis()?.conversionRate ?? 0"
        format="percent"
        caption="share of all quotes"
        [tone]="4" />
      <erp-stat-card
        label="Total Quotes"
        [value]="kpis()?.totalQuotesCount ?? 0"
        format="number"
        caption="all time, every status"
        [tone]="3" />
    </div>

    <!-- Quotes Table -->
    <erp-paged-table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search quote number, customer..."
      exportFilename="sales-quotes"
      (rowClick)="openQuote($event)">

      <button toolbar *erpHasPermission="'sales.quotes.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="quote-new">
        <span nz-icon nzType="plus"></span>New Quote
      </button>
      
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'number') {
          <a [routerLink]="['/sales/quotes', row.id]" class="quote-link" (click)="$event.stopPropagation()">
            <strong>{{ row.number }}</strong>
          </a>
        } @else if (column.key === 'customerName') {
          <div class="customer-cell">
            <span class="customer-name">{{ row.customerName }}</span>
          </div>
        } @else if (column.key === 'totalAmount') {
          <strong class="amount">{{ row.currency }} {{ row.totalAmount | number:'1.2-2' }}</strong>
        } @else if (column.key === 'status') {
          <erp-status [value]="row.status" />
        } @else if (column.key === 'actions') {
          <div class="action-buttons" (click)="$event.stopPropagation()">
            <a [href]="'/print/quote/' + row.id" target="_blank" nz-button nzType="link" nzSize="small">
              <span nz-icon nzType="printer"></span>Print
            </a>
            @if (row.status === 'accepted' && !row.convertedSalesOrderId) {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="link" nzSize="small" (click)="convertToOrder(row.id)">
                Convert
              </button>
            }
            @if (row.status === 'draft') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button nzType="link" nzDanger nzSize="small"
                      nz-popconfirm nzPopconfirmTitle="Delete this quote?" (nzOnConfirm)="deleteQuote(row.id)">
                Delete
              </button>
            }
          </div>
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>

      <!-- Create Quote Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="New Sales Quote"
        [nzWidth]="850"
        (nzOnClose)="drawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form nz-form [formGroup]="form" nzLayout="vertical" (ngSubmit)="submitForm()">
            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Legal Entity</nz-form-label>
                  <nz-form-control nzErrorTip="Please select a legal entity">
                    <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                      @for (e of entities(); track e.id) {
                        <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                      }
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Quote Number (Auto if blank)</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="number" placeholder="QT-2026-0001" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Customer</nz-form-label>
                  <nz-form-control nzErrorTip="Customer is required">
                    <nz-select
                      formControlName="customerPartyId"
                      nzShowSearch
                      nzPlaceHolder="Select Customer"
                      (ngModelChange)="onCustomerSelected($event)">
                      @for (c of customers(); track c.id) {
                        <nz-option [nzValue]="c.id" [nzLabel]="c.name + ' (' + c.code + ')'"></nz-option>
                      }
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Customer Email</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="customerEmail" type="email" placeholder="billing@client.com" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Quote Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="quoteDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label>Valid Until</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="validUntil" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Currency</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="currency" placeholder="QAR" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <!-- Lines Section -->
            <div class="lines-header">
              <h3>Line Items</h3>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addLine()">
                <span nz-icon nzType="plus"></span>Add Item
              </button>
            </div>

            <div formArrayName="lines" class="lines-table-container">
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 220px;">Item / Product</th>
                    <th>Description</th>
                    <th style="width: 80px;">Qty</th>
                    <th style="width: 100px;">Price</th>
                    <th style="width: 75px;">Disc %</th>
                    <th style="width: 75px;">Tax %</th>
                    <th style="width: 100px;">Total</th>
                    <th style="width: 40px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (ctrl of linesArray.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td style="width: 220px;">
                        <nz-select
                          formControlName="itemId"
                          nzShowSearch
                          nzAllowClear
                          nzPlaceHolder="Select Item"
                          (ngModelChange)="onItemSelected($index, $event)"
                          style="width: 100%;">
                          @for (it of items(); track it.id) {
                            <nz-option [nzValue]="it.id" [nzLabel]="it.sku + ' - ' + it.name"></nz-option>
                          }
                        </nz-select>
                      </td>
                      <td><input aria-label="Item description" nz-input formControlName="description" placeholder="Item description" /></td>
                      <td><nz-input-number formControlName="quantity" [nzMin]="1" [nzStep]="1" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="unitPrice" [nzMin]="0" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="discountPercent" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="taxRate" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td class="line-calc">{{ calculateLineTotal(ctrl.value) | number:'1.2-2' }}</td>
                      <td>
                        @if (linesArray.length > 1) {
                          <button nz-button nzType="text" nzDanger nzSize="small" type="button" aria-label="Remove line" nz-tooltip nzTooltipTitle="Remove line" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="quote-summary-panel">
              <div class="summary-line">
                <span>Subtotal:</span>
                <span>{{ calculatedTotals().subTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-line">
                <span>Total Tax:</span>
                <span>{{ calculatedTotals().taxTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-line total-highlight">
                <span>Total Amount:</span>
                <span>{{ form.get('currency')?.value }} {{ calculatedTotals().total | number:'1.2-2' }}</span>
              </div>
            </div>

            <div nz-row [nzGutter]="16" style="margin-top: 16px;">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Payment Terms</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="terms" rows="2" placeholder="e.g. 50% advance, balance on delivery"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Internal Notes</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="notes" rows="2" placeholder="Notes for sales team"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Save Quote</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .quote-link { color: var(--erp-color-primary); font-weight: 600; white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .quote-link:hover { text-decoration: underline; color: var(--erp-color-primary-hover); }
    .customer-cell { display: flex; flex-direction: column; }
    .customer-name { font-weight: 600; color: var(--erp-color-text-primary); }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .lines-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
    .lines-header h3 { margin: 0; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .lines-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .lines-table th { background: var(--erp-color-bg-subtle); padding: 8px; text-align: left; font-weight: 600; color: var(--erp-color-text-secondary); border: 1px solid var(--erp-color-border-subtle); }
    .lines-table td { padding: 4px; border: 1px solid var(--erp-color-border-subtle); vertical-align: middle; }
    .line-calc { text-align: right; padding-inline-end: 8px; font-weight: 600; color: var(--erp-color-text-primary); }
    .quote-summary-panel { margin-top: 16px; margin-inline-start: auto; width: 280px; background: var(--erp-color-bg-subtle); border-radius: 8px; padding: 12px; border: 1px solid var(--erp-color-border-subtle); }
    .summary-line { display: flex; justify-content: space-between; margin-bottom: 6px; font-size: 13px; color: var(--erp-color-text-secondary); }
    .total-highlight { border-top: 1px solid var(--erp-color-border-default); padding-top: 6px; margin-top: 6px; font-weight: 700; font-size: 15px; color: var(--erp-color-text-primary); }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
  `],
})
export class QuotesListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly quotesApi = inject(SalesQuotesApi);
  private readonly kpiApi = inject(SalesKpiApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly taxCodesApi = inject(TaxCodesApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.quotesApi.list(q), { sort: 'number' });
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

  openQuote(row: QuoteSummaryDto): void {
    this.router.navigate(['/sales/quotes', row.id]);
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
        customerEmail: p.email || this.form.value.customerEmail,
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
    const input: QuoteInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      customerPartyId: raw.customerPartyId || null,
      customerName: raw.customerName || (this.customers().find((x) => x.id === raw.customerPartyId)?.name ?? ''),
      customerEmail: raw.customerEmail || null,
      quoteDate: this.formatDate(raw.quoteDate),
      validUntil: raw.validUntil ? this.formatDate(raw.validUntil) : null,
      currency: raw.currency,
      terms: raw.terms || null,
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

    this.quotesApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(`Quote ${created.number} created successfully.`);
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
        this.message.success(`Converted into Sales Order ${order.number}`);
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/sales/orders', order.id]);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to convert quote')),
    });
  }

  deleteQuote(id: string): void {
    this.quotesApi.delete(id).subscribe({
      next: () => {
        this.message.success('Quote deleted');
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete quote')),
    });
  }

  private formatDate(d: Date | string): string {
    const date = new Date(d);
    return date.toISOString().split('T')[0];
  }
}
