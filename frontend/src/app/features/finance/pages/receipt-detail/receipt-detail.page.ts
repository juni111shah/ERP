import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
// Kept under its original alias so the compiled page definition stays identical.
import { NzButtonModule as NzButtonModuleForPanel } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { ReceiptsApi } from '@core/api/admin.api';
import { FinanceDocumentsApi, ReceiptDetailDto } from '@core/api/finance-documents.api';
import { isFirstPaymentMoment } from '@core/format/celebrate';
import { problemMessage } from '@core/http/problem-details';
import { tNow } from '@core/i18n/translate-now';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { EmailDocumentDrawerComponent } from '@shared/components/email-document-drawer/email-document-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { SuccessPanelComponent } from '@shared/components/success-panel/success-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { loadRecord } from '@shared/utils/load-record';
import { askReason } from '@shared/utils/reason-prompt';
import { formatMoney } from '../../finance-format';
import { SettlementPageSpec, settlementActions, settlementDescriptions, settlementFacts, settlementKpis } from '../../finance-settlements';

const RECEIPT_PAGE: SettlementPageSpec = { listRoute: '/finance/receipts', permission: 'finance.receipts.write', printKind: 'receipt', appliedTitle: 'Invoices Applied', partyLabel: 'Customer' };

/** A customer receipt: the invoices it settles, and allocate / void / print / email. Right after recording it says what happened. */
@Component({
  selector: 'erp-receipt-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, NzButtonModuleForPanel, NzModalModule, NzSpinModule, NzTableModule, TranslatePipe, DetailPageComponent, EmailDocumentDrawerComponent, StatePanelComponent, SuccessPanelComponent, HasPermissionDirective],
  templateUrl: './receipt-detail.page.html',
})
export class ReceiptDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly api = inject(ReceiptsApi);
  private readonly documentsApi = inject(FinanceDocumentsApi);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  readonly emailOpen = signal(false);
  loading = signal(true);
  error = signal<string | null>(null);
  receipt = signal<ReceiptDetailDto | null>(null);
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
    const facts = settlementFacts(r);
    return applied ? tNow('ui.success.receiptApplied', 'Receipt {facts}, applied to {docs}.', { facts, docs: applied }) : tNow('ui.success.receiptFacts', 'Receipt {facts}.', { facts });
  });

  actions = computed((): ActionButton[] => { const rec = this.receipt(); return rec ? settlementActions(rec, RECEIPT_PAGE, this.router, () => this.voidReceipt(), () => this.emailOpen.set(true)) : []; });
  kpiCards = computed((): KpiCard[] => { const rec = this.receipt(); return rec ? settlementKpis(rec, RECEIPT_PAGE) : []; });
  descriptions = computed((): DescriptionItem[] => { const rec = this.receipt(); return rec ? settlementDescriptions(rec, RECEIPT_PAGE) : []; });

  money(v: number): string { return formatMoney(v, this.receipt()?.currencyCode); }

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    loadRecord(this.route.snapshot.paramMap.get('id'), (id) => this.documentsApi.receipt(id), { loading: this.loading, error: this.error, record: this.receipt },
      { invalid: 'Invalid receipt ID', failed: 'Failed to load receipt' }, (r) => this.showRecorded(r));
  }

  voidReceipt(): void {
    const rec = this.receipt();
    if (!rec) return;
    askReason(this.modal, 'Void this receipt?', 'Void Receipt', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (!reason) return;
      this.api.void(rec.id, reason).subscribe({
        next: (res) => { this.receipt.set(res as unknown as ReceiptDetailDto); this.message.success('Receipt voided'); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void receipt', 'reason')),
      });
    });
  }

  /** The message belongs to this visit, so the query flag leaves the address and a reload shows the plain record. */
  private showRecorded(r: ReceiptDetailDto): void {
    if (!this.route.snapshot.queryParamMap.get('recorded')) return;
    this.justRecorded.set(true);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    this.api.list({ page: 1, pageSize: 1 }).pipe(catchError(() => of({ total: 0 }))).subscribe((page) =>
      this.firstPayment.set(isFirstPaymentMoment(this.session.me()?.workspace?.id, page.total, r.allocations.length > 0)));
  }
}
