import { SuccessToastService } from '../../core/services/success-toast.service';
import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
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
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { QuoteDto, SalesQuotesApi, SalesQuoteStatus } from '../../core/api/sales.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { displayLabel } from '../../core/format/display';

@Component({
  selector: 'erp-quote-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatusTagComponent, NzSkeletonModule, NzToolTipModule,
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzGridModule,
    NzCardModule,
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
              <erp-status [value]="q.status" />
            </div>
            <p class="customer-subtitle">Quotation for {{ q.customerName }}</p>
          </div>

          <div class="header-actions">
            @if (q.status === 'draft') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button (click)="changeStatus('sent')">
                <span nz-icon nzType="mail"></span>Mark Sent
              </button>
            }
            @if (q.status === 'sent' || q.status === 'draft') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button nzType="primary" (click)="changeStatus('accepted')">
                <span nz-icon nzType="check"></span>Accept Quote
              </button>
              <button *erpHasPermission="'sales.quotes.manage'" nz-button nzDanger (click)="changeStatus('declined')">
                <span nz-icon nzType="close"></span>Decline
              </button>
            }
            @if (q.status === 'sent') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button (click)="changeStatus('expired')" data-testid="quote-expire">
                <span nz-icon nzType="clock-circle"></span>Mark Expired
              </button>
            }
            @if (q.status === 'declined' || q.status === 'expired') {
              <button *erpHasPermission="'sales.quotes.manage'" nz-button (click)="changeStatus('draft')" data-testid="quote-reopen">
                <span nz-icon nzType="undo"></span>Back to Draft
              </button>
            }
            @if (q.status === 'accepted' && !q.convertedSalesOrderId) {
              <button *erpHasPermission="'sales.orders.manage'" nz-button nzType="primary" (click)="convertToOrder()">
                <span nz-icon nzType="shopping-cart"></span>Convert to Sales Order
              </button>
            }
            @if (q.convertedSalesOrderId) {
              <a [routerLink]="['/sales/orders', q.convertedSalesOrderId]" nz-button nzType="dashed">
                <span nz-icon nzType="link"></span>View Sales Order
              </a>
            }
            <a [href]="'/print/quote/' + q.id" target="_blank" nz-button data-testid="quote-print">
              <span nz-icon nzType="printer"></span>Print / PDF
            </a>
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
                <nz-descriptions-item nzTitle="Quote Date">{{ q.quoteDate | erpDate }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Valid Until">{{ (q.validUntil | erpDate) || 'No end date' }}</nz-descriptions-item>
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
                <span class="value" style="color: var(--erp-color-error-text);">-{{ q.currency }} {{ q.discountTotal | number:'1.2-2' }}</span>
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
                <th style="width: 90px; text-align: end;">Quantity</th>
                <th style="width: 80px;">UoM</th>
                <th style="width: 130px; text-align: end;">Unit Price</th>
                <th style="width: 100px; text-align: end;">Disc %</th>
                <th style="width: 100px; text-align: end;">Tax %</th>
                <th style="width: 150px; text-align: end;">Line Total</th>
              </tr>
            </thead>
            <tbody>
              @for (item of itemsTable.data; track item.id) {
                <tr>
                  <td><strong>{{ item.itemCode || '-' }}</strong></td>
                  <td>{{ item.description }}</td>
                  <td style="text-align: end;">{{ item.quantity | number:'1.0-2' }}</td>
                  <td>{{ item.unitOfMeasure }}</td>
                  <td style="text-align: end;">{{ item.unitPrice | number:'1.2-2' }}</td>
                  <td style="text-align: end;">{{ item.discountPercent }}%</td>
                  <td style="text-align: end;">{{ item.taxRate }}%</td>
                  <td style="text-align: end; font-weight: 600;">{{ item.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </nz-card>
      } @else if (failed()) {
        <erp-state-panel kind="error" title="This quote could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="reload()" />
      } @else {
        <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" />
      }
    </div>
  `,
  styles: [`
    .quote-detail-container { max-width: 1400px; margin: 0 auto; }
    .header-nav { margin-bottom: 12px; }
    .back-link { color: var(--erp-color-text-muted); font-size: 14px; display: inline-flex; align-items: center; gap: 6px; }
    .back-link:hover { color: var(--erp-color-primary); }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 24px; }
    .number-tag { display: flex; align-items: center; gap: 12px; }
    .quote-number { font-size: 26px; font-weight: 700; color: var(--erp-color-text-primary); margin: 0; }
    .customer-subtitle { color: var(--erp-color-text-muted); font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; justify-content: flex-end; }
    .info-card, .summary-card, .items-card { border-radius: 12px; border: 1px solid var(--erp-color-border-subtle); }
    .note-box { background: var(--erp-color-bg-subtle); border-inline-start: 3px solid var(--erp-color-primary); padding: 10px 14px; border-start-end-radius: 6px; border-end-end-radius: 6px; font-size: 13px; }
    .note-box p { margin: 4px 0 0 0; color: var(--erp-color-text-secondary); }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--erp-color-border-subtle); font-size: 14px; color: var(--erp-color-text-secondary); }
    .total-row { border-top: 2px solid var(--erp-color-border-subtle); border-bottom: none; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: var(--erp-color-text-primary); }
  `],
})
export class QuoteDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly quotesApi = inject(SalesQuotesApi);
  private readonly message = inject(NzMessageService);
  private readonly toast = inject(SuccessToastService);

  readonly quote = signal<QuoteDto | null>(null);
  readonly failed = signal(false);
  private quoteId: string | null = null;

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadQuote(id);
    });
  }

  reload(): void {
    if (this.quoteId) this.loadQuote(this.quoteId);
  }

  loadQuote(id: string): void {
    this.quoteId = id;
    this.failed.set(false);
    this.quotesApi.get(id).subscribe({
      next: (q) => this.quote.set(q),
      error: () => this.failed.set(true),
    });
  }

  changeStatus(status: SalesQuoteStatus): void {
    const q = this.quote();
    if (!q) return;
    this.quotesApi.setStatus(q.id, status).subscribe({
      next: (updated) => {
        this.quote.set(updated);
        this.message.success(`${q.number} is now ${displayLabel(status).toLowerCase()}.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to update status')),
    });
  }

  convertToOrder(): void {
    const q = this.quote();
    if (!q) return;
    this.quotesApi.convertToOrder(q.id).subscribe({
      next: (order) => {
        this.toast.show(`${q.number} converted into sales order ${order.number}`, { label: 'View order', to: ['/sales/orders', order.id] });
        this.loadQuote(q.id);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to convert quote')),
    });
  }

}
