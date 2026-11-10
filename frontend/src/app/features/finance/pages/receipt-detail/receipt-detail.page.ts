import { EmailDocumentDrawerComponent } from '@shared/components/email-document-drawer/email-document-drawer.component';
import { SuccessPanelComponent } from '@shared/components/success-panel/success-panel.component';
import { tNow } from '@core/i18n/translate-now';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { isFirstPaymentMoment } from '@core/format/celebrate';
import { SessionStore } from '@core/session/session.store';
import { NzButtonModule as NzButtonModuleForPanel } from 'ng-zorro-antd/button';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '@core/api/api-config';
import { ReceiptsApi } from '@core/api/admin.api';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, of } from 'rxjs';
import { askReason } from '@shared/utils/reason-prompt';
import { problemMessage } from '@core/http/problem-details';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { displayDate, displayLabel } from '@core/format/display';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '@shared/components/detail-page/detail-page.component';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { formatMoney } from '../../components/invoice-sheet/invoice-sheet.component';

interface Allocation {
  id: string;
  invoiceId: string;
  invoiceNumber: string;
  amount: number;
}

interface Receipt {
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
  journalEntryId: string | null;
  allocations: Allocation[];
}

@Component({
  selector: 'erp-receipt-detail',
  standalone: true,
  imports: [HasPermissionDirective, SuccessPanelComponent, NzButtonModuleForPanel, RouterLink, NzSpinModule, StatePanelComponent, EmailDocumentDrawerComponent, TranslatePipe, CommonModule, DatePipe, NzTableModule, NzModalModule, DetailPageComponent],
  templateUrl: './receipt-detail.page.html'
})
export class ReceiptDetailPage implements OnInit {
  readonly emailOpen = signal(false);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly api = inject(ReceiptsApi);
  private readonly modal = inject(NzModalService);

  money(v: number): string { return formatMoney(v, this.receipt()?.currencyCode); }
  loading = signal(true);
  error = signal<string | null>(null);
  receipt = signal<Receipt | null>(null);
  private readonly session = inject(SessionStore);
  /** Arrived here straight after recording the money: the outcome stays on the page until it is closed or the person leaves. */
  readonly justRecorded = signal(false);
  readonly firstPayment = signal(false);
  readonly recordedTitle = computed(() => {
    const r = this.receipt();
    return r ? tNow('ui.success.received', 'Received {amount} from {party}', { amount: this.money(r.amount), party: r.partyName }) : '';
  });
  readonly recordedDetail = computed(() => {
    const r = this.receipt();
    if (!r) return '';
    const applied = r.allocations.map((a) => a.invoiceNumber).join(', ');
    const facts = `${r.number}, ${displayDate(r.date)}, ${displayLabel(r.method)}`;
    return applied ? tNow('ui.success.receiptApplied', 'Receipt {facts}, applied to {docs}.', { facts, docs: applied }) : tNow('ui.success.receiptFacts', 'Receipt {facts}.', { facts });
  });


  actions = computed((): ActionButton[] => {
    const rec = this.receipt();
    if (!rec) return [];
    
    return [
      {
        label: 'Allocate',
        icon: 'link',
        type: 'primary',
        permission: 'finance.receipts.write',
        visible: rec.status === 'issued' && rec.unallocated > 0,
        handler: () => this.router.navigate(['/finance/receipts'], { queryParams: { open: rec.id } })
      },
      {
        label: 'Void',
        icon: 'stop',
        danger: true,
        permission: 'finance.receipts.write',
        visible: rec.status === 'issued',
        handler: () => this.voidReceipt()
      },
      {
        label: 'View Journal',
        icon: 'book',
        permission: 'finance.journals.read',
        visible: !!rec.journalEntryId,
        handler: () => this.router.navigate(['/finance/journals', rec.journalEntryId])
      },
      {
        label: 'Print',
        icon: 'printer',
        handler: () => window.open(`/print/receipt/${rec.id}`, '_blank')
      },
      {
        label: 'Email',
        icon: 'mail',
        permission: 'core.email.send',
        visible: rec.status === 'issued',
        handler: () => this.emailOpen.set(true)
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const rec = this.receipt();
    if (!rec) return [];
    
    return [
      {
        title: 'Total Amount',
        value: formatMoney(rec.amount),
        suffix: rec.currencyCode
      },
      {
        title: 'Allocated',
        value: formatMoney(rec.allocations.reduce((s, a) => s + a.amount, 0)),
        suffix: rec.currencyCode
      },
      {
        title: 'Unallocated',
        value: formatMoney(rec.unallocated),
        suffix: rec.currencyCode,
        valueStyle: { color: rec.unallocated > 0 ? '#fa8c16' : '#52c41a' }
      },
      {
        title: 'Invoices Applied',
        value: rec.allocations.length
      }
    ];
  });

  descriptions = computed((): DescriptionItem[] => {
    const rec = this.receipt();
    if (!rec) return [];
    
    return [
      { label: 'Customer', value: rec.partyName, routerLink: ['/parties', rec.partyId] },
      { label: 'Date', value: displayDate(rec.date) },
      { label: 'Payment Method', value: displayLabel(rec.method) },
      { label: 'Currency', value: rec.currencyCode },
      { label: 'Reference', value: rec.reference, span: 2 }
    ];
  });

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid receipt ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/finance/receipts/${id}`).subscribe({
      next: (response) => {
        this.receipt.set(response);
        this.loading.set(false);
        this.showRecorded(response);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load receipt');
        this.loading.set(false);
      }
    });
  }

  /** The message belongs to this visit, so the query flag leaves the address and a reload shows the plain record. */
  private showRecorded(r: Receipt): void {
    if (!this.route.snapshot.queryParamMap.get('recorded')) return;
    this.justRecorded.set(true);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    this.api.list({ page: 1, pageSize: 1 }).pipe(catchError(() => of({ total: 0 }))).subscribe((page) =>
      this.firstPayment.set(isFirstPaymentMoment(this.session.me()?.workspace?.id, page.total, r.allocations.length > 0)));
  }

  voidReceipt(): void {
    const rec = this.receipt();
    if (!rec) return;
    askReason(this.modal, 'Void this receipt?', 'Void Receipt', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (!reason) return;
      this.api.void(rec.id, reason).subscribe({
        next: (res) => { this.receipt.set(res as unknown as Receipt); this.message.success('Receipt voided'); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void receipt', 'reason'))
      });
    });
  }
}
