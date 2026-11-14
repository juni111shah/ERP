import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
// Kept under its original alias so the compiled page definition stays identical.
import { NzButtonModule as NzButtonModuleForPanel } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { PaymentsApi } from '@core/api/admin.api';
import { FinanceDocumentsApi, PaymentDetailDto } from '@core/api/finance-documents.api';
import { problemMessage } from '@core/http/problem-details';
import { tNow } from '@core/i18n/translate-now';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { EmailDocumentDrawerComponent } from '@shared/components/email-document-drawer/email-document-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { SuccessPanelComponent } from '@shared/components/success-panel/success-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CanPipe } from '@shared/pipes/can.pipe';
import { loadRecord } from '@shared/utils/load-record';
import { askReason } from '@shared/utils/reason-prompt';
import { formatMoney } from '../../finance-format';
import { SettlementPageSpec, settlementActions, settlementDescriptions, settlementFacts, settlementKpis } from '../../finance-settlements';

const PAYMENT_PAGE: SettlementPageSpec = { listRoute: '/finance/payments', permission: 'finance.payments.write', printKind: 'payment', appliedTitle: 'Bills Applied', partyLabel: 'Vendor' };

/** A vendor payment: the bills it settles, and allocate / void / print / email. Right after recording it says what happened. */
@Component({
  selector: 'erp-payment-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, NzButtonModuleForPanel, NzModalModule, NzSpinModule, NzTableModule, TranslatePipe, DetailPageComponent, EmailDocumentDrawerComponent, StatePanelComponent, SuccessPanelComponent, HasPermissionDirective, CanPipe],
  templateUrl: './payment-detail.page.html',
})
export class PaymentDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly api = inject(PaymentsApi);
  private readonly documentsApi = inject(FinanceDocumentsApi);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  readonly emailOpen = signal(false);
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
    const facts = settlementFacts(p);
    return applied ? tNow('ui.success.paymentApplied', 'Payment {facts}, applied to {docs}.', { facts, docs: applied }) : tNow('ui.success.paymentFacts', 'Payment {facts}.', { facts });
  });
  loading = signal(true);
  error = signal<string | null>(null);
  payment = signal<PaymentDetailDto | null>(null);

  actions = computed((): ActionButton[] => { const pay = this.payment(); return pay ? settlementActions(pay, PAYMENT_PAGE, this.router, () => this.voidPayment(), () => this.emailOpen.set(true)) : []; });
  kpiCards = computed((): KpiCard[] => { const pay = this.payment(); return pay ? settlementKpis(pay, PAYMENT_PAGE) : []; });
  descriptions = computed((): DescriptionItem[] => { const pay = this.payment(); return pay ? settlementDescriptions(pay, PAYMENT_PAGE, this.session.hasPermission('core.parties.read')) : []; });

  money(v: number): string { return formatMoney(v, this.payment()?.currencyCode); }

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    loadRecord(this.route.snapshot.paramMap.get('id'), (id) => this.documentsApi.payment(id), { loading: this.loading, error: this.error, record: this.payment },
      { invalid: 'Invalid payment ID', failed: 'Failed to load payment' }, () => this.showRecorded());
  }

  voidPayment(): void {
    const pay = this.payment();
    if (!pay) return;
    askReason(this.modal, 'Void this payment?', 'Void Payment', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (!reason) return;
      this.api.void(pay.id, reason).subscribe({
        next: (res) => { this.payment.set(res as unknown as PaymentDetailDto); this.message.success('Payment voided'); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void payment', 'reason')),
      });
    });
  }

  /** The message belongs to this visit, so the query flag leaves the address and a reload shows the plain record. */
  private showRecorded(): void {
    if (!this.route.snapshot.queryParamMap.get('recorded')) return;
    this.justRecorded.set(true);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }
}
