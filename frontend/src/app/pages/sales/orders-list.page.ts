import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { OrderInput, OrderSummaryDto, SalesKpiApi, SalesKpiDto, SalesOrdersApi } from '../../core/api/sales.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
  template: `
    <h1 class="erp-page-title">Sales Orders</h1>

    <!-- KPI Stat Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="order-stats">
      <erp-stat-card
        label="Open Orders"
        [value]="kpis()?.openOrdersCount ?? 0"
        format="number"
        caption="orders in fulfillment"
        [tone]="1" />
      <erp-stat-card
        label="Order Backlog"
        [value]="kpis()?.openOrdersAmount ?? 0"
        format="money"
        caption="unfulfilled delivery value"
        [tone]="3" />
      <erp-stat-card
        label="Total Orders"
        [value]="kpis()?.totalOrdersCount ?? 0"
        format="number"
        caption="all time orders"
        [tone]="4" />
      <erp-stat-card
        label="Gross Revenue"
        [value]="kpis()?.totalSales ?? 0"
        format="money"
        caption="total volume booked"
        [tone]="0" />
    </div>

    <!-- Orders Table -->
    <erp-paged-table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search order number, customer, PO...">

      <button toolbar *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="order-new">
        <span nz-icon nzType="plus"></span>New Sales Order
      </button>
      
      <ng-template #cell let-row let-column="column">
        @if (column.key === 'number') {
          <a [routerLink]="['/sales/orders', row.id]" class="order-link">
            <strong>{{ row.number }}</strong>
          </a>
        } @else if (column.key === 'customerName') {
          <div class="customer-cell">
            <span class="customer-name">{{ row.customerName }}</span>
            @if (row.customerReference) {
              <span class="customer-ref">Ref: {{ row.customerReference }}</span>
            }
          </div>
        } @else if (column.key === 'totalAmount') {
          <strong>{{ row.currency }} {{ row.totalAmount | number:'1.2-2' }}</strong>
        } @else if (column.key === 'status') {
          <nz-tag [nzColor]="statusColor(row.status)">{{ row.status }}</nz-tag>
        } @else if (column.key === 'invoiced') {
          @if (row.invoicedInvoiceId) {
            <nz-tag nzColor="green"><span nz-icon nzType="check"></span> Invoiced</nz-tag>
          } @else {
            <nz-tag nzColor="default">Pending</nz-tag>
          }
        } @else if (column.key === 'actions') {
              <div class="action-buttons">
                <a [routerLink]="['/sales/orders', row.id]" nz-button nzType="link" nzSize="small">
                  Manage
                </a>
                <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="link" nzDanger nzSize="small"
                        nz-popconfirm nzPopconfirmTitle="Delete this order?" (nzOnConfirm)="deleteOrder(row.id)">
                  Delete
                </button>
              </div>
            }
          </ng-template>
        </erp-paged-table>

      <!-- Create Order Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="New Sales Order"
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
                  <nz-form-label>Order Number (Auto if blank)</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="number" placeholder="SO-2026-0001" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Customer Name</nz-form-label>
                  <nz-form-control nzErrorTip="Customer name is required">
                    <input nz-input formControlName="customerName" placeholder="Client or Company Name" />
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Customer PO / Reference</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="customerReference" placeholder="e.g. PO-8839" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Order Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="orderDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label>Delivery Due Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="expectedDeliveryDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Currency</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="currency" placeholder="AED" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <!-- Lines Section -->
            <div class="lines-header">
              <h3>Order Line Items</h3>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addLine()">
                <span nz-icon nzType="plus"></span>Add Item
              </button>
            </div>

            <div formArrayName="lines" class="lines-table-container">
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 120px;">Item Code</th>
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
                      <td><input nz-input formControlName="itemCode" placeholder="Code" /></td>
                      <td><input nz-input formControlName="description" placeholder="Item description" /></td>
                      <td><nz-input-number formControlName="quantity" [nzMin]="1" [nzStep]="1" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="unitPrice" [nzMin]="0" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="discountPercent" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="taxRate" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td class="line-calc">{{ calculateLineTotal(ctrl.value) | number:'1.2-2' }}</td>
                      <td>
                        @if (linesArray.length > 1) {
                          <a (click)="removeLine($index)" style="color: #ef4444;"><span nz-icon nzType="delete"></span></a>
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
                  <nz-form-label>Shipping Address</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="shippingAddress" rows="2" placeholder="Destination address"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Payment Terms</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="paymentTerms" rows="2" placeholder="e.g. Net 30 days"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Create Order</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .order-link { color: #10955d; font-weight: 600; }
    .order-link:hover { text-decoration: underline; color: #0c7347; }
    .customer-cell { display: flex; flex-direction: column; }
    .customer-name { font-weight: 600; color: #1e293b; }
    .customer-ref { font-size: 12px; color: #64748b; }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .lines-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; border-top: 1px solid #e2e8f0; padding-top: 16px; }
    .lines-header h3 { margin: 0; font-size: 15px; font-weight: 600; color: #1e293b; }
    .lines-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .lines-table th { background: #f8fafc; padding: 8px; text-align: left; font-weight: 600; color: #475569; border: 1px solid #e2e8f0; }
    .lines-table td { padding: 4px; border: 1px solid #e2e8f0; vertical-align: middle; }
    .line-calc { text-align: right; padding-right: 8px; font-weight: 600; color: #0f172a; }
    .quote-summary-panel { margin-top: 16px; margin-left: auto; width: 280px; background: #f8fafc; border-radius: 8px; padding: 12px; border: 1px solid #e2e8f0; }
    .summary-line { display: flex; justify-content: space-between; margin-bottom: 6px; font-size: 13px; color: #475569; }
    .total-highlight { border-top: 1px solid #cbd5e1; padding-top: 6px; margin-top: 6px; font-weight: 700; font-size: 15px; color: #0f172a; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; border-top: 1px solid #f1f5f9; padding-top: 16px; }
  `],
})
export class OrdersListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly kpiApi = inject(SalesKpiApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);

  readonly list = createPagedList((q) => this.ordersApi.list(q), { sort: 'number' });
  readonly kpis = signal<SalesKpiDto | null>(null);
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', Validators.required],
    number: [''],
    customerName: ['', Validators.required],
    customerReference: [''],
    orderDate: [new Date(), Validators.required],
    expectedDeliveryDate: [null],
    currency: ['AED', Validators.required],
    paymentTerms: ['Net 30'],
    shippingAddress: [''],
    notes: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<OrderSummaryDto>[] = [
    { key: 'number', label: 'Order #', sortField: 'number', width: '130px' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'orderDate', label: 'Order Date', sortField: 'orderDate', width: '120px' },
    { key: 'expectedDeliveryDate', label: 'Delivery Due', sortField: 'expectedDeliveryDate', width: '120px' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '150px' },
    { key: 'status', label: 'Status', sortField: 'status', width: '120px' },
    { key: 'invoiced', label: 'Invoice', width: '110px' },
    { key: 'actions', label: 'Actions', width: '130px' },
  ];

  readonly calculatedTotals = computed(() => {
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
  });

  constructor() {
    this.loadKpis();
    this.loadEntities();
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

  openCreateDrawer(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id ?? '',
      number: '',
      customerName: '',
      customerReference: '',
      orderDate: new Date(),
      expectedDeliveryDate: null,
      currency: 'AED',
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
        itemCode: [''],
        description: ['', Validators.required],
        quantity: [1, [Validators.required, Validators.min(0.01)]],
        unitOfMeasure: ['EA'],
        unitPrice: [0, [Validators.required, Validators.min(0)]],
        discountPercent: [0, [Validators.min(0), Validators.max(100)]],
        taxRate: [5, [Validators.min(0), Validators.max(100)]],
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

  statusColor(status: string): string {
    switch (status) {
      case 'Confirmed': return 'blue';
      case 'Processing': return 'orange';
      case 'Delivered': return 'cyan';
      case 'Invoiced': return 'green';
      case 'Cancelled': return 'red';
      default: return 'default';
    }
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
      customerName: raw.customerName,
      customerReference: raw.customerReference || null,
      orderDate: this.formatDate(raw.orderDate),
      expectedDeliveryDate: raw.expectedDeliveryDate ? this.formatDate(raw.expectedDeliveryDate) : null,
      currency: raw.currency,
      paymentTerms: raw.paymentTerms || null,
      shippingAddress: raw.shippingAddress || null,
      notes: raw.notes || null,
      lines: raw.lines,
    };

    this.ordersApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(`Sales Order ${created.number} created.`);
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/sales/orders', created.id]);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(err?.error?.message ?? 'Failed to create sales order');
      },
    });
  }

  deleteOrder(id: string): void {
    this.ordersApi.delete(id).subscribe({
      next: () => {
        this.message.success('Sales order deleted');
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to delete order'),
    });
  }

  private formatDate(d: Date | string): string {
    const date = new Date(d);
    return date.toISOString().split('T')[0];
  }
}
