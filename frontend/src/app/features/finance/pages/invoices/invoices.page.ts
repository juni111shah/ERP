import { doneText } from '@core/format/done-text';
import { SuccessToastService } from '@core/services/success-toast.service';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzModalService } from 'ng-zorro-antd/modal';
import { API_BASE_URL } from '@core/api/api-config';
import { InvoiceExtras, InvoiceExtrasService, InvoiceSheetComponent, daysOverdue, formatMoney, humanizeSource, invoiceStatusLabel, taxNameFor } from '../../components/invoice-sheet/invoice-sheet.component';
import { catchError, forkJoin, of } from 'rxjs';
import { InvoiceDto, InvoiceSummary, InvoicesApi, LegalEntitiesApi, LegalEntityDto, TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { ListQuery } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/format/dates';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { asProblem, controlError, problemMessage } from '@core/http/problem-details';
import { askReason } from '@shared/utils/reason-prompt';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { PartiesApi } from '@core/api/parties.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { AllRows, countOf, countRows, fetchAllRows, moneyByCurrency, startOfYear } from '../../finance-kpis';

const STATUS_OPTIONS = ['draft', 'pendingApproval', 'issued', 'partlyPaid', 'paid', 'voided'];
/** Lease / property behind an invoice, supplied by the real-estate module only when it is installed (the core invoice knows nothing about leases). */
interface LeaseLink { invoiceId: string; leaseId: string; leaseNumber?: string | null; propertyId: string; propertyName?: string | null; unitNumber?: string | null; periodFrom: string; periodTo: string; }

/** Sales invoices and credit notes: draft with a line editor, issue (posts to the ledger), void, and apply a credit note to an invoice. */
@Component({
  selector: 'erp-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent, StatCardComponent, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule, InvoiceSheetComponent],
  styleUrl: './invoices.page.scss',
  templateUrl: './invoices.page.html',
})
export class InvoicesPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly toast = inject(SuccessToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly modal = inject(NzModalService);
  private readonly extrasApi = inject(InvoiceExtrasService);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly taxPage = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] }))), { initialValue: { items: [] as TaxCodeDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly taxes = computed(() => this.taxPage().items.filter((t) => t.isActive));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-invoiceDate' });
  readonly statusOptions = STATUS_OPTIONS;
  readonly statusFilter = signal<string | null>(null);
  readonly billing = signal(false);
  readonly extras = signal<InvoiceExtras | null>(null);
  /** invoice id → lease link; filled only for rows the real-estate module raised */
  readonly links = signal<Record<string, LeaseLink>>({});
  /** True while invoice-links is loading, so Lease/Property show a placeholder instead of a blank cell. */
  readonly linksLoading = signal(false);
  readonly taxRates = computed(() => Object.fromEntries(this.taxPage().items.map((t) => [t.id, t.rate])) as Record<string, number>);
  /** VAT / GST / Sales tax when every legal entity is in one country, else plain "Tax" (same table as the print layout). */
  readonly taxLabel = computed(() => { const c = [...new Set(this.entities().map((e) => e.countryCode))]; return c.length === 1 ? taxNameFor(c[0]) : 'Tax'; });
  readonly objectKeys = Object.keys;
  readonly columns = computed((): TableColumn<InvoiceSummary>[] => {
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
  });
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;
  private readonly statsData = signal<{ open: AllRows<InvoiceSummary>; year: AllRows<InvoiceSummary>; drafts: number } | null>(null);
  readonly statsLoading = signal(true);
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = computed(() => (this.statsLoading() ? null : 'Could not load this figure'));
  /** The figures for the whole workspace, not the page on screen: open invoices and this year's invoices are read in full, drafts are counted by the server. */
  readonly stats = computed(() => {
    const d = this.statsData();
    if (!d) return null;
    const cur = this.workspaceCurrency();
    const open = d.open.items.filter((r) => !r.isCreditNote);
    const credits = d.open.items.filter((r) => r.isCreditNote);
    const late = open.filter((r) => daysOverdue(r) > 0);
    const yearInvoices = d.year.items.filter((r) => !r.isCreditNote).length;
    const partial = d.open.capped || d.year.capped ? ' (latest rows only)' : '';
    return {
      invoiced: moneyByCurrency(d.year.items, (r) => (r.isCreditNote ? -r.total : r.total), cur),
      invoicedCaption: `${countOf(yearInvoices, 'invoice')}, less credit notes${partial}`,
      outstanding: moneyByCurrency(open, (r) => r.outstanding, cur),
      outstandingCaption: credits.length
        ? `${open.length} open · ${moneyByCurrency(credits, (r) => r.outstanding, cur)} in credits`
        : countOf(open.length, 'open invoice'),
      overdue: moneyByCurrency(late, (r) => r.outstanding, cur),
      overdueCount: late.length,
      overdueCaption: late.length ? countOf(late.length, 'invoice') : 'Nothing overdue',
      drafts: d.drafts,
    };
  });
  /** A new document opened from a link can beat the legal entity list; fill the only choice once it arrives. */
  private readonly defaultEntity = effect(() => {
    const entities = this.entities();
    if (entities.length === 1 && this.drawerOpen() && !this.invoice() && !this.form.controls.legalEntityId.value) this.form.controls.legalEntityId.setValue(entities[0].id);
  });
  private readonly loadLinks = effect(() => {
    const ids = this.list.items().filter((r) => (r.sourceType ?? '').toLowerCase().startsWith('realestate.')).map((r) => r.id);
    if (!ids.length) { this.links.set({}); return; }
    this.linksLoading.set(true);
    this.http.get<LeaseLink[]>(`${this.apiBase}/m/realestate/billing/invoice-links`, { params: { ids } }).pipe(catchError(() => of([] as LeaseLink[])))
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
    isCreditNote: [false], notes: [''], lines: this.fb.array<unknown>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  /** Payment terms (days) per party id; null = the party has no terms. */
  private readonly partyTerms = new Map<string, number | null>();
  /** True once the user picked a due date themselves; then invoice date / customer changes no longer move it. */
  private dueDateTouched = false;
  private syncingDueDate = false;

  constructor() {
    this.loadStats();
    const c = this.form.controls;
    c.partyId.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => { if (!this.syncingDueDate && id) this.withTerms(id, () => this.recalcDueDate()); });
    c.invoiceDate.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => { if (!this.syncingDueDate) this.recalcDueDate(); });
    c.dueDate.valueChanges.pipe(takeUntilDestroyed()).subscribe((d) => {
      if (this.syncingDueDate) return;
      // Clearing the due date hands it back to the payment terms (the API applies them to an empty due date).
      this.dueDateTouched = !!d;
      if (!d) this.recalcDueDate();
    });
  }

  /** Mirrors the API rule (Invoicing.cs): due date = invoice date + the customer's PaymentTermsDays. No terms: leave the due date alone. */
  private termsDueDate(invoiceDate: Date | null | undefined, partyId: string | null | undefined): Date | null {
    const days = partyId ? this.partyTerms.get(partyId) : undefined;
    if (!invoiceDate || days === undefined || days === null) return null;
    return new Date(invoiceDate.getFullYear(), invoiceDate.getMonth(), invoiceDate.getDate() + days);
  }

  private recalcDueDate(): void {
    if (this.dueDateTouched || !this.editable()) return;
    const v = this.form.getRawValue();
    const due = this.termsDueDate(v.invoiceDate, v.partyId);
    if (!due) return;
    this.syncingDueDate = true;
    this.form.controls.dueDate.setValue(due);
    this.syncingDueDate = false;
  }

  /** Runs `then` once the party's payment terms are known (from the search results or a single lookup). */
  private withTerms(partyId: string, then: () => void): void {
    if (this.partyTerms.has(partyId)) { then(); return; }
    this.parties.get(partyId).subscribe({
      next: (p) => { this.partyTerms.set(partyId, p.paymentTermsDays ?? null); if (this.form.controls.partyId.value === partyId) then(); },
      error: () => undefined,
    });
  }
  readonly totals = computed(() => {
    const lines = (this.value().lines ?? []) as { quantity?: number | null; price?: number | null; taxCodeId?: string | null }[];
    const rate = (id?: string | null) => this.taxes().find((t) => t.id === id)?.rate ?? 0;
    const net = lines.reduce((s, l) => s + Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100, 0);
    const tax = lines.reduce((s, l) => s + Math.round(Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100 * rate(l.taxCodeId)) / 100, 0);
    return { net, tax };
  });
  readonly sheetData = computed(() => { const i = this.invoice(); return i ? { ...i, lines: i.lines.map((l) => ({ ...l, taxCodeId: l.taxCodeId ?? null })) } : null; });
  readonly editable = computed(() => !this.invoice() || this.invoice()!.status === 'draft');
  readonly title = computed(() => this.invoice() ? `${this.invoice()!.isCreditNote ? 'Credit note' : 'Invoice'} ${this.invoice()!.number}` : 'New invoice');

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  /** A line field the user has tried to save empty; these sit outside nz-form-control, so they need their own red state. */
  lineStatus(line: unknown, name: string): 'error' | '' { const c = (line as { get(n: string): { invalid: boolean; touched: boolean } | null }).get(name); return c?.invalid && c.touched ? 'error' : ''; }
  /** Line totals in the currency of the chosen legal entity. */
  lineMoney(v: number): string { return formatMoney(v, this.entities().find((e) => e.id === this.value().legalEntityId)?.baseCurrency ?? this.workspaceCurrency()); }
  label(s: string): string { return invoiceStatusLabel(s); }
  overdue(r: InvoiceSummary): number { return daysOverdue(r); }
  source(r: InvoiceSummary): string { return humanizeSource(r.sourceType); }
  link(r: InvoiceSummary): LeaseLink | undefined { return this.links()[r.id]; }

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
        this.http.post<{ invoiced: number }>(`${this.apiBase}/m/realestate/billing/run`, { asOf: toIsoDate(new Date()) }).subscribe({
          next: (r) => { this.billing.set(false); this.message.success(`${this.i18n.t('ui.msg.billingRunCreated', 'Billing run created')} ${r?.invoiced ?? 0} ${this.i18n.t('ui.msg.invoicesParen', 'invoice(s)')}`); this.reload(); },
          error: (e) => { this.billing.set(false); this.fail(e); },
        });
      },
    });
  }
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: Partial<{ description: string; quantity: number; price: number; taxCodeId: string }>) {
    return this.fb.group({ description: [l?.description ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => { r.items.forEach((p) => this.partyTerms.set(p.id, p.paymentTermsDays ?? null)); this.partyOptions.update((cur) => this.merge(cur, r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` })))); });
  }
  private merge(current: LookupOption[], fresh: LookupOption[]): LookupOption[] {
    const keep = current.filter((c) => c.value === this.form.value.partyId && !fresh.some((f) => f.value === c.value));
    return [...keep, ...fresh];
  }

  /** `?open=<id>` opens a drawer; `?duplicate=<id>` / `?creditFor=<id>` start a new draft prefilled from that invoice (detail page actions); `?partyId=<id>` starts one for that customer. */
  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    if (q.get('new')) this.show(null);
    const open = q.get('open'), dup = q.get('duplicate'), credit = q.get('creditFor'), party = q.get('partyId');
    if (open) this.open({ id: open } as never);
    else if (dup || credit) this.api.detail((dup ?? credit)!).subscribe((src) => this.prefill(src, !!credit && !dup));
    else if (party) this.parties.get(party).subscribe((p) => {
      this.extras.set(null);
      this.show(null);
      this.partyTerms.set(p.id, p.paymentTermsDays ?? null);
      this.partyOptions.set([{ value: p.id, label: `${p.code} · ${p.name}` }]);
      this.form.controls.partyId.setValue(p.id);
    });
  }

  private prefill(src: InvoiceDto, asCredit: boolean): void {
    this.extras.set(null);
    this.show(null);
    this.lines.clear();
    for (const l of src.lines.length ? src.lines : [{} as never]) this.lines.push(this.newLine({ description: l.description, quantity: l.quantity, price: l.price, taxCodeId: l.taxCodeId ?? null } as never) as never);
    this.form.patchValue({
      legalEntityId: src.legalEntityId, partyId: src.partyId,
      isCreditNote: asCredit ? true : src.isCreditNote,
      // No credit-note-for field exists on the invoice, so the original is referenced in the notes.
      notes: asCredit ? `Credit note for invoice ${src.number}` : src.notes ?? '',
    });
    this.partyOptions.set([{ value: src.partyId, label: src.partyName ?? src.partyId }]);
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/invoices', id]); }
  printInvoice(id: string): void { window.open(`/print/invoice/${id}`, '_blank'); }

  open(row: InvoiceSummary | null): void {
    if (!row) { this.extras.set(null); this.show(null); return; }
    this.extras.set(null);
    this.api.detail(row.id).subscribe((inv) => this.show(inv));
    this.extrasApi.load(row.id).subscribe((x) => this.extras.set(x));
  }

  private show(inv: InvoiceDto | null): void {
    this.invoice.set(inv);
    this.lines.clear();
    for (const l of inv?.lines ?? [{} as never]) this.lines.push(this.newLine(l as never) as never);
    const entities = this.entities();
    this.syncingDueDate = true;
    this.form.patchValue({
      legalEntityId: inv?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), partyId: inv?.partyId ?? '', isCreditNote: inv?.isCreditNote ?? false, notes: inv?.notes ?? '',
      invoiceDate: inv ? new Date(inv.invoiceDate + 'T00:00:00') : new Date(), dueDate: inv ? new Date(inv.dueDate + 'T00:00:00') : null,
    });
    this.partyOptions.set(inv ? [{ value: inv.partyId, label: inv.partyName ?? inv.partyId }] : []);
    if (!inv) this.searchParties('');
    this.editable() ? this.form.enable() : this.form.disable();
    this.syncingDueDate = false;
    // A new invoice follows the terms; an opened draft keeps its due date as an override only if it differs from date + terms.
    this.dueDateTouched = !!inv;
    if (inv && this.editable()) this.withTerms(inv.partyId, () => {
      // Same rule as the API: no terms counts as 0 days, so such a draft is "automatic" when due = invoice date.
      const v = this.form.getRawValue(), days = this.partyTerms.get(inv.partyId) ?? 0, d = v.invoiceDate;
      const auto = d ? toIsoDate(new Date(d.getFullYear(), d.getMonth(), d.getDate() + days)) : null;
      this.dueDateTouched = !v.dueDate || toIsoDate(v.dueDate) !== auto;
    });
    this.drawerOpen.set(true);
  }

  save(issue: boolean): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, partyId: v.partyId, invoiceDate: toIsoDate(v.invoiceDate!), dueDate: v.dueDate ? toIsoDate(v.dueDate) : null, isCreditNote: v.isCreditNote, notes: v.notes || null,
      lines: (v.lines as { description: string; quantity: number; price: number; taxCodeId: string | null }[]).map((l) => ({ ...l, taxCodeId: l.taxCodeId || null })),
    };
    this.saving.set(true);
    this.api.save(this.invoice()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!issue) { this.saving.set(false); this.drawerOpen.set(false); this.toast.show(doneText('saved', 'Draft invoice', v.partyId ? this.partyOptions().find((o) => o.value === v.partyId)?.label : null), { label: 'Open draft', to: ['/finance/invoices', saved.id] }); this.reload(); return; }
        this.api.issue(saved.id).subscribe({ next: (i) => { this.saving.set(false); this.drawerOpen.set(false); for (const w of i.warnings ?? []) this.message.warning(w, { nzDuration: 8000 }); void this.router.navigate(['/finance/invoices', i.id], { queryParams: { issued: 1 } }); }, error: (e) => { this.saving.set(false); this.invoice.set(saved); this.reload(); this.fail(e); } });
      },
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

  private applyLabel(i: { number: string; total: number; paid: number; currencyCode: string }): string {
    return `${i.number} · ${this.i18n.t('ui.common.outstandingLc', 'outstanding')} ${formatMoney(i.total - i.paid, i.currencyCode)}`;
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

  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.reload(); }
  reload(): void { this.list.reload(); this.loadStats(); }

  private loadStats(): void {
    const source = (q: ListQuery) => this.api.list(q);
    forkJoin({
      open: fetchAllRows(source, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }], 'dueDate'),
      year: fetchAllRows(source, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid', 'paid'] }, { field: 'invoiceDate', op: 'gte', value: startOfYear() }], '-invoiceDate'),
      drafts: countRows(source, [{ field: 'status', op: 'eq', value: 'draft' }]),
    }).subscribe({
      next: (d) => { this.statsData.set(d); this.statsLoading.set(false); },
      error: () => { this.statsData.set(null); this.statsLoading.set(false); },
    });
  }
  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Could not save the invoice', 'reason')); }
}
