import { EmailDocumentDrawerComponent } from '../../shared/email-document-drawer.component';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { API_BASE_URL } from '../../core/api/api-config';
import { BillsApi } from '../../core/api/admin.api';
import { askReason } from '../../shared/reason-prompt';
import { problemMessage } from '../../core/http/problem-details';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { displayDate } from '../../core/format/display';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { daysOverdue, formatMoney, invoiceStatusLabel } from './invoice-sheet.component';
import { I18nService } from '../../core/i18n/i18n.service';

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
  imports: [NzSpinModule, StatePanelComponent, EmailDocumentDrawerComponent, TranslatePipe, CommonModule, DatePipe, NzTableModule, NzModalModule, DetailPageComponent],
  styles: [`
    .line-items-table { margin-top: 16px; }
    .totals-section {
      margin-top: 16px;
      text-align: end;
    }
    .total-row {
      display: flex;
      justify-content: flex-end;
      gap: 100px;
      padding: 8px 0;
      font-size: 14px;
    }
    .total-row.grand {
      border-top: 2px solid var(--erp-color-border-subtle);
      padding-top: 12px;
      margin-top: 8px;
      font-size: 16px;
      font-weight: 600;
    }
  `],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;"><nz-spin nzSimple></nz-spin></div>
    } @else if (error()) {
      <erp-state-panel kind="error" title="Could not load this bill" [hint]="error()" actionLabel="Try again" (action)="retry()" />
    } @else if (bill()) {
      <erp-detail-page
        [backLink]="['/finance/bills']"
        [backLabel]="'Back to Bills'"
        [title]="('ui.title.billNo' | translate: 'Bill #') + bill()!.number"
        [status]="bill()!.status === 'issued' ? 'posted' : statusLabel(bill()!.status)"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="bill()!.billDate"
        [entityName]="'Bill'"
        [showDocuments]="false"
        recordType="Bill" [recordId]="bill()!.id">
        
        <div content class="line-items-table">
          <nz-table #itemsTable [nzData]="bill()!.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
            <thead>
              <tr>
                <th style="width: 40px;">#</th>
                <th>Description</th>
                <th style="width: 100px; text-align: end;">Quantity</th>
                <th style="width: 120px; text-align: end;">Price</th>
                <th style="width: 120px; text-align: end;">Tax</th>
                <th style="width: 140px; text-align: end;">Net Amount</th>
              </tr>
            </thead>
            <tbody>
              @for (line of itemsTable.data; track line.id) {
                <tr>
                  <td>{{ line.seq }}</td>
                  <td>{{ line.description }}</td>
                  <td style="text-align: end;">{{ line.quantity }}</td>
                  <td style="text-align: end;">{{ money(line.price) }}</td>
                  <td style="text-align: end;">{{ money(line.taxAmount) }}</td>
                  <td style="text-align: end;">{{ money(line.net) }}</td>
                </tr>
              }
            </tbody>
          </nz-table>

          <div class="totals-section">
            <div class="total-row">
              <span>Subtotal:</span>
              <span>{{ money(bill()!.net) }}</span>
            </div>
            <div class="total-row">
              <span>Tax:</span>
              <span>{{ money(bill()!.tax) }}</span>
            </div>
            <div class="total-row grand">
              <span>Total:</span>
              <span>{{ money(bill()!.total) }}</span>
            </div>
          </div>
        </div>
      </erp-detail-page>
      <erp-email-document-drawer [open]="emailOpen()" [path]="'finance/bills/' + bill()!.id + '/email'" (closed)="emailOpen.set(false)" />
    }
  `
})
export class BillDetailPage implements OnInit {
  readonly emailOpen = signal(false);
  private readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly api = inject(BillsApi);

  readonly statusLabel = invoiceStatusLabel;
  money(v: number): string { return formatMoney(v, this.bill()?.currencyCode); }
  loading = signal(true);
  error = signal<string | null>(null);
  bill = signal<Bill | null>(null);

  balance = computed(() => {
    const bil = this.bill();
    return bil && bil.status !== 'voided' ? bil.total - bil.paid : 0;
  });

  daysOverdue = computed(() => {
    const bil = this.bill();
    return bil ? daysOverdue(bil) : 0;
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
        permission: 'finance.bills.void',
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
      },
      {
        label: 'Email',
        icon: 'mail',
        permission: 'core.email.send',
        visible: bil.status !== 'draft' && bil.status !== 'voided',
        handler: () => this.emailOpen.set(true)
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const bil = this.bill();
    if (!bil) return [];
    
    return [
      {
        title: 'Total Amount',
        value: formatMoney(bil.total),
        suffix: bil.currencyCode
      },
      {
        title: 'Amount Paid',
        value: formatMoney(bil.paid),
        suffix: bil.currencyCode
      },
      {
        title: 'Balance Due',
        value: formatMoney(this.balance()),
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
      { label: 'Bill Date', value: displayDate(bil.billDate) },
      { label: 'Due Date', value: displayDate(bil.dueDate) },
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

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

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
    askReason(this.modal, 'Void this bill?', 'Void Bill', 'This cannot be undone. The bill will be marked as voided.').then((reason) => {
      if (!reason) return;
      this.api.void(bil.id, reason).subscribe({
        next: (res) => { this.bill.set(res as unknown as Bill); this.message.success('Bill voided'); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void bill', 'reason'))
      });
    });
  }

  postBill(): void {
    const bil = this.bill();
    if (!bil) return;
    this.api.post(bil.id).subscribe({
      next: (res) => {
        this.bill.set(res as unknown as Bill);
        this.message.success(res.status === 'pendingApproval' ? 'Sent for approval. Post it again once it is approved.' : `${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${res.number}`);
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
