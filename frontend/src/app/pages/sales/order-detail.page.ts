import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { OrderDto, OrderFulfillmentLineInput, SalesOrdersApi, SalesOrderStatus } from '../../core/api/sales.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

@Component({
  selector: 'erp-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
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
              <nz-tag [nzColor]="statusColor(o.status)">{{ o.status }}</nz-tag>
              @if (o.invoicedInvoiceId) {
                <nz-tag nzColor="green"><span nz-icon nzType="check"></span> Invoiced</nz-tag>
              }
            </div>
            <p class="customer-subtitle">Customer: <strong>{{ o.customerName }}</strong></p>
          </div>

          <div class="header-actions">
            @if (o.status === 'Draft') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" (click)="changeStatus('Confirmed')">
                <span nz-icon nzType="check"></span>Confirm Order
              </button>
            }
            @if (o.status === 'Confirmed' || o.status === 'Processing') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button (click)="openFulfillmentModal()">
                <span nz-icon nzType="send"></span>Record Delivery
              </button>
            }
            @if (!o.invoicedInvoiceId && (o.status === 'Confirmed' || o.status === 'Processing' || o.status === 'Delivered')) {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" (click)="convertToInvoice()">
                <span nz-icon nzType="file-done"></span>Generate Invoice
              </button>
            }
            @if (o.invoicedInvoiceId) {
              <a [routerLink]="['/finance/invoices', o.invoicedInvoiceId]" nz-button nzType="dashed">
                <span nz-icon nzType="file-text"></span>View Accounting Invoice
              </a>
            }
            <a [href]="'/print/order/' + o.id" target="_blank" nz-button data-testid="order-print">
              <span nz-icon nzType="printer"></span>Print / PDF
            </a>
            @if (o.status !== 'Cancelled' && o.status !== 'Invoiced') {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzDanger
                      nz-popconfirm nzPopconfirmTitle="Cancel this order?" (nzOnConfirm)="changeStatus('Cancelled')">
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
                <nz-descriptions-item nzTitle="Order Date">{{ o.orderDate }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Delivery Due">{{ o.expectedDeliveryDate || 'Standard' }}</nz-descriptions-item>
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
                <span class="value" style="color: #ef4444;">-{{ o.currency }} {{ o.discountTotal | number:'1.2-2' }}</span>
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
                    <span [style.color]="item.deliveredQuantity >= item.quantity ? '#10955d' : '#d97706'">
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
          nzTitle="Record Goods Delivery"
          (nzOnCancel)="fulfillmentModalVisible.set(false)"
          (nzOnOk)="saveFulfillment()">
          <ng-container *nzModalContent>
            <p style="color: #64748b; margin-bottom: 16px;">Update quantities dispatched/delivered to client:</p>
            <table class="modal-fulfillment-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th style="width: 90px;">Ordered</th>
                  <th style="width: 120px;">Delivered Qty</th>
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
                        [nzMin]="0"
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
      }
    </div>
  `,
  styles: [`
    .order-detail-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .header-nav { margin-bottom: 12px; }
    .back-link { color: #64748b; font-size: 14px; display: inline-flex; align-items: center; gap: 6px; }
    .back-link:hover { color: #10955d; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .number-tag { display: flex; align-items: center; gap: 12px; }
    .order-number { font-size: 26px; font-weight: 700; color: #0f172a; margin: 0; }
    .customer-subtitle { color: #64748b; font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; align-items: center; }
    .info-card, .summary-card, .items-card { border-radius: 12px; border: 1px solid #e2e8f0; }
    .note-box { background: #f8fafc; border-left: 3px solid #10955d; padding: 10px 14px; border-radius: 0 6px 6px 0; font-size: 13px; }
    .note-box p { margin: 4px 0 0 0; color: #475569; }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-size: 14px; color: #475569; }
    .total-row { border-top: 2px solid #e2e8f0; border-bottom: none; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: #0f172a; }
    .fulfillment-meter { margin-top: 20px; border-top: 1px solid #f1f5f9; padding-top: 16px; }
    .meter-label { display: flex; justify-content: space-between; font-size: 13px; color: #475569; margin-bottom: 6px; }
    .modal-fulfillment-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .modal-fulfillment-table th { background: #f8fafc; padding: 8px; border: 1px solid #e2e8f0; text-align: left; }
    .modal-fulfillment-table td { padding: 6px; border: 1px solid #e2e8f0; vertical-align: middle; }
  `],
})
export class OrderDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly message = inject(NzMessageService);

  readonly order = signal<OrderDto | null>(null);
  readonly fulfillmentModalVisible = signal(false);
  readonly fulfillmentDraft = signal<{ lineId: string; description: string; quantity: number; deliveredQuantity: number }[]>([]);

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

  loadOrder(id: string): void {
    this.ordersApi.get(id).subscribe({
      next: (o) => this.order.set(o),
      error: () => this.message.error('Failed to load sales order'),
    });
  }

  changeStatus(status: SalesOrderStatus): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.setStatus(o.id, status).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.message.success(`Status updated to ${status}`);
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to update status'),
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
        deliveredQuantity: l.deliveredQuantity || l.quantity,
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
    this.ordersApi.updateFulfillment(o.id, lines).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.fulfillmentModalVisible.set(false);
        this.message.success('Delivery quantities updated successfully');
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to update delivery'),
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
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to generate invoice'),
    });
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
}
