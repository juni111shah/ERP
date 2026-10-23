import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { API_BASE_URL } from '../../core/api/api-config';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';

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
  imports: [CommonModule, DatePipe, CurrencyPipe, NzTableModule, DetailPageComponent],
  styles: [`
    .line-items-table { margin-top: 16px; }
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
  `],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (invoice()) {
      <erp-detail-page
        [backLink]="['/finance/invoices']"
        [backLabel]="'Back to Invoices'"
        [title]="(invoice()!.isCreditNote ? 'Credit Note' : 'Invoice') + ' #' + invoice()!.number"
        [status]="invoice()!.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="invoice()!.invoiceDate"
        [entityName]="invoice()!.isCreditNote ? 'Credit Note' : 'Invoice'">
        
        <div content class="line-items-table">
          <nz-table #itemsTable [nzData]="invoice()!.lines" [nzFrontPagination]="false" nzSize="small">
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
                  <td style="text-align: right;">{{ line.price | currency:invoice()!.currencyCode }}</td>
                  <td style="text-align: right;">{{ line.taxAmount | currency:invoice()!.currencyCode }}</td>
                  <td style="text-align: right;">{{ line.net | currency:invoice()!.currencyCode }}</td>
                </tr>
              }
            </tbody>
          </nz-table>

          <div class="totals-section">
            <div class="total-row">
              <span>Subtotal:</span>
              <span>{{ invoice()!.net | currency:invoice()!.currencyCode }}</span>
            </div>
            <div class="total-row">
              <span>Tax:</span>
              <span>{{ invoice()!.tax | currency:invoice()!.currencyCode }}</span>
            </div>
            <div class="total-row grand">
              <span>Total:</span>
              <span>{{ invoice()!.total | currency:invoice()!.currencyCode }}</span>
            </div>
          </div>
        </div>
      </erp-detail-page>
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

  actions = computed((): ActionButton[] => {
    const inv = this.invoice();
    if (!inv) return [];
    
    return [
      {
        label: 'Record Payment',
        icon: 'dollar',
        type: 'primary',
        permission: 'core.receipts.create',
        visible: inv.status === 'issued' || inv.status === 'partial',
        handler: () => this.recordPayment()
      },
      {
        label: 'Credit Note',
        icon: 'file-add',
        permission: 'core.invoices.create',
        visible: !inv.isCreditNote && (inv.status === 'issued' || inv.status === 'partial' || inv.status === 'paid'),
        handler: () => this.createCreditNote()
      },
      {
        label: 'Void',
        icon: 'stop',
        danger: true,
        permission: 'core.invoices.edit',
        visible: inv.status !== 'voided' && inv.status !== 'paid',
        handler: () => this.voidInvoice()
      },
      {
        label: 'Duplicate',
        icon: 'copy',
        permission: 'core.invoices.create',
        handler: () => this.duplicate()
      },
      {
        label: 'View Journal',
        icon: 'book',
        visible: !!inv.journalEntryId,
        handler: () => this.viewJournal(inv.journalEntryId!)
      },
      {
        label: 'Print',
        icon: 'printer',
        handler: () => this.print(inv.id)
      },
      {
        label: 'Email',
        icon: 'mail',
        handler: () => this.email()
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const inv = this.invoice();
    if (!inv) return [];
    
    return [
      {
        title: 'Total Amount',
        value: inv.total,
        suffix: inv.currencyCode
      },
      {
        title: 'Amount Paid',
        value: inv.paid,
        suffix: inv.currencyCode
      },
      {
        title: 'Balance Due',
        value: this.balance(),
        suffix: inv.currencyCode,
        valueStyle: { color: this.balance() > 0 ? '#ff4d4f' : '#52c41a' }
      },
      {
        title: 'Days Overdue',
        value: this.daysOverdue() || 0,
        valueStyle: { color: this.daysOverdue() > 0 ? '#ff4d4f' : '#52c41a' }
      }
    ];
  });

  descriptions = computed((): DescriptionItem[] => {
    const inv = this.invoice();
    if (!inv) return [];
    
    const items: DescriptionItem[] = [
      { label: 'Customer', value: inv.partyName, routerLink: ['/parties', inv.partyId] },
      { label: 'Invoice Date', value: new Date(inv.invoiceDate).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Due Date', value: new Date(inv.dueDate).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Currency', value: inv.currencyCode }
    ];

    if (inv.creditNoteForId) {
      items.push({ 
        label: 'Credit Note For', 
        value: 'View original invoice',
        routerLink: ['/finance/invoices', inv.creditNoteForId]
      });
    }

    if (inv.notes) {
      items.push({ label: 'Notes', value: inv.notes, span: 2 });
    }

    return items;
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

  recordPayment(): void {
    this.message.info('Record payment functionality will be implemented');
  }

  createCreditNote(): void {
    this.message.info('Create credit note functionality will be implemented');
  }

  voidInvoice(): void {
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

  duplicate(): void {
    this.message.info('Duplicate functionality will be implemented');
  }

  viewJournal(journalId: string): void {
    this.router.navigate(['/finance/journals', journalId]);
  }

  print(id: string): void {
    window.open(`/print/invoice/${id}`, '_blank');
  }

  email(): void {
    this.message.info('Email functionality will be implemented');
  }
}
