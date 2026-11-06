import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CreateProjectButtonComponent } from '../../shared/create-project-button.component';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { OrderDto, OrderFulfillmentLineInput, SalesOrdersApi, SalesOrderStatus } from '../../core/api/sales.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { displayLabel } from '../../core/format/display';

@Component({
  selector: 'erp-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatusTagComponent, NzSkeletonModule, NzToolTipModule,
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    CreateProjectButtonComponent,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTableModule,
    NzDescriptionsModule,
    NzProgressModule,
    NzModalModule,
    NzInputNumberModule,
    NzPopconfirmModule,
    HasPermissionDirective,
  ],
  template: `
    <div class="order-detail-container">
      @if (order(); as o) {
        <div class="header-nav">
          <a routerLink="/sales/orders" class="back-link">
            <span nz-icon nzType="arrow-left"></span> Back to Sales Orders
          </a>
        </div>

        <div class="page-header">
          <div class="title-section">
            <div class="number-tag">
              <h1 class="order-number">{{ o.number }}</h1>
              <erp-status [value]="o.status" />
            </div>
            <p class="customer-subtitle">Customer: <strong>{{ o.customerName }}</strong></p>
          </div>

          <div class="header-actions">
            @if (o.status !== 'cancelled') {
              <erp-create-project-button sourceType="sales.order" [sourceId]="o.id" [sourceLabel]="'order ' + o.number" [defaultName]="'Delivery of ' + o.number" [legalEntityId]="o.legalEntityId" [customerId]="o.customerPartyId" [amount]="o.totalAmount" />
            }
            @if (o.status === 'draft') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" (click)="changeStatus('confirmed')">
                <span nz-icon nzType="check"></span>Confirm Order
              </button>
            }
            @if (o.status === 'confirmed') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button (click)="changeStatus('processing')" data-testid="order-start-processing">
                <span nz-icon nzType="play-circle"></span>Start Processing
              </button>
            }
            @if (o.status === 'confirmed' || o.status === 'processing') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button (click)="openFulfillmentModal()">
                <span nz-icon nzType="send"></span>Record Delivery
              </button>
            }
            @if (!o.invoicedInvoiceId && (o.status === 'confirmed' || o.status === 'processing' || o.status === 'delivered')) {
              <span nz-tooltip [nzTooltipTitle]="o.customerPartyId ? null : 'Link this order to a customer record to invoice it.'">
                <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" [disabled]="!o.customerPartyId" (click)="convertToInvoice()" data-testid="order-invoice">
                  <span nz-icon nzType="file-done"></span>Generate Invoice
                </button>
              </span>
            }
            @if (o.invoicedInvoiceId) {
              <a [routerLink]="['/finance/invoices', o.invoicedInvoiceId]" nz-button nzType="dashed">
                <span nz-icon nzType="file-text"></span>View Accounting Invoice
              </a>
            }
            <a [href]="'/print/order/' + o.id" target="_blank" nz-button data-testid="order-print">
              <span nz-icon nzType="printer"></span>Print / PDF
            </a>
            @if (o.status !== 'cancelled' && o.status !== 'invoiced' && o.status !== 'delivered') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzDanger
                      nz-popconfirm nzPopconfirmTitle="Cancel this order?" (nzOnConfirm)="changeStatus('cancelled')">
                Cancel
              </button>
            }
          </div>
        </div>

        <!-- Order Information Cards -->
        <div nz-row [nzGutter]="16" style="margin-bottom: 24px;">
          <div nz-col [nzXs]="24" [nzLg]="16">
            <nz-card nzTitle="Order Details" class="info-card">
              <nz-descriptions [nzColumn]="{ xxl: 3, xl: 3, lg: 2, md: 2, sm: 1, xs: 1 }">
                <nz-descriptions-item nzTitle="Customer">{{ o.customerName }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="PO / Reference">{{ o.customerReference || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Origin Quote">
                  @if (o.quoteId) {
                    <a [routerLink]="['/sales/quotes', o.quoteId]">{{ o.quoteNumber || 'View Quote' }}</a>
                  } @else {
                    Direct Order
                  }
                </nz-descriptions-item>
                <nz-descriptions-item nzTitle="Order Date">{{ o.orderDate | erpDate }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Delivery Due">{{ (o.expectedDeliveryDate | erpDate) || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Currency">{{ o.currency }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Payment Terms">{{ o.paymentTerms || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Shipping Address" [nzSpan]="2">{{ o.shippingAddress || '-' }}</nz-descriptions-item>
              </nz-descriptions>

              @if (o.notes) {
                <div class="note-box" style="margin-top: 16px;">
                  <strong>Special Instructions / Notes:</strong>
                  <p>{{ o.notes }}</p>
                </div>
              }
            </nz-card>
          </div>

          <div nz-col [nzXs]="24" [nzLg]="8">
            <nz-card nzTitle="Financial Summary" class="summary-card">
              <div class="summary-row">
                <span class="label">Subtotal</span>
                <span class="value">{{ o.currency }} {{ o.subTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span class="label">Discount</span>
                <span class="value" style="color: var(--erp-color-error-text);">-{{ o.currency }} {{ o.discountTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span class="label">Tax</span>
                <span class="value">{{ o.currency }} {{ o.taxTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row total-row">
                <span class="label">Order Total</span>
                <span class="value">{{ o.currency }} {{ o.totalAmount | number:'1.2-2' }}</span>
              </div>

              <!-- Fulfillment Overall Meter -->
              <div class="fulfillment-meter">
                <div class="meter-label">
                  <span>Fulfillment Progress</span>
                  <span><strong>{{ fulfillmentPercentage() }}%</strong></span>
                </div>
                <nz-progress [nzPercent]="fulfillmentPercentage()" [nzStatus]="fulfillmentPercentage() === 100 ? 'success' : 'active'"></nz-progress>
              </div>
            </nz-card>
          </div>
        </div>

        <!-- Line Items Table with Fulfillment -->
        <nz-card nzTitle="Order Items & Fulfillment Status" class="items-card" [nzBodyStyle]="{ padding: '0' }">
          <nz-table #itemsTable [nzData]="o.lines" [nzShowPagination]="false">
            <thead>
              <tr>
                <th style="width: 140px;">Item Code</th>
                <th>Description</th>
                <th style="width: 90px; text-align: right;">Ordered</th>
                <th style="width: 90px; text-align: right;">Delivered</th>
                <th style="width: 90px; text-align: right;">Invoiced</th>
                <th style="width: 120px; text-align: right;">Unit Price</th>
                <th style="width: 90px; text-align: right;">Disc %</th>
                <th style="width: 90px; text-align: right;">Tax %</th>
                <th style="width: 140px; text-align: right;">Line Total</th>
              </tr>
            </thead>
            <tbody>
              @for (item of itemsTable.data; track item.id) {
                <tr>
                  <td><strong>{{ item.itemCode || '-' }}</strong></td>
                  <td>{{ item.description }}</td>
                  <td style="text-align: right;">{{ item.quantity | number:'1.0-2' }} {{ item.unitOfMeasure }}</td>
                  <td style="text-align: right;">
                    <span [style.color]="item.deliveredQuantity >= item.quantity ? 'var(--erp-color-primary)' : 'var(--erp-color-warning-text, #d97706)'">
                      {{ item.deliveredQuantity | number:'1.0-2' }}
                    </span>
                  </td>
                  <td style="text-align: right;">{{ item.invoicedQuantity | number:'1.0-2' }}</td>
                  <td style="text-align: right;">{{ item.unitPrice | number:'1.2-2' }}</td>
                  <td style="text-align: right;">{{ item.discountPercent }}%</td>
                  <td style="text-align: right;">{{ item.taxRate }}%</td>
                  <td style="text-align: right; font-weight: 600;">{{ item.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </nz-card>

        <!-- Fulfillment Delivery Modal -->
        <nz-modal
          [(nzVisible)]="fulfillmentModalVisible"
          nzTitle="Record Delivery"
          nzOkText="Record Delivery"
          [nzOkLoading]="saving()"
          (nzOnCancel)="fulfillmentModalVisible.set(false)"
          (nzOnOk)="saveFulfillment()">
          <ng-container *nzModalContent>
            <p style="color: var(--erp-color-text-muted); margin-bottom: 16px;">Enter the total delivered so far for each item.</p>
            <table class="modal-fulfillment-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th style="width: 90px;">Ordered</th>
                  <th style="width: 120px;">Delivered so far</th>
                </tr>
              </thead>
              <tbody>
                @for (line of fulfillmentDraft(); track line.lineId) {
                  <tr>
                    <td>{{ line.description }}</td>
                    <td>{{ line.quantity }}</td>
                    <td>
                      <nz-input-number
                        [(ngModel)]="line.deliveredQuantity"
                        [attr.aria-label]="'Delivered quantity for ' + line.description"
                        [nzMin]="line.alreadyDelivered"
                        [nzMax]="line.quantity"
                        style="width: 100%;">
                      </nz-input-number>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </ng-container>
        </nz-modal>
      } @else if (failed()) {
        <erp-state-panel kind="error" title="This sales order could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="reload()" />
      } @else {
        <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" />
      }
    </div>
  `,
  styles: [`
    .order-detail-container { max-width: 1400px; margin: 0 auto; }
    .header-nav { margin-bottom: 12px; }
    .back-link { color: var(--erp-color-text-muted); font-size: 14px; display: inline-flex; align-items: center; gap: 6px; }
    .back-link:hover { color: var(--erp-color-primary); }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 24px; }
    .number-tag { display: flex; align-items: center; gap: 12px; }
    .order-number { font-size: 26px; font-weight: 700; color: var(--erp-color-text-primary); margin: 0; }
    .customer-subtitle { color: var(--erp-color-text-muted); font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; justify-content: flex-end; }
    .info-card, .summary-card, .items-card { border-radius: 12px; border: 1px solid var(--erp-color-border-subtle); }
    .note-box { background: var(--erp-color-bg-subtle); border-inline-start: 3px solid var(--erp-color-primary); padding: 10px 14px; border-start-end-radius: 6px; border-end-end-radius: 6px; font-size: 13px; }
    .note-box p { margin: 4px 0 0 0; color: var(--erp-color-text-secondary); }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--erp-color-border-subtle); font-size: 14px; color: var(--erp-color-text-secondary); }
    .total-row { border-top: 2px solid var(--erp-color-border-subtle); border-bottom: none; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: var(--erp-color-text-primary); }
    .fulfillment-meter { margin-top: 20px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
    .meter-label { display: flex; justify-content: space-between; font-size: 13px; color: var(--erp-color-text-secondary); margin-bottom: 6px; }
    .modal-fulfillment-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .modal-fulfillment-table th { background: var(--erp-color-bg-subtle); padding: 8px; border: 1px solid var(--erp-color-border-subtle); text-align: left; }
    .modal-fulfillment-table td { padding: 6px; border: 1px solid var(--erp-color-border-subtle); vertical-align: middle; }
  `],
})
export class OrderDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly message = inject(NzMessageService);

  readonly order = signal<OrderDto | null>(null);
  readonly fulfillmentModalVisible = signal(false);
  readonly fulfillmentDraft = signal<{ lineId: string; description: string; quantity: number; alreadyDelivered: number; deliveredQuantity: number }[]>([]);
  readonly failed = signal(false);
  readonly saving = signal(false);
  private orderId: string | null = null;

  readonly fulfillmentPercentage = () => {
    const o = this.order();
    if (!o || o.lines.length === 0) return 0;
    const totalOrdered = o.lines.reduce((acc, l) => acc + l.quantity, 0);
    const totalDelivered = o.lines.reduce((acc, l) => acc + l.deliveredQuantity, 0);
    if (totalOrdered <= 0) return 0;
    return Math.min(100, Math.round((totalDelivered / totalOrdered) * 100));
  };

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadOrder(id);
    });
  }

  reload(): void {
    if (this.orderId) this.loadOrder(this.orderId);
  }

  loadOrder(id: string): void {
    this.orderId = id;
    this.failed.set(false);
    this.ordersApi.get(id).subscribe({
      next: (o) => this.order.set(o),
      error: () => this.failed.set(true),
    });
  }

  changeStatus(status: SalesOrderStatus): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.setStatus(o.id, status).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.message.success(`Status changed to ${displayLabel(status).toLowerCase()}.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to update status')),
    });
  }

  openFulfillmentModal(): void {
    const o = this.order();
    if (!o) return;
    this.fulfillmentDraft.set(
      o.lines.map((l) => ({
        lineId: l.id,
        description: l.description,
        quantity: l.quantity,
        alreadyDelivered: l.deliveredQuantity,
        deliveredQuantity: l.quantity,
      }))
    );
    this.fulfillmentModalVisible.set(true);
  }

  saveFulfillment(): void {
    const o = this.order();
    if (!o) return;
    const lines: OrderFulfillmentLineInput[] = this.fulfillmentDraft().map((d) => ({
      lineId: d.lineId,
      deliveredQuantity: d.deliveredQuantity,
    }));
    this.saving.set(true);
    this.ordersApi.updateFulfillment(o.id, lines).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.order.set(updated);
        this.fulfillmentModalVisible.set(false);
        this.message.success('Delivery recorded.');
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(problemMessage(err, 'Failed to update delivery'));
      },
    });
  }

  convertToInvoice(): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.convertToInvoice(o.id).subscribe({
      next: () => {
        this.message.success('Invoice generated successfully in Accounting');
        this.loadOrder(o.id);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to generate invoice')),
    });
  }

}
