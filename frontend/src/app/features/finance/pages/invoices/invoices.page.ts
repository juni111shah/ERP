import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, of } from 'rxjs';
import { InvoiceDto, InvoiceSummary, InvoicesApi } from '@core/api/admin.api';
import { ListQuery } from '@core/api/api.models';
import { InvoiceExtras, InvoiceLeaseLink, InvoiceLeaseLinksApi, FinanceDocumentsApi } from '@core/api/finance-documents.api';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi, PartyDto } from '@core/api/parties.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { controlError, problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SuccessToastService } from '@core/services/success-toast.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { askReason } from '@shared/utils/reason-prompt';
import { InvoiceSheetComponent } from '../../components/invoice-sheet/invoice-sheet.component';
import { TermsDueDate } from '../../finance-due-date';
import { daysOverdue, formatMoney, humanizeSource, invoiceStatusLabel, taxNameFor } from '../../finance-format';
import { KpiStats } from '../../finance-kpis';
import { InvoiceLineSeed, invoiceLineGroup, lineFieldStatus, taxedLineTotals, withTaxCodes } from '../../finance-lines';
import { FinanceLookups, documentPartyOption, partyOption } from '../../finance-lookups';
import { DocumentStatsData, invoiceStats, loadDocumentStats } from '../../finance-stats';

const STATUS_OPTIONS = ['draft', 'pendingApproval', 'issued', 'partlyPaid', 'paid', 'voided'];

/** Sales invoices and credit notes: draft with a line editor, issue (posts to the ledger), void, and apply a credit note to an invoice. */
@Component({
  selector: 'erp-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzTagModule,
    NzToolTipModule,
    TranslatePipe,
    PagedTableComponent,
    StatCardComponent,
    StatusTagComponent,
    HasPermissionDirective,
    ErpDatePipe,
    InvoiceSheetComponent,
  ],
  providers: [FinanceLookups],
  styleUrl: './invoices.page.scss',
  templateUrl: './invoices.page.html',
})
export class InvoicesPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly toast = inject(SuccessToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly modal = inject(NzModalService);
  private readonly documentsApi = inject(FinanceDocumentsApi);
  private readonly leaseLinksApi = inject(InvoiceLeaseLinksApi);
  private readonly lookups = inject(FinanceLookups).load({ entities: true, taxCodes: true });
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;

  readonly entities = this.lookups.entities;
  readonly taxes = this.lookups.activeTaxes;
  readonly taxRates = this.lookups.taxRates;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-invoiceDate' });
  readonly statusOptions = STATUS_OPTIONS;
  readonly statusFilter = signal<string | null>(null);
  readonly billing = signal(false);
  readonly extras = signal<InvoiceExtras | null>(null);
  /** invoice id → lease link; filled only for rows the real-estate module raised */
  readonly links = signal<Record<string, InvoiceLeaseLink>>({});
  /** True while invoice-links is loading, so Lease/Property show a placeholder instead of a blank cell. */
  readonly linksLoading = signal(false);
  /** VAT / GST / Sales tax when every legal entity is in one country, else plain "Tax" (same table as the print layout). */
  readonly taxLabel = computed(() => { const c = [...new Set(this.entities().map((e) => e.countryCode))]; return c.length === 1 ? taxNameFor(c[0]) : 'Tax'; });
  readonly objectKeys = Object.keys;
  readonly columns = computed((): TableColumn<InvoiceSummary>[] => this.buildColumns());

  private readonly kpis = new KpiStats<DocumentStatsData<InvoiceSummary>>();
  readonly statsLoading = this.kpis.loading;
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = this.kpis.error;
  /** The figures for the whole workspace, not the page on screen: open invoices and this year's invoices are read in full, drafts are counted by the server. */
  readonly stats = computed(() => { const d = this.kpis.data(); return d ? invoiceStats(d, this.workspaceCurrency()) : null; });

  /** A new document opened from a link can beat the legal entity list; fill the only choice once it arrives. */
  private readonly defaultEntity = effect(() => {
    const entities = this.entities();
    if (entities.length === 1 && this.drawerOpen() && !this.invoice() && !this.form.controls.legalEntityId.value) this.form.controls.legalEntityId.setValue(entities[0].id);
  });
  private readonly loadLinks = effect(() => {
    const ids = this.list.items().filter((r) => (r.sourceType ?? '').toLowerCase().startsWith('realestate.')).map((r) => r.id);
    if (!ids.length) { this.links.set({}); return; }
    this.linksLoading.set(true);
    this.leaseLinksApi.links(ids).pipe(catchError(() => of([] as InvoiceLeaseLink[])))
      .subscribe((r) => { this.links.set(Object.fromEntries((r ?? []).map((l) => [l.invoiceId, l]))); this.linksLoading.set(false); });
  });

  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly invoice = signal<InvoiceDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly applyOpen = signal(false);
  readonly applyLocked = signal(false);
  readonly openInvoices = signal<LookupOption[]>([]);
  applyInvoice: string | null = null;
  applyAmount = 0;
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], invoiceDate: [null as Date | null, Validators.required], dueDate: [null as Date | null],
    isCreditNote: [false], notes: [''], lines: this.fb.array<ReturnType<typeof invoiceLineGroup>>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly totals = computed(() => taxedLineTotals(this.value().lines ?? [], this.taxes()));
  readonly sheetData = computed(() => { const i = this.invoice(); return i ? { ...i, lines: i.lines.map((l) => ({ ...l, taxCodeId: l.taxCodeId ?? null })) } : null; });
  readonly editable = computed(() => !this.invoice() || this.invoice()!.status === 'draft');
  readonly title = computed(() => this.invoice() ? `${this.invoice()!.isCreditNote ? 'Credit note' : 'Invoice'} ${this.invoice()!.number}` : 'New invoice');
  private readonly dueDate: TermsDueDate;

  constructor() {
    this.loadStats();
    const c = this.form.controls;
    this.dueDate = new TermsDueDate({ party: c.partyId, date: c.invoiceDate, due: c.dueDate }, this.parties, this.editable);
  }

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  /** A line field the user has tried to save empty; these sit outside nz-form-control, so they need their own red state. */
  lineStatus(line: AbstractControl, name: string): 'error' | '' { return lineFieldStatus(line, name); }
  /** Line totals in the currency of the chosen legal entity. */
  lineMoney(v: number): string { return formatMoney(v, this.entities().find((e) => e.id === this.value().legalEntityId)?.baseCurrency ?? this.workspaceCurrency()); }
  label(s: string): string { return invoiceStatusLabel(s); }
  overdue(r: InvoiceSummary): number { return daysOverdue(r); }
  source(r: InvoiceSummary): string { return humanizeSource(r.sourceType); }
  link(r: InvoiceSummary): InvoiceLeaseLink | undefined { return this.links()[r.id]; }
  err(name: string): string { return controlError(this.form.get(name)); }

  filterStatus(s: string | null): void {
    this.statusFilter.set(s);
    this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []);
  }

  /** Runs the module's billing run (invoices every lease instalment due by today). Only shown to users holding that module's permission. */
  runBilling(): void {
    this.modal.confirm({
      nzTitle: 'Run billing now?', nzContent: 'Creates and issues an invoice for every scheduled instalment due by today.', nzOkText: 'Run billing',
      nzOnOk: () => {
        this.billing.set(true);
        this.leaseLinksApi.runBilling(toIsoDate(new Date())).subscribe({
          next: (r) => { this.billing.set(false); this.message.success(`${this.i18n.t('ui.msg.billingRunCreated', 'Billing run created')} ${r?.invoiced ?? 0} ${this.i18n.t('ui.msg.invoicesParen', 'invoice(s)')}`); this.reload(); },
          error: (e) => { this.billing.set(false); this.fail(e); },
        });
      },
    });
  }

  addLine(): void { this.lines.push(invoiceLineGroup(this.fb)); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.lookups.findParties(term).subscribe((found) => {
      found.forEach((p) => this.dueDate.remember(p));
      this.partyOptions.update((cur) => this.merge(cur, found.map(partyOption)));
    });
  }

  /** `?open=<id>` opens a drawer; `?duplicate=<id>` / `?creditFor=<id>` start a new draft prefilled from that invoice (detail page actions); `?partyId=<id>` starts one for that customer. */
  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    if (q.get('new')) this.show(null);
    const open = q.get('open'), dup = q.get('duplicate'), credit = q.get('creditFor'), party = q.get('partyId');
    if (open) this.openById(open);
    else if (dup || credit) this.api.detail((dup ?? credit)!).subscribe((src) => this.prefill(src, !!credit && !dup));
    else if (party) this.parties.get(party).subscribe((p) => this.startFor(p));
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/invoices', id]); }
  printInvoice(id: string): void { window.open(`/print/invoice/${id}`, '_blank'); }

  open(row: InvoiceSummary | null): void {
    if (!row) { this.extras.set(null); this.show(null); return; }
    this.openById(row.id);
  }

  save(issue: boolean): void {
    if (this.form.invalid) { revealErrors(this.form, { touchAll: true }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, partyId: v.partyId, invoiceDate: toIsoDate(v.invoiceDate!), dueDate: v.dueDate ? toIsoDate(v.dueDate) : null, isCreditNote: v.isCreditNote, notes: v.notes || null,
      lines: withTaxCodes(v.lines),
    };
    this.saving.set(true);
    this.api.save(this.invoice()?.id ?? null, body).subscribe({
      next: (saved) => (issue ? this.issue(saved) : this.savedDraft(saved, v.partyId)),
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  voidDocument(): void {
    const inv = this.invoice();
    if (!inv) return;
    void askReason(this.modal, 'Void this invoice?', 'Void Invoice', 'This reverses its journal and cannot be undone. The invoice will be marked as voided.').then((reason) => {
      if (reason) this.api.void(inv.id, reason).subscribe({ next: () => this.done(doneText('voided', 'Invoice', inv.number)), error: (e) => this.fail(e) });
    });
  }

  /** A credit note raised from an invoice can settle only that invoice (API rule), so it is chosen and locked; any other credit note lists the customer's open invoices. */
  openApply(): void {
    const credit = this.invoice();
    if (!credit) return;
    const left = credit.total - credit.paid;
    this.applyInvoice = null;
    this.applyAmount = left;
    const sourceId = credit.sourceType === 'invoice' ? credit.sourceId ?? null : null;
    this.applyLocked.set(!!sourceId);
    if (sourceId) {
      this.api.detail(sourceId).subscribe((src) => {
        this.openInvoices.set([{ value: src.id, label: this.applyLabel(src) }]);
        this.applyInvoice = src.id;
        this.applyAmount = Math.min(left, src.total - src.paid);
        this.applyOpen.set(true);
      });
    } else {
      this.searchOpenInvoices('');
      this.applyOpen.set(true);
    }
  }

  searchOpenInvoices(term: string): void {
    const inv = this.invoice();
    if (!inv || this.applyLocked()) return;
    this.api.list({ page: 1, pageSize: 20, search: term, filters: [{ field: 'partyId', op: 'eq', value: inv.partyId }, { field: 'isCreditNote', op: 'eq', value: false }, { field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }] })
      .subscribe((r) => this.openInvoices.set(r.items.map((i) => ({ value: i.id, label: this.applyLabel(i) }))));
  }

  apply(): void {
    const credit = this.invoice();
    if (!credit) return;
    if (!this.applyInvoice || !this.applyAmount) { this.message.warning('Choose an invoice and an amount'); return; }
    this.api.applyCredit(credit.id, this.applyInvoice, this.applyAmount).subscribe({
      next: () => { this.applyOpen.set(false); this.done(`Credit note ${credit.number} applied`); },
      error: (e) => this.fail(e),
    });
  }

  reload(): void { this.list.reload(); this.loadStats(); }

  private buildColumns(): TableColumn<InvoiceSummary>[] {
    const hasLinks = Object.keys(this.links()).length > 0;
    // A credit note takes money off the customer's account, so its figures read as negative.
    const money = (f: (r: InvoiceSummary) => number | undefined) => (r: InvoiceSummary) => formatMoney((r.isCreditNote ? -1 : 1) * (f(r) ?? 0), r.currencyCode);
    return [
      { key: 'number', label: 'Number', sortField: 'number', width: '210px' },
      { key: 'invoiceDate', label: 'Date', sortField: 'invoiceDate', width: '110px', filterField: 'invoiceDate' },
      { key: 'partyName', label: 'Customer', value: (r) => r.partyName || '—' },
      ...(hasLinks ? [
        { key: 'lease', label: 'Lease', width: '120px', value: (r: InvoiceSummary) => this.links()[r.id]?.leaseNumber ?? (this.linksLoading() ? '…' : '') },
        { key: 'property', label: 'Property', width: '200px', value: (r: InvoiceSummary) => this.links()[r.id]?.propertyName ?? (this.linksLoading() ? '…' : '') },
      ] : []),
      { key: 'dueDate', label: 'Due', sortField: 'dueDate', width: '150px', filterField: 'dueDate' },
      { key: 'net', label: 'Net', width: '140px', align: 'right', hidden: true, value: money((r) => r.net) },
      { key: 'tax', label: this.taxLabel(), width: '130px', align: 'right', hidden: true, value: money((r) => r.tax) },
      { key: 'total', label: 'Total', sortField: 'total', width: '150px', align: 'right', value: money((r) => r.total) },
      { key: 'outstanding', label: 'Balance', width: '150px', align: 'right', value: (r) => (r.status === 'voided' ? '—' : money((x) => x.outstanding)(r)) },
      { key: 'status', label: 'Status', width: '120px', value: (r) => invoiceStatusLabel(r.status), filterField: 'status', filterOptions: STATUS_OPTIONS.map((s) => ({ value: s, label: invoiceStatusLabel(s) })) },
    ];
  }

  /** Keeps the chosen customer in the options when a new search does not return it. */
  private merge(current: LookupOption[], fresh: LookupOption[]): LookupOption[] {
    const keep = current.filter((c) => c.value === this.form.value.partyId && !fresh.some((f) => f.value === c.value));
    return [...keep, ...fresh];
  }

  private openById(id: string): void {
    this.extras.set(null);
    this.api.detail(id).subscribe((inv) => this.show(inv));
    this.documentsApi.invoiceExtras(id).subscribe((x) => this.extras.set(x));
  }

  /** The party page's New invoice: a new draft for that customer. */
  private startFor(p: PartyDto): void {
    this.extras.set(null);
    this.show(null);
    this.dueDate.remember(p);
    this.partyOptions.set([partyOption(p)]);
    this.form.controls.partyId.setValue(p.id);
  }

  private prefill(src: InvoiceDto, asCredit: boolean): void {
    this.extras.set(null);
    this.show(null);
    this.lines.clear();
    for (const l of src.lines.length ? src.lines : [{}]) this.lines.push(invoiceLineGroup(this.fb, lineSeed(l)));
    this.form.patchValue({
      legalEntityId: src.legalEntityId, partyId: src.partyId,
      isCreditNote: asCredit ? true : src.isCreditNote,
      // No credit-note-for field exists on the invoice, so the original is referenced in the notes.
      notes: asCredit ? `Credit note for invoice ${src.number}` : src.notes ?? '',
    });
    this.partyOptions.set([{ value: src.partyId, label: src.partyName ?? src.partyId }]);
  }

  private show(inv: InvoiceDto | null): void {
    this.invoice.set(inv);
    this.lines.clear();
    for (const l of inv?.lines ?? [{}]) this.lines.push(invoiceLineGroup(this.fb, l));
    this.dueDate.quietly(() => {
      this.form.patchValue({
        legalEntityId: inv?.legalEntityId ?? this.lookups.onlyEntityId(), partyId: inv?.partyId ?? '', isCreditNote: inv?.isCreditNote ?? false, notes: inv?.notes ?? '',
        invoiceDate: inv ? new Date(inv.invoiceDate + 'T00:00:00') : new Date(), dueDate: inv ? new Date(inv.dueDate + 'T00:00:00') : null,
      });
      this.partyOptions.set(inv ? [documentPartyOption(inv)] : []);
      if (!inv) this.searchParties('');
      if (this.editable()) this.form.enable(); else this.form.disable();
    });
    this.dueDate.follow(inv);
    this.drawerOpen.set(true);
  }

  private savedDraft(saved: InvoiceDto, partyId: string | null): void {
    this.saving.set(false);
    this.drawerOpen.set(false);
    this.toast.show(doneText('saved', 'Draft invoice', partyId ? this.partyOptions().find((o) => o.value === partyId)?.label : null), { label: 'Open draft', to: ['/finance/invoices', saved.id] });
    this.reload();
  }

  private issue(saved: InvoiceDto): void {
    this.api.issue(saved.id).subscribe({
      next: (i) => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        for (const w of i.warnings ?? []) this.message.warning(w, { nzDuration: 8000 });
        void this.router.navigate(['/finance/invoices', i.id], { queryParams: { issued: 1 } });
      },
      error: (e) => { this.saving.set(false); this.invoice.set(saved); this.reload(); this.fail(e); },
    });
  }

  private applyLabel(i: { number: string; total: number; paid: number; currencyCode: string }): string {
    return `${i.number} · ${this.i18n.t('ui.common.outstandingLc', 'outstanding')} ${formatMoney(i.total - i.paid, i.currencyCode)}`;
  }

  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.reload(); }
  private loadStats(): void { this.kpis.load(loadDocumentStats((q: ListQuery) => this.api.list(q), 'invoiceDate')); }
  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Could not save the invoice', 'reason')); }
}

/** The editable part of a source invoice's line, for a duplicate or a credit note. */
function lineSeed(l: InvoiceLineSeed): InvoiceLineSeed {
  return { description: l.description, quantity: l.quantity, price: l.price, taxCodeId: l.taxCodeId ?? null };
}
