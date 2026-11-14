import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { BillsApi } from '@core/api/admin.api';
import { BillDetailDto, FinanceDocumentsApi } from '@core/api/finance-documents.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { EmailDocumentDrawerComponent } from '@shared/components/email-document-drawer/email-document-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { loadRecord } from '@shared/utils/load-record';
import { askReason } from '@shared/utils/reason-prompt';
import { balanceDue, paymentKpis, printDocument } from '../../finance-documents';
import { daysOverdue, formatMoney, invoiceStatusLabel } from '../../finance-format';

/** The message of a failed post or delete: the server's detail or message, else `fallback`. */
const serverMessage = (err: { error?: { detail?: string; message?: string } } | null | undefined, fallback: string): string => err?.error?.detail || err?.error?.message || fallback;

/** A vendor bill: lines, totals and the actions its status allows (pay, post, edit, return, void, print, email). */
@Component({
  selector: 'erp-bill-detail',
  standalone: true,
  imports: [CommonModule, NzModalModule, NzSpinModule, NzTableModule, TranslatePipe, DetailPageComponent, EmailDocumentDrawerComponent, StatePanelComponent],
  styleUrl: './bill-detail.page.scss',
  templateUrl: './bill-detail.page.html',
})
export class BillDetailPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly api = inject(BillsApi);
  private readonly documentsApi = inject(FinanceDocumentsApi);

  readonly emailOpen = signal(false);
  readonly statusLabel = invoiceStatusLabel;
  loading = signal(true);
  error = signal<string | null>(null);
  bill = signal<BillDetailDto | null>(null);

  balance = computed(() => { const bil = this.bill(); return bil ? balanceDue(bil) : 0; });
  daysOverdue = computed(() => { const bil = this.bill(); return bil ? daysOverdue(bil) : 0; });

  actions = computed((): ActionButton[] => { const bil = this.bill(); return bil ? this.buildActions(bil) : []; });
  kpiCards = computed((): KpiCard[] => { const bil = this.bill(); return bil ? paymentKpis(bil, this.balance(), this.daysOverdue()) : []; });
  descriptions = computed((): DescriptionItem[] => { const bil = this.bill(); return bil ? billDescriptions(bil, this.session.hasPermission('core.parties.read')) : []; });

  money(v: number): string { return formatMoney(v, this.bill()?.currencyCode); }

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    loadRecord(this.route.snapshot.paramMap.get('id'), (id) => this.documentsApi.bill(id), { loading: this.loading, error: this.error, record: this.bill },
      { invalid: 'Invalid bill ID', failed: 'Failed to load bill' });
  }

  voidBill(): void {
    const bil = this.bill();
    if (!bil) return;
    askReason(this.modal, 'Void this bill?', 'Void Bill', 'This cannot be undone. The bill will be marked as voided.').then((reason) => {
      if (!reason) return;
      this.api.void(bil.id, reason).subscribe({
        next: (res) => { this.bill.set(res as unknown as BillDetailDto); this.message.success(doneText('voided', 'Bill', bil.number)); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void bill', 'reason')),
      });
    });
  }

  postBill(): void {
    const bil = this.bill();
    if (!bil) return;
    this.api.post(bil.id).subscribe({
      next: (res) => {
        this.bill.set(res as unknown as BillDetailDto);
        this.message.success(res.status === 'pendingApproval' ? 'Sent for approval. Post it again once it is approved.' : `${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${res.number}`);
      },
      error: (err) => this.message.error(serverMessage(err, 'Failed to post bill')),
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
        next: () => { this.message.success(doneText('deleted', 'Draft bill', bil.number)); this.router.navigate(['/finance/bills']); },
        error: (err) => this.message.error(serverMessage(err, 'Failed to delete bill')),
      }),
    });
  }

  private buildActions(bil: BillDetailDto): ActionButton[] {
    const draft = bil.status === 'draft' || bil.status === 'pendingApproval';
    return [
      { label: 'Make Payment', icon: 'dollar', type: 'primary', permission: 'finance.payments.write', visible: bil.status === 'issued' || bil.status === 'partlyPaid', handler: () => this.router.navigate(['/finance/payments'], { queryParams: { partyId: bil.partyId, billId: bil.id } }) },
      { label: 'Post', icon: 'check', type: 'primary', permission: 'finance.bills.post', visible: draft, handler: () => this.postBill() },
      { label: 'Edit', icon: 'edit', permission: 'finance.bills.write', visible: draft, handler: () => this.router.navigate(['/finance/bills'], { queryParams: { open: bil.id } }) },
      { label: 'Delete', icon: 'delete', danger: true, permission: 'finance.bills.write', visible: draft, handler: () => this.deleteBill() },
      { label: 'Return goods', icon: 'rollback', permission: 'purchasing.returns.manage', visible: this.session.isModuleEnabled('purchasing') && (bil.status === 'issued' || bil.status === 'partlyPaid' || bil.status === 'paid'), handler: () => this.router.navigate(['/purchasing/returns'], { queryParams: { bill: bil.id } }) },
      { label: 'Void', icon: 'stop', danger: true, permission: 'finance.bills.void', visible: bil.status === 'issued' && bil.paid === 0, handler: () => this.voidBill() },
      { label: 'View Journal', icon: 'book', permission: 'finance.journals.read', visible: !!bil.journalEntryId, handler: () => this.router.navigate(['/finance/journals', bil.journalEntryId]) },
      { label: 'Print', icon: 'printer', handler: () => printDocument('bill', bil.id) },
      { label: 'Email', icon: 'mail', permission: 'core.email.send', visible: bil.status !== 'draft' && bil.status !== 'voided', handler: () => this.emailOpen.set(true) },
    ];
  }
}

/** Vendor, dates and currency; approval, vendor reference and notes when there are any. */
function billDescriptions(bil: BillDetailDto, partyLink: boolean): DescriptionItem[] {
  const items: DescriptionItem[] = [
    { label: 'Vendor', value: bil.partyName, routerLink: partyLink ? ['/parties', bil.partyId] : undefined },
    { label: 'Bill Date', value: displayDate(bil.billDate) },
    { label: 'Due Date', value: displayDate(bil.dueDate) },
    { label: 'Currency', value: bil.currencyCode },
  ];
  if (bil.status === 'pendingApproval' || bil.approvalRequestId) items.push({ label: 'Approval', value: bil.status === 'pendingApproval' ? 'Pending approval' : 'Approved', routerLink: ['/approvals'] });
  if (bil.vendorReference) items.push({ label: 'Vendor Reference', value: bil.vendorReference });
  if (bil.notes) items.push({ label: 'Notes', value: bil.notes, span: 2 });
  return items;
}
