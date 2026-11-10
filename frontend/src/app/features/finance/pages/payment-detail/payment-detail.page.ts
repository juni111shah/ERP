import { EmailDocumentDrawerComponent } from '@shared/components/email-document-drawer/email-document-drawer.component';
import { SuccessPanelComponent } from '@shared/components/success-panel/success-panel.component';
import { tNow } from '@core/i18n/translate-now';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { NzButtonModule as NzButtonModuleForPanel } from 'ng-zorro-antd/button';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '@core/api/api-config';
import { PaymentsApi } from '@core/api/admin.api';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '@shared/utils/reason-prompt';
import { problemMessage } from '@core/http/problem-details';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { displayDate, displayLabel } from '@core/format/display';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '@shared/components/detail-page/detail-page.component';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { formatMoney } from '../../components/invoice-sheet/invoice-sheet.component';

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
  imports: [HasPermissionDirective, SuccessPanelComponent, NzButtonModuleForPanel, RouterLink, NzSpinModule, StatePanelComponent, EmailDocumentDrawerComponent, TranslatePipe, CommonModule, DatePipe, NzTableModule, NzModalModule, DetailPageComponent],
  templateUrl: './payment-detail.page.html'
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

  money(v: number): string { return formatMoney(v, this.payment()?.currencyCode); }
  /** Arrived here straight after recording the money: the outcome stays on the page until it is closed or the person leaves. */
  readonly justRecorded = signal(false);
  readonly recordedTitle = computed(() => {
    const p = this.payment();
    return p ? tNow('ui.success.paid', 'Paid {amount} to {party}', { amount: this.money(p.amount), party: p.partyName }) : '';
  });
  readonly recordedDetail = computed(() => {
    const p = this.payment();
    if (!p) return '';
    const applied = p.allocations.map((a) => a.billNumber).join(', ');
    const facts = `${p.number}, ${displayDate(p.date)}, ${displayLabel(p.method)}`;
    return applied ? tNow('ui.success.paymentApplied', 'Payment {facts}, applied to {docs}.', { facts, docs: applied }) : tNow('ui.success.paymentFacts', 'Payment {facts}.', { facts });
  });
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
        visible: pay.status === 'issued',
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
        value: formatMoney(pay.amount),
        suffix: pay.currencyCode
      },
      {
        title: 'Allocated',
        value: formatMoney(pay.allocations.reduce((s, a) => s + a.amount, 0)),
        suffix: pay.currencyCode
      },
      {
        title: 'Unallocated',
        value: formatMoney(pay.unallocated),
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
      { label: 'Date', value: displayDate(pay.date) },
      { label: 'Payment Method', value: displayLabel(pay.method) },
      { label: 'Currency', value: pay.currencyCode },
      { label: 'Reference', value: pay.reference, span: 2 }
    ];
  });

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

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
        this.showRecorded();
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load payment');
        this.loading.set(false);
      }
    });
  }

  /** The message belongs to this visit, so the query flag leaves the address and a reload shows the plain record. */
  private showRecorded(): void {
    if (!this.route.snapshot.queryParamMap.get('recorded')) return;
    this.justRecorded.set(true);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
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
