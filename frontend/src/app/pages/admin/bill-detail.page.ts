import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { API_BASE_URL } from '../../core/api/api-config';
import { BillsApi } from '../../core/api/admin.api';
import { askReason } from '../../shared/reason-prompt';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';

interface BillLine {
  id: string;
  seq: number;
  description: string;
  quantity: number;
  price: number;
  taxCodeId: string | null;
  taxAmount: number;
  taxRecoverable: number;
  net: number;
  accountId: string | null;
}

interface Bill {
  id: string;
  number: string;
  legalEntityId: string;
  partyId: string;
  partyName: string;
  vendorReference: string | null;
  billDate: string;
  dueDate: string;
  currencyCode: string;
  net: number;
  tax: number;
  total: number;
  paid: number;
  status: string;
  approvalRequestId: string | null;
  journalEntryId: string | null;
  notes: string | null;
  lines: BillLine[];
}

@Component({
  selector: 'erp-bill-detail',
  standalone: true,
  imports: [CommonModule, DatePipe, CurrencyPipe, NzTableModule, NzModalModule, DetailPageComponent],
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
    } @else if (bill()) {
      <erp-detail-page
        [backLink]="['/finance/bills']"
        [backLabel]="'Back to Bills'"
        [title]="'Bill #' + bill()!.number"
        [status]="bill()!.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="bill()!.billDate"
        [entityName]="'Bill'">
        
        <div content class="line-items-table">
          <nz-table #itemsTable [nzData]="bill()!.lines" [nzFrontPagination]="false" nzSize="small">
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
                  <td style="text-align: right;">{{ line.price | currency:bill()!.currencyCode }}</td>
                  <td style="text-align: right;">{{ line.taxAmount | currency:bill()!.currencyCode }}</td>
                  <td style="text-align: right;">{{ line.net | currency:bill()!.currencyCode }}</td>
                </tr>
              }
            </tbody>
          </nz-table>

          <div class="totals-section">
            <div class="total-row">
              <span>Subtotal:</span>
              <span>{{ bill()!.net | currency:bill()!.currencyCode }}</span>
            </div>
            <div class="total-row">
              <span>Tax:</span>
              <span>{{ bill()!.tax | currency:bill()!.currencyCode }}</span>
            </div>
            <div class="total-row grand">
              <span>Total:</span>
              <span>{{ bill()!.total | currency:bill()!.currencyCode }}</span>
            </div>
          </div>
        </div>
      </erp-detail-page>
    }
  `
})
export class BillDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly api = inject(BillsApi);

  loading = signal(true);
  error = signal<string | null>(null);
  bill = signal<Bill | null>(null);

  balance = computed(() => {
    const bil = this.bill();
    return bil ? bil.total - bil.paid : 0;
  });

  daysOverdue = computed(() => {
    const bil = this.bill();
    if (!bil || bil.status === 'paid' || bil.status === 'voided') return 0;
    const due = new Date(bil.dueDate);
    const today = new Date();
    const diff = Math.floor((today.getTime() - due.getTime()) / (1000 * 60 * 60 * 24));
    return diff > 0 ? diff : 0;
  });

  actions = computed((): ActionButton[] => {
    const bil = this.bill();
    if (!bil) return [];
    
    return [
      {
        label: 'Make Payment',
        icon: 'dollar',
        type: 'primary',
        permission: 'finance.payments.write',
        visible: bil.status === 'issued' || bil.status === 'partlyPaid',
        handler: () => this.router.navigate(['/finance/payments'], { queryParams: { partyId: bil.partyId, billId: bil.id } })
      },
      {
        label: 'Post',
        icon: 'check',
        type: 'primary',
        permission: 'finance.bills.post',
        visible: bil.status === 'draft' || bil.status === 'pendingApproval',
        handler: () => this.postBill()
      },
      {
        label: 'Edit',
        icon: 'edit',
        permission: 'finance.bills.write',
        visible: bil.status === 'draft' || bil.status === 'pendingApproval',
        handler: () => this.router.navigate(['/finance/bills'], { queryParams: { open: bil.id } })
      },
      {
        label: 'Delete',
        icon: 'delete',
        danger: true,
        permission: 'finance.bills.write',
        visible: bil.status === 'draft' || bil.status === 'pendingApproval',
        handler: () => this.deleteBill()
      },
      {
        label: 'Void',
        icon: 'stop',
        danger: true,
        permission: 'finance.bills.post',
        visible: bil.status === 'issued' && bil.paid === 0,
        handler: () => this.voidBill()
      },
      {
        label: 'View Journal',
        icon: 'book',
        permission: 'finance.journals.read',
        visible: !!bil.journalEntryId,
        handler: () => this.router.navigate(['/finance/journals', bil.journalEntryId])
      },
      {
        label: 'Print',
        icon: 'printer',
        handler: () => window.open(`/print/bill/${bil.id}`, '_blank')
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const bil = this.bill();
    if (!bil) return [];
    
    return [
      {
        title: 'Total Amount',
        value: bil.total,
        suffix: bil.currencyCode
      },
      {
        title: 'Amount Paid',
        value: bil.paid,
        suffix: bil.currencyCode
      },
      {
        title: 'Balance Due',
        value: this.balance(),
        suffix: bil.currencyCode,
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
    const bil = this.bill();
    if (!bil) return [];
    
    const items: DescriptionItem[] = [
      { label: 'Vendor', value: bil.partyName, routerLink: ['/parties', bil.partyId] },
      { label: 'Bill Date', value: new Date(bil.billDate).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Due Date', value: new Date(bil.dueDate).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Currency', value: bil.currencyCode }
    ];

    if (bil.status === 'pendingApproval' || bil.approvalRequestId) {
      items.push({ label: 'Approval', value: bil.status === 'pendingApproval' ? 'Pending approval' : 'Approved', routerLink: ['/approvals'] });
    }

    if (bil.vendorReference) {
      items.push({ label: 'Vendor Reference', value: bil.vendorReference });
    }

    if (bil.notes) {
      items.push({ label: 'Notes', value: bil.notes, span: 2 });
    }

    return items;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid bill ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/finance/bills/${id}`).subscribe({
      next: (response) => {
        this.bill.set(response);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load bill');
        this.loading.set(false);
      }
    });
  }

  voidBill(): void {
    const bil = this.bill();
    if (!bil) return;
    // The void endpoint takes no body yet, so the reason is collected but not sent (backend gap, see CHANGES.md).
    askReason(this.modal, 'Void this bill?', 'Void Bill', 'This cannot be undone. The bill will be marked as voided.').then((reason) => {
      if (!reason) return;
      this.api.void(bil.id).subscribe({
        next: (res) => { this.bill.set(res as unknown as Bill); this.message.success('Bill voided'); },
        error: (err) => this.message.error(err?.error?.detail || err?.error?.message || 'Failed to void bill')
      });
    });
  }

  postBill(): void {
    const bil = this.bill();
    if (!bil) return;
    this.api.post(bil.id).subscribe({
      next: (res) => {
        this.bill.set(res as unknown as Bill);
        this.message.success(res.status === 'pendingApproval' ? 'Sent for approval. Post it again once it is approved.' : `Posted as ${res.number}`);
      },
      error: (err) => this.message.error(err?.error?.detail || err?.error?.message || 'Failed to post bill')
    });
  }

  deleteBill(): void {
    const bil = this.bill();
    if (!bil) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft bill?',
      nzOkText: 'Delete',
      nzOkDanger: true,
      nzOnOk: () => this.api.delete(bil.id).subscribe({
        next: () => { this.message.success('Draft deleted'); this.router.navigate(['/finance/bills']); },
        error: (err) => this.message.error(err?.error?.detail || err?.error?.message || 'Failed to delete bill')
      })
    });
  }
}
