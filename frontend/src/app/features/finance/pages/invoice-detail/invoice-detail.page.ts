import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
// Kept under its original alias so the compiled page definition stays identical.
import { NzButtonModule as NzButtonModuleForPanel } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { catchError, of } from 'rxjs';
import { AdminApi, AuditApi, AuditEvent, InvoicesApi, TaxCodeDto, TaxCodesApi, UserSummary } from '@core/api/admin.api';
import { FinanceDocumentsApi, InvoiceAllocation, InvoiceDetailDto, InvoiceExtras, InvoiceLeaseLink, InvoiceLeaseLinksApi } from '@core/api/finance-documents.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { tNow } from '@core/i18n/translate-now';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { EmailDocumentDrawerComponent } from '@shared/components/email-document-drawer/email-document-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { SuccessPanelComponent } from '@shared/components/success-panel/success-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { loadRecord } from '@shared/utils/load-record';
import { askReason } from '@shared/utils/reason-prompt';
import { InvoiceSheetComponent } from '../../components/invoice-sheet/invoice-sheet.component';
import { balanceDue, paymentKpis, printDocument } from '../../finance-documents';
import { daysOverdue, formatMoney, humanizeSource, invoiceStatusLabel } from '../../finance-format';
import { taxRateMap } from '../../finance-lookups';

/** What just happened to the invoice on this visit (issued from the list's drawer, or emailed from here). */
interface InvoiceOutcome { kind: 'issued' | 'sent'; title: string; detail: string | null; }

/** An invoice or credit note: the document sheet, its receipts, history, attachments and the actions its status allows. */
@Component({
  selector: 'erp-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    NzButtonModuleForPanel,
    NzCardModule,
    NzModalModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzTimelineModule,
    TranslatePipe,
    AttachmentsComponent,
    DetailPageComponent,
    EmailDocumentDrawerComponent,
    StatePanelComponent,
    StatusTagComponent,
    SuccessPanelComponent,
    HasPermissionDirective,
    InvoiceSheetComponent,
  ],
  styleUrl: './invoice-detail.page.scss',
  templateUrl: './invoice-detail.page.html',
})
export class InvoiceDetailPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly documentsApi = inject(FinanceDocumentsApi);
  private readonly leaseLinksApi = inject(InvoiceLeaseLinksApi);
  private readonly auditApi = inject(AuditApi);
  private readonly adminApi = inject(AdminApi);
  private readonly taxCodesApi = inject(TaxCodesApi);

  readonly emailOpen = signal(false);
  /** What just happened to this invoice: stays on the page until closed or the person leaves. */
  readonly outcome = signal<InvoiceOutcome | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  invoice = signal<InvoiceDetailDto | null>(null);
  extras = signal<InvoiceExtras | null>(null);
  allocations = signal<InvoiceAllocation[]>([]);
  history = signal<AuditEvent[]>([]);
  users = signal<UserSummary[]>([]);
  leaseLink = signal<InvoiceLeaseLink | null>(null);
  taxRates = signal<Record<string, number>>({});

  balance = computed(() => { const inv = this.invoice(); return inv ? balanceDue(inv) : 0; });
  daysOverdue = computed(() => { const inv = this.invoice(); return inv ? daysOverdue(inv) : 0; });

  /** Part of "paid" not covered by receipts: applied credit notes leave no allocation row, so this is the difference. */
  creditsApplied = computed(() => {
    const inv = this.invoice();
    if (!inv || inv.isCreditNote) return 0;
    const received = this.allocations().reduce((s, a) => s + a.amount, 0);
    return Math.max(0, Math.round((inv.paid - received) * 100) / 100);
  });

  issuedBy = computed(() => {
    const created = this.history().find((h) => h.action === 'Created');
    return created?.userId ? this.userName(created.userId) : null;
  });

  actions = computed((): ActionButton[] => { const inv = this.invoice(); return inv ? this.buildActions(inv) : []; });

  kpiCards = computed((): KpiCard[] => {
    const inv = this.invoice();
    if (!inv) return [];
    if (inv.isCreditNote) {
      return [
        { title: 'Credit amount', value: formatMoney(inv.total), suffix: inv.currencyCode },
        { title: 'Applied', value: formatMoney(inv.paid), suffix: inv.currencyCode },
        { title: 'Still to apply', value: formatMoney(this.balance()), suffix: inv.currencyCode },
      ];
    }
    return paymentKpis(inv, this.balance(), this.daysOverdue());
  });

  descriptions = computed((): DescriptionItem[] => { const inv = this.invoice(); return inv ? this.buildDescriptions(inv) : []; });

  money(v: number): string { return formatMoney(v, this.invoice()?.currencyCode); }
  readonly fmtDate = displayDate;
  readonly statusLabel = invoiceStatusLabel;
  humanize(v: string | null | undefined): string {
    if (!v) return '';
    const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2');
    return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
  }
  userName(id?: string | null): string {
    if (!id) return 'System';
    const u = this.users().find((x) => x.id === id);
    return u ? u.fullName || u.email : 'User';
  }

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    loadRecord(this.route.snapshot.paramMap.get('id'), (id) => this.documentsApi.invoice(id), { loading: this.loading, error: this.error, record: this.invoice },
      { invalid: 'Invalid invoice ID', failed: 'Failed to load invoice' }, (inv) => { this.loadSections(inv); this.showIssued(inv); });
  }

  recordPayment(): void {
    const inv = this.invoice();
    if (inv) void this.router.navigate(['/finance/receipts'], { queryParams: { partyId: inv.partyId, invoiceId: inv.id } });
  }

  /** Creates a draft credit note linked to this invoice (all lines, capped by the API at the uncredited amount) and opens it. */
  createCreditNote(): void {
    const inv = this.invoice();
    if (!inv) return;
    this.invoicesApi.creditNote(inv.id).subscribe({
      next: (note) => { this.message.success(`${this.i18n.t('ui.msg.draftCnCreated', 'Draft credit note created')}: ${note.number}`); void this.router.navigate(['/finance/invoices'], { queryParams: { open: note.id } }); },
      error: (e) => this.message.error(problemMessage(e, 'Could not create the credit note', 'lines')),
    });
  }

  deleteDraft(): void {
    const inv = this.invoice();
    if (!inv) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft?',
      nzContent: 'The draft invoice will be removed permanently.',
      nzOkText: 'Delete',
      nzOkDanger: true,
      nzOnOk: () => this.invoicesApi.delete(inv.id).subscribe({
        next: () => { this.message.success(doneText('deleted', 'Draft invoice', inv.number)); void this.router.navigate(['/finance/invoices']); },
        error: (e) => { this.message.error(problemMessage(e, 'Could not delete the draft')); },
      }),
    });
  }

  voidInvoice(): void {
    const inv = this.invoice();
    if (!inv) return;
    askReason(this.modal, 'Void this invoice?', 'Void Invoice', 'This reverses its journal and cannot be undone. The invoice will be marked as voided.').then((reason) => {
      if (!reason) return;
      this.documentsApi.voidInvoice(inv.id, reason).subscribe({
        next: (r) => { this.invoice.set(r); this.message.success(doneText('voided', 'Invoice', inv.number)); },
        error: (e) => this.message.error(problemMessage(e, 'Could not void the invoice', 'reason')),
      });
    });
  }

  duplicate(): void {
    const inv = this.invoice();
    if (inv) void this.router.navigate(['/finance/invoices'], { queryParams: { duplicate: inv.id } });
  }

  viewJournal(journalId: string): void { this.router.navigate(['/finance/journals', journalId]); }
  print(id: string): void { printDocument('invoice', id); }
  email(): void { this.emailOpen.set(true); }

  onSent(to: string): void {
    const inv = this.invoice();
    if (inv) this.outcome.set({ kind: 'sent', title: tNow('ui.success.invoiceSent', 'Invoice {number} sent to {party}', { number: inv.number, party: inv.partyName || to }) + this.dueText(inv), detail: tNow('ui.success.emailedTo', 'Emailed to {to}.', { to }) });
  }

  private buildActions(inv: InvoiceDetailDto): ActionButton[] {
    const open = inv.status === 'issued' || inv.status === 'partlyPaid';
    const settled = open || inv.status === 'paid';
    return [
      { label: 'Record Payment', icon: 'dollar', type: 'primary', permission: 'finance.receipts.write', visible: !inv.isCreditNote && open, handler: () => this.recordPayment() },
      // The list page's drawer holds the apply form.
      { label: 'Apply to invoice', icon: 'link', type: 'primary', permission: 'finance.invoices.credit', visible: inv.isCreditNote && open, handler: () => this.router.navigate(['/finance/invoices'], { queryParams: { open: inv.id } }) },
      { label: 'Edit draft', icon: 'edit', type: 'primary', permission: 'finance.invoices.write', visible: inv.status === 'draft', handler: () => this.router.navigate(['/finance/invoices'], { queryParams: { open: inv.id } }) },
      { label: 'Open approvals', icon: 'check-square', visible: inv.status === 'pendingApproval', handler: () => this.router.navigate(['/approvals']) },
      { label: 'Return goods', icon: 'rollback', permission: 'sales.returns.manage', visible: this.session.isModuleEnabled('sales') && !inv.isCreditNote && settled, handler: () => this.router.navigate(['/sales/returns'], { queryParams: { invoice: inv.id } }) },
      { label: 'Credit Note', icon: 'file-add', permission: 'finance.invoices.credit', visible: !inv.isCreditNote && settled, handler: () => this.createCreditNote() },
      // The API voids only an issued document with nothing paid or applied (409 otherwise).
      { label: 'Void', icon: 'stop', danger: true, permission: 'finance.invoices.void', visible: inv.status === 'issued' && inv.paid === 0, handler: () => this.voidInvoice() },
      { label: 'Delete', icon: 'delete', danger: true, permission: 'finance.invoices.write', visible: inv.status === 'draft', handler: () => this.deleteDraft() },
      { label: 'Duplicate', icon: 'copy', permission: 'finance.invoices.write', handler: () => this.duplicate() },
      { label: 'View Journal', icon: 'book', visible: !!inv.journalEntryId, handler: () => this.viewJournal(inv.journalEntryId!) },
      { label: 'Print', icon: 'printer', handler: () => this.print(inv.id) },
      { label: 'Email', icon: 'mail', permission: 'core.email.send', visible: inv.status !== 'draft' && inv.status !== 'voided' && inv.status !== 'pendingApproval', handler: () => this.email() },
    ];
  }

  private buildDescriptions(inv: InvoiceDetailDto): DescriptionItem[] {
    const h = this.extras()?.header;
    const items: DescriptionItem[] = [];
    if (h) items.push({ label: 'Company', value: h.legalEntityName || h.organizationName });
    items.push({ label: 'Customer', value: inv.partyName || 'Customer', routerLink: ['/parties', inv.partyId] });
    if (this.issuedBy()) items.push({ label: 'Issued by', value: this.issuedBy() });
    const link = this.leaseLink();
    if (link) {
      items.push({ label: 'Service period', value: `${displayDate(link.periodFrom)} – ${displayDate(link.periodTo)}` });
      items.push({ label: 'Lease', value: link.leaseNumber ?? '—' });
      items.push({ label: 'Property', value: [link.propertyName, link.unitNumber ? `${this.i18n.t('ui.re.unitLc', 'unit')} ${link.unitNumber}` : ''].filter((x) => !!x).join(', ') });
    }
    const src = humanizeSource(inv.sourceType);
    if (src) items.push({ label: 'Source', value: src });
    const x = this.extras();
    const originalId = inv.creditNoteForId ?? x?.originalInvoiceId ?? (inv.isCreditNote && inv.sourceType === 'invoice' ? inv.sourceId : null);
    if (originalId) items.push({ label: 'Credit Note For', value: x?.originalInvoiceNumber ? `${this.i18n.t('ui.label.invoice', 'Invoice')} ${x.originalInvoiceNumber}` : 'View original invoice', routerLink: ['/finance/invoices', originalId] });
    return items;
  }

  /** Each optional section loads on its own and simply stays hidden when the user lacks the permission or there is no data. */
  private loadSections(inv: InvoiceDetailDto): void {
    this.documentsApi.invoiceExtras(inv.id).subscribe((x) => this.extras.set(x));
    this.taxCodesApi.list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] })))
      .subscribe((r) => this.taxRates.set(taxRateMap(r.items)));
    this.documentsApi.invoiceAllocations(inv.id).pipe(catchError(() => of([] as InvoiceAllocation[])))
      .subscribe((r) => this.allocations.set(r ?? []));
    this.auditApi.list({ page: 1, pageSize: 50, sort: 'occurredAt', filters: [{ field: 'recordId', op: 'eq', value: inv.id }] })
      .pipe(catchError(() => of({ items: [] as AuditEvent[] })))
      .subscribe((r) => {
        this.history.set(r.items ?? []);
        if (r.items?.some((h) => h.userId)) this.adminApi.users().pipe(catchError(() => of([] as UserSummary[]))).subscribe((u) => this.users.set(u ?? []));
      });
    if ((inv.sourceType ?? '').toLowerCase().startsWith('realestate.')) {
      this.leaseLinksApi.links([inv.id]).pipe(catchError(() => of([] as InvoiceLeaseLink[])))
        .subscribe((r) => this.leaseLink.set(r?.[0] ?? null));
    }
  }

  private dueText(inv: InvoiceDetailDto): string { return inv.dueDate ? tNow('ui.success.due', ', due {date}', { date: displayDate(inv.dueDate) }) : ''; }

  /** Issued from the list's drawer: the query flag leaves the address, so a reload shows the plain record. */
  private showIssued(inv: InvoiceDetailDto): void {
    if (!this.route.snapshot.queryParamMap.get('issued')) return;
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    this.outcome.set({ kind: 'issued', title: tNow('ui.success.invoiceIssued', 'Invoice {number} issued to {party}', { number: inv.number, party: inv.partyName || '' }) + this.dueText(inv), detail: tNow('ui.success.toCollect', '{amount} to collect. Nothing has gone to the customer yet.', { amount: formatMoney(inv.total, inv.currencyCode) }) });
  }
}
