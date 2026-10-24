import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { QuoteDto, SalesQuotesApi, SalesQuoteStatus } from '../../core/api/sales.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

@Component({
  selector: 'erp-quote-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzTableModule,
    NzDescriptionsModule,
    NzPopconfirmModule,
    HasPermissionDirective,
  ],
  template: `
    <div class="quote-detail-container">
      @if (quote(); as q) {
        <div class="header-nav">
          <a routerLink="/sales/quotes" class="back-link">
            <span nz-icon nzType="arrow-left"></span> Back to Quotes
          </a>
        </div>

        <div class="page-header">
          <div class="title-section">
            <div class="number-tag">
              <h1 class="quote-number">{{ q.number }}</h1>
              <nz-tag [nzColor]="statusColor(q.status)">{{ q.status }}</nz-tag>
            </div>
            <p class="customer-subtitle">Quotation for {{ q.customerName }}</p>
          </div>

          <div class="header-actions">
            @if (q.status === 'Draft') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button (click)="changeStatus('Sent')">
                <span nz-icon nzType="mail"></span>Mark Sent
              </button>
            }
            @if (q.status === 'Sent' || q.status === 'Draft') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button nzType="primary" (click)="changeStatus('Accepted')">
                <span nz-icon nzType="check"></span>Accept Quote
              </button>
              <button *erpHasPermission="'sales.quotes.manage'" nz-button nzDanger (click)="changeStatus('Declined')">
                <span nz-icon nzType="close"></span>Decline
              </button>
            }
            @if (q.status === 'Accepted' && !q.convertedSalesOrderId) {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" (click)="convertToOrder()">
                <span nz-icon nzType="shopping-cart"></span>Convert to Sales Order
              </button>
            }
            @if (q.convertedSalesOrderId) {
              <a [routerLink]="['/sales/orders', q.convertedSalesOrderId]" nz-button nzType="dashed">
                <span nz-icon nzType="link"></span>View Sales Order
              </a>
            }
          </div>
        </div>

        <!-- Overview Information -->
        <div nz-row [nzGutter]="16" style="margin-bottom: 24px;">
          <div nz-col [nzXs]="24" [nzLg]="16">
            <nz-card nzTitle="Quotation Details" class="info-card">
              <nz-descriptions [nzColumn]="{ xxl: 3, xl: 3, lg: 2, md: 2, sm: 1, xs: 1 }">
                <nz-descriptions-item nzTitle="Customer">{{ q.customerName }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Email">{{ q.customerEmail || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Phone">{{ q.customerPhone || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Quote Date">{{ q.quoteDate }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Valid Until">{{ q.validUntil || 'Open' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Currency">{{ q.currency }}</nz-descriptions-item>
              </nz-descriptions>

              @if (q.terms) {
                <div class="note-box" style="margin-top: 16px;">
                  <strong>Payment & Delivery Terms:</strong>
                  <p>{{ q.terms }}</p>
                </div>
              }
              @if (q.notes) {
                <div class="note-box" style="margin-top: 8px;">
                  <strong>Internal Notes:</strong>
                  <p>{{ q.notes }}</p>
                </div>
              }
            </nz-card>
          </div>

          <div nz-col [nzXs]="24" [nzLg]="8">
            <nz-card nzTitle="Financial Summary" class="summary-card">
              <div class="summary-row">
                <span class="label">Subtotal</span>
                <span class="value">{{ q.currency }} {{ q.subTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span class="label">Discount</span>
                <span class="value" style="color: #ef4444;">-{{ q.currency }} {{ q.discountTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span class="label">Tax Total</span>
                <span class="value">{{ q.currency }} {{ q.taxTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row total-row">
                <span class="label">Total Amount</span>
                <span class="value">{{ q.currency }} {{ q.totalAmount | number:'1.2-2' }}</span>
              </div>
            </nz-card>
          </div>
        </div>

        <!-- Line Items Table -->
        <nz-card nzTitle="Quotation Items" class="items-card" [nzBodyStyle]="{ padding: '0' }">
          <nz-table #itemsTable [nzData]="q.lines" [nzShowPagination]="false">
            <thead>
              <tr>
                <th style="width: 140px;">Item Code</th>
                <th>Description</th>
                <th style="width: 90px; text-align: right;">Quantity</th>
                <th style="width: 80px;">UoM</th>
                <th style="width: 130px; text-align: right;">Unit Price</th>
                <th style="width: 100px; text-align: right;">Disc %</th>
                <th style="width: 100px; text-align: right;">Tax %</th>
                <th style="width: 150px; text-align: right;">Line Total</th>
              </tr>
            </thead>
            <tbody>
              @for (item of itemsTable.data; track item.id) {
                <tr>
                  <td><strong>{{ item.itemCode || '-' }}</strong></td>
                  <td>{{ item.description }}</td>
                  <td style="text-align: right;">{{ item.quantity | number:'1.0-2' }}</td>
                  <td>{{ item.unitOfMeasure }}</td>
                  <td style="text-align: right;">{{ item.unitPrice | number:'1.2-2' }}</td>
                  <td style="text-align: right;">{{ item.discountPercent }}%</td>
                  <td style="text-align: right;">{{ item.taxRate }}%</td>
                  <td style="text-align: right; font-weight: 600;">{{ item.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </nz-card>
      }
    </div>
  `,
  styles: [`
    .quote-detail-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .header-nav { margin-bottom: 12px; }
    .back-link { color: #64748b; font-size: 14px; display: inline-flex; align-items: center; gap: 6px; }
    .back-link:hover { color: #10955d; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .number-tag { display: flex; align-items: center; gap: 12px; }
    .quote-number { font-size: 26px; font-weight: 700; color: #0f172a; margin: 0; }
    .customer-subtitle { color: #64748b; font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; align-items: center; }
    .info-card, .summary-card, .items-card { border-radius: 12px; border: 1px solid #e2e8f0; }
    .note-box { background: #f8fafc; border-left: 3px solid #10955d; padding: 10px 14px; border-radius: 0 6px 6px 0; font-size: 13px; }
    .note-box p { margin: 4px 0 0 0; color: #475569; }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-size: 14px; color: #475569; }
    .total-row { border-top: 2px solid #e2e8f0; border-bottom: none; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: #0f172a; }
  `],
})
export class QuoteDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly quotesApi = inject(SalesQuotesApi);
  private readonly message = inject(NzMessageService);

  readonly quote = signal<QuoteDto | null>(null);

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadQuote(id);
    });
  }

  loadQuote(id: string): void {
    this.quotesApi.get(id).subscribe({
      next: (q) => this.quote.set(q),
      error: () => this.message.error('Failed to load quote details'),
    });
  }

  changeStatus(status: SalesQuoteStatus): void {
    const q = this.quote();
    if (!q) return;
    this.quotesApi.setStatus(q.id, status).subscribe({
      next: (updated) => {
        this.quote.set(updated);
        this.message.success(`Status updated to ${status}`);
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to update status'),
    });
  }

  convertToOrder(): void {
    const q = this.quote();
    if (!q) return;
    this.quotesApi.convertToOrder(q.id).subscribe({
      next: (order) => {
        this.message.success(`Converted into Sales Order ${order.number}`);
        this.loadQuote(q.id);
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to convert quote'),
    });
  }

  statusColor(status: string): string {
    switch (status) {
      case 'Accepted':
      case 'Converted': return 'green';
      case 'Sent': return 'blue';
      case 'Declined':
      case 'Expired': return 'red';
      default: return 'default';
    }
  }
}
