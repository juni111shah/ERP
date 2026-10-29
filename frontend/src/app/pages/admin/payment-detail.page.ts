import { EmailDocumentDrawerComponent } from '../../shared/email-document-drawer.component';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '../../core/api/api-config';
import { PaymentsApi } from '../../core/api/admin.api';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '../../shared/reason-prompt';
import { problemMessage } from '../../core/http/problem-details';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface BillAllocation {
  id: string;
  billId: string;
  billNumber: string;
  amount: number;
}

interface Payment {
  id: string;
  number: string;
  legalEntityId: string;
  partyId: string;
  partyName: string;
  date: string;
  method: string;
  bankAccountId: string | null;
  currencyCode: string;
  amount: number;
  unallocated: number;
  reference: string | null;
  status: string;
  paymentInstrumentId: string | null;
  journalEntryId: string | null;
  allocations: BillAllocation[];
}

@Component({
  selector: 'erp-payment-detail',
  standalone: true,
  imports: [EmailDocumentDrawerComponent, TranslatePipe, CommonModule, DatePipe, CurrencyPipe, NzTableModule, NzModalModule, DetailPageComponent],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (payment()) {
      <erp-detail-page
        [backLink]="['/finance/payments']"
        [backLabel]="'Back to Payments'"
        [title]="('ui.title.paymentNo' | translate: 'Payment #') + payment()!.number"
        [status]="payment()!.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="payment()!.date"
        [entityName]="'Payment'"
        recordType="Settlement" [recordId]="payment()!.id">
        
        @if (payment()!.allocations.length > 0) {
          <div content style="margin-top: 16px;">
            <h3>Applied to Bills</h3>
            <nz-table #allocTable [nzData]="payment()!.allocations" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
              <thead>
                <tr>
                  <th>Bill Number</th>
                  <th style="width: 150px; text-align: right;">Amount Applied</th>
                </tr>
              </thead>
              <tbody>
                @for (alloc of allocTable.data; track alloc.id) {
                  <tr>
                    <td>{{ alloc.billNumber }}</td>
                    <td style="text-align: right;">{{ alloc.amount | currency:payment()!.currencyCode }}</td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </div>
        }
      </erp-detail-page>
      <erp-email-document-drawer [open]="emailOpen()" [path]="'finance/payments/' + payment()!.id + '/email'" (closed)="emailOpen.set(false)" />
    }
  `
})
export class PaymentDetailPage implements OnInit {
  readonly emailOpen = signal(false);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly api = inject(PaymentsApi);
  private readonly modal = inject(NzModalService);

  loading = signal(true);
  error = signal<string | null>(null);
  payment = signal<Payment | null>(null);

  actions = computed((): ActionButton[] => {
    const pay = this.payment();
    if (!pay) return [];
    
    return [
      {
        label: 'Allocate',
        icon: 'link',
        type: 'primary',
        permission: 'finance.payments.write',
        visible: pay.status === 'issued' && pay.unallocated > 0,
        handler: () => this.router.navigate(['/finance/payments'], { queryParams: { open: pay.id } })
      },
      {
        label: 'Void',
        icon: 'stop',
        danger: true,
        permission: 'finance.payments.write',
        visible: pay.status === 'issued',
        handler: () => this.voidPayment()
      },
      {
        label: 'View Journal',
        icon: 'book',
        permission: 'finance.journals.read',
        visible: !!pay.journalEntryId,
        handler: () => this.router.navigate(['/finance/journals', pay.journalEntryId])
      },
      {
        label: 'Print',
        icon: 'printer',
        handler: () => window.open(`/print/payment/${pay.id}`, '_blank')
      },
      {
        label: 'Email',
        icon: 'mail',
        permission: 'core.email.send',
        handler: () => this.emailOpen.set(true)
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const pay = this.payment();
    if (!pay) return [];
    
    return [
      {
        title: 'Total Amount',
        value: pay.amount,
        suffix: pay.currencyCode
      },
      {
        title: 'Allocated',
        value: pay.amount - pay.unallocated,
        suffix: pay.currencyCode
      },
      {
        title: 'Unallocated',
        value: pay.unallocated,
        suffix: pay.currencyCode,
        valueStyle: { color: pay.unallocated > 0 ? '#fa8c16' : '#52c41a' }
      },
      {
        title: 'Bills Applied',
        value: pay.allocations.length
      }
    ];
  });

  descriptions = computed((): DescriptionItem[] => {
    const pay = this.payment();
    if (!pay) return [];
    
    return [
      { label: 'Vendor', value: pay.partyName, routerLink: ['/parties', pay.partyId] },
      { label: 'Date', value: new Date(pay.date).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Payment Method', value: pay.method },
      { label: 'Currency', value: pay.currencyCode },
      { label: 'Reference', value: pay.reference, span: 2 }
    ];
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid payment ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/finance/payments/${id}`).subscribe({
      next: (response) => {
        this.payment.set(response);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load payment');
        this.loading.set(false);
      }
    });
  }

  voidPayment(): void {
    const pay = this.payment();
    if (!pay) return;
    askReason(this.modal, 'Void this payment?', 'Void Payment', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (!reason) return;
      this.api.void(pay.id, reason).subscribe({
        next: (res) => { this.payment.set(res as unknown as Payment); this.message.success('Payment voided'); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void payment', 'reason'))
      });
    });
  }
}
