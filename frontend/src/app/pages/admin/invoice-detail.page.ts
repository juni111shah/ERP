import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { API_BASE_URL } from '../../core/api/api-config';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

interface InvoiceLine {
  id: string;
  seq: number;
  itemId: string | null;
  description: string;
  quantity: number;
  price: number;
  taxCodeId: string | null;
  taxAmount: number;
  net: number;
  accountId: string | null;
}

interface Invoice {
  id: string;
  number: string;
  legalEntityId: string;
  partyId: string;
  partyName: string;
  invoiceDate: string;
  dueDate: string;
  isCreditNote: boolean;
  creditNoteForId: string | null;
  currencyCode: string;
  net: number;
  tax: number;
  total: number;
  paid: number;
  status: string;
  approvalRequestId: string | null;
  journalEntryId: string | null;
  notes: string | null;
  lines: InvoiceLine[];
}

@Component({
  selector: 'erp-invoice-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe, CurrencyPipe, RouterLink,
    NzButtonModule, NzCardModule, NzDescriptionsModule, NzGridModule,
    NzIconModule, NzStatisticModule, NzTableModule, NzTabsModule, NzTagModule,
    HasPermissionDirective
  ],
  styles: [`
    .erp-page-title {
      font-size: 24px;
      font-weight: 500;
      margin: 0 0 16px;
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .status-tag {
      font-size: 12px;
      padding: 2px 8px;
      border-radius: 4px;
      text-transform: uppercase;
    }
    .status-draft { background: #f0f0f0; color: #666; }
    .status-approved { background: #e6f7ff; color: #1890ff; }
    .status-issued { background: #f6ffed; color: #52c41a; }
    .status-partial { background: #fff7e6; color: #fa8c16; }
    .status-paid { background: #f6ffed; color: #52c41a; }
    .status-overdue { background: #fff1f0; color: #f5222d; }
    .status-voided { background: #f5f5f5; color: #999; }
    .action-bar {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }
    .line-items-table {
      margin-top: 16px;
    }
    .totals-section {
      margin-top: 16px;
      text-align: right;
    }
    .total-row {
      display: flex;
      justify-content: flex-end;
      gap: 100px;
      padding: 8px 0;
      font-size: 14px;
    }
    .total-row.grand {
      border-top: 2px solid #1890ff;
      padding-top: 12px;
      margin-top: 8px;
      font-size: 16px;
      font-weight: 600;
    }
    .history-timeline {
      padding: 16px;
    }
    .history-item {
      padding: 12px 0;
      border-bottom: 1px solid #f0f0f0;
    }
    .history-item:last-child {
      border-bottom: none;
    }
    .history-timestamp {
      font-size: 12px;
      color: #999;
    }
  `],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (invoice() !== null) {
      @if (invoice(); as inv) {
      <p><a routerLink="/admin/invoices">← Back to Invoices</a></p>
      
      <h1 class="erp-page-title">
        {{ inv.isCreditNote ? 'Credit Note' : 'Invoice' }} #{{ inv.number }}
        <span class="status-tag" [class]="'status-' + inv.status.toLowerCase()">
          {{ inv.status }}
        </span>
      </h1>

      <div class="action-bar">
        @if (inv.status === 'issued' || inv.status === 'partial') {
          <button *erpHasPermission="'core.receipts.create'" nz-button nzType="primary" (click)="recordPayment(inv)">
            <span nz-icon nzType="dollar"></span> Record Payment
          </button>
        }
        @if (!inv.isCreditNote && (inv.status === 'issued' || inv.status === 'partial' || inv.status === 'paid')) {
          <button *erpHasPermission="'core.invoices.create'" nz-button (click)="createCreditNote(inv)">
            <span nz-icon nzType="file-add"></span> Credit Note
          </button>
        }
        @if (inv.status !== 'voided' && inv.status !== 'paid') {
          <button *erpHasPermission="'core.invoices.edit'" nz-button nzDanger (click)="voidInvoice(inv)">
            <span nz-icon nzType="stop"></span> Void
          </button>
        }
        <button *erpHasPermission="'core.invoices.create'" nz-button (click)="duplicate(inv)">
          <span nz-icon nzType="copy"></span> Duplicate
        </button>
        @if (inv.journalEntryId) {
          <button nz-button (click)="viewJournal(inv.journalEntryId)">
            <span nz-icon nzType="book"></span> View Journal
          </button>
        }
        <button nz-button (click)="print(inv.id)">
          <span nz-icon nzType="printer"></span> Print
        </button>
        <button nz-button (click)="email(inv)">
          <span nz-icon nzType="mail"></span> Email
        </button>
      </div>

      <div nz-row [nzGutter]="[16, 16]">
        <div nz-col [nzXs]="24" [nzMd]="6">
          <nz-card>
            <nz-statistic 
              nzTitle="Total Amount"
              [nzValue]="inv.total"
              [nzSuffix]="inv.currencyCode">
            </nz-statistic>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzMd]="6">
          <nz-card>
            <nz-statistic 
              nzTitle="Amount Paid"
              [nzValue]="inv.paid"
              [nzSuffix]="inv.currencyCode">
            </nz-statistic>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzMd]="6">
          <nz-card>
            <nz-statistic 
              nzTitle="Balance Due"
              [nzValue]="balance()"
              [nzSuffix]="inv.currencyCode"
              [nzValueStyle]="{ color: balance() > 0 ? '#ff4d4f' : '#52c41a' }">
            </nz-statistic>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzMd]="6">
          <nz-card>
            <nz-statistic 
              nzTitle="Days Overdue"
              [nzValue]="daysOverdue() || 0"
              [nzValueStyle]="{ color: daysOverdue() > 0 ? '#ff4d4f' : '#52c41a' }">
            </nz-statistic>
          </nz-card>
        </div>
      </div>

      <nz-card style="margin-top: 16px;" nzTitle="Details">
        <nz-descriptions nzBordered [nzColumn]="2">
          <nz-descriptions-item nzTitle="Customer">
            <a [routerLink]="['/admin/parties', inv.partyId]">{{ inv.partyName }}</a>
          </nz-descriptions-item>
          <nz-descriptions-item nzTitle="Invoice Date">
            {{ inv.invoiceDate | date:'dd MMM yyyy' }}
          </nz-descriptions-item>
          <nz-descriptions-item nzTitle="Due Date">
            {{ inv.dueDate | date:'dd MMM yyyy' }}
          </nz-descriptions-item>
          <nz-descriptions-item nzTitle="Currency">
            {{ inv.currencyCode }}
          </nz-descriptions-item>
          @if (inv.creditNoteForId) {
            <nz-descriptions-item nzTitle="Credit Note For">
              <a [routerLink]="['/admin/invoices', inv.creditNoteForId]">View original invoice</a>
            </nz-descriptions-item>
          }
          @if (inv.notes) {
            <nz-descriptions-item nzTitle="Notes" [nzSpan]="2">
              {{ inv.notes }}
            </nz-descriptions-item>
          }
        </nz-descriptions>
      </nz-card>

      <div class="line-items-table">
        <nz-table #itemsTable [nzData]="inv.lines" [nzFrontPagination]="false" nzSize="small">
          <thead>
            <tr>
              <th style="width: 40px;">#</th>
              <th>Description</th>
              <th style="width: 100px; text-align: right;">Quantity</th>
              <th style="width: 120px; text-align: right;">Price</th>
              <th style="width: 120px; text-align: right;">Tax</th>
              <th style="width: 140px; text-align: right;">Net Amount</th>
            </tr>
          </thead>
          <tbody>
            @for (line of itemsTable.data; track line.id) {
              <tr>
                <td>{{ line.seq }}</td>
                <td>{{ line.description }}</td>
                <td style="text-align: right;">{{ line.quantity }}</td>
                <td style="text-align: right;">{{ line.price | currency:inv.currencyCode }}</td>
                <td style="text-align: right;">{{ line.taxAmount | currency:inv.currencyCode }}</td>
                <td style="text-align: right;">{{ line.net | currency:inv.currencyCode }}</td>
              </tr>
            }
          </tbody>
        </nz-table>
      </div>

      <div class="totals-section">
        <div class="total-row">
          <span>Subtotal:</span>
          <span>{{ inv.net | currency:inv.currencyCode }}</span>
        </div>
        <div class="total-row">
          <span>Tax:</span>
          <span>{{ inv.tax | currency:inv.currencyCode }}</span>
        </div>
        <div class="total-row grand">
          <span>Total:</span>
          <span>{{ inv.total | currency:inv.currencyCode }}</span>
        </div>
      </div>

      <nz-card style="margin-top: 24px;">
        <nz-tabset>
          <nz-tab nzTitle="Documents">
            <div style="padding: 16px;">
              <p><i>Document attachments will be implemented here</i></p>
            </div>
          </nz-tab>
          <nz-tab nzTitle="History">
            <div class="history-timeline">
              <div class="history-item">
                <div class="history-timestamp">{{ inv.invoiceDate | date:'dd MMM yyyy, h:mm a' }}</div>
                <div>Invoice created</div>
              </div>
              @if (inv.status === 'paid') {
                <div class="history-item">
                  <div class="history-timestamp">Recent</div>
                  <div>Invoice marked as paid</div>
                </div>
              }
            </div>
          </nz-tab>
        </nz-tabset>
      </nz-card>
      }
    }
  `
})
export class InvoiceDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly apiBase = inject(API_BASE_URL);

  loading = signal(true);
  error = signal<string | null>(null);
  invoice = signal<Invoice | null>(null);

  balance = computed(() => {
    const inv = this.invoice();
    return inv ? inv.total - inv.paid : 0;
  });

  daysOverdue = computed(() => {
    const inv = this.invoice();
    if (!inv || inv.status === 'paid' || inv.status === 'voided') return 0;
    const due = new Date(inv.dueDate);
    const today = new Date();
    const diff = Math.floor((today.getTime() - due.getTime()) / (1000 * 60 * 60 * 24));
    return diff > 0 ? diff : 0;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid invoice ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/invoices/${id}`).subscribe({
      next: (response) => {
        this.invoice.set(response.data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load invoice');
        this.loading.set(false);
      }
    });
  }

  recordPayment(inv: Invoice): void {
    this.message.info('Record payment functionality will be implemented');
  }

  createCreditNote(inv: Invoice): void {
    this.message.info('Create credit note functionality will be implemented');
  }

  voidInvoice(inv: Invoice): void {
    this.modal.confirm({
      nzTitle: 'Void this invoice?',
      nzContent: 'This action cannot be undone. The invoice will be marked as voided.',
      nzOkText: 'Void Invoice',
      nzOkDanger: true,
      nzOnOk: () => {
        this.message.info('Void functionality will be implemented');
      }
    });
  }

  duplicate(inv: Invoice): void {
    this.message.info('Duplicate functionality will be implemented');
  }

  viewJournal(journalId: string): void {
    this.router.navigate(['/admin/journals', journalId]);
  }

  print(id: string): void {
    window.open(`/print/invoice/${id}`, '_blank');
  }

  email(inv: Invoice): void {
    this.message.info('Email functionality will be implemented');
  }
}
