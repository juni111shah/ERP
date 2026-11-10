import { doneText } from '@core/format/done-text';
import { SuccessToastService, ToastAction } from '@core/services/success-toast.service';
import { ActivatedRoute, Router } from '@angular/router';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '@shared/utils/reason-prompt';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, forkJoin, of } from 'rxjs';
import { AccountDto, AccountsApi, BillDto, BillSummary, BillsApi, LegalEntitiesApi, LegalEntityDto, TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/format/dates';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { asProblem, controlError, problemMessage } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { PartiesApi } from '@core/api/parties.api';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ListQuery } from '@core/api/api.models';
import { daysOverdue, formatMoney } from '../../components/invoice-sheet/invoice-sheet.component';
import { AllRows, countOf, countRows, fetchAllRows, moneyByCurrency, startOfYear } from '../../finance-kpis';

const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid', pendingApproval: 'Needs approval', issued: 'Posted' };

/** Vendor bills: draft with a line editor, post to the ledger (after approval when a rule applies), void. */
@Component({
  selector: 'erp-bills',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, StatusTagComponent, NzToolTipModule, RevealDirective, ReactiveFormsModule, NzModalModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule, NzInputNumberModule, NzSelectModule, NzTagModule],
  styleUrl: './bills.page.scss',
  templateUrl: './bills.page.html',
})
export class BillsPage {
  private readonly currencyService = inject(WorkspaceCurrencyService);
  readonly currency = this.currencyService.currency;
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(BillsApi);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly toast = inject(SuccessToastService);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly taxPage = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] }))), { initialValue: { items: [] as TaxCodeDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly taxes = computed(() => this.taxPage().items.filter((t) => t.isActive));
  readonly accounts = computed(() => this.accountPage().items.filter((a) => a.isPostable && a.isActive && (a.type === 'expense' || a.type === 'asset')));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-billDate' });

  private readonly statsData = signal<{ open: AllRows<BillSummary>; year: AllRows<BillSummary>; drafts: number } | null>(null);
  readonly statsLoading = signal(true);
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = computed(() => (this.statsLoading() ? null : 'Could not load this figure'));
  /** The figures for the whole workspace, not the page on screen: open bills and this year's bills are read in full, drafts are counted by the server. */
  readonly stats = computed(() => {
    const d = this.statsData();
    if (!d) return null;
    const cur = this.currency();
    const late = d.open.items.filter((b) => daysOverdue(b) > 0);
    return {
      billed: moneyByCurrency(d.year.items, (b) => b.total, cur),
      billedCaption: countOf(d.year.items.length, 'bill') + (d.year.capped ? ' (latest rows only)' : ''),
      outstanding: moneyByCurrency(d.open.items, (b) => b.outstanding, cur),
      outstandingCaption: d.open.items.length ? countOf(d.open.items.length, 'open bill') : 'Everything is paid',
      overdue: moneyByCurrency(late, (b) => b.outstanding, cur),
      overdueCount: late.length,
      overdueCaption: late.length ? countOf(late.length, 'bill') : 'Nothing overdue',
      drafts: d.drafts,
    };
  });
  /** A new document opened from a link can beat the legal entity list; fill the only choice once it arrives. */
  private readonly defaultEntity = effect(() => {
    const entities = this.entities();
    if (entities.length === 1 && this.drawerOpen() && !this.bill() && !this.form.controls.legalEntityId.value) this.form.controls.legalEntityId.setValue(entities[0].id);
  });
  readonly columns: TableColumn<BillSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'partyName', label: 'Vendor' },
    { key: 'vendorReference', label: 'Reference', width: '140px' },
    { key: 'billDate', label: 'Date', sortField: 'billDate', width: '120px', filterField: 'billDate' },
    { key: 'dueDate', label: 'Due', sortField: 'dueDate', width: '120px', filterField: 'dueDate' },
    { key: 'total', label: 'Total', sortField: 'total', width: '150px', align: 'right', value: (b) => formatMoney(b.total, b.currencyCode) },
    { key: 'outstanding', label: 'Outstanding', width: '150px', align: 'right', value: (b) => (b.status === 'voided' ? '—' : formatMoney(b.outstanding, b.currencyCode)) },
    { key: 'status', label: 'Status', width: '130px', filterField: 'status', filterOptions: ['draft', 'pendingApproval', 'issued', 'partlyPaid', 'paid', 'voided'].map((s) => ({ value: s, label: this.label(s) })) },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly bill = signal<BillDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], vendorReference: [''], billDate: [null as Date | null, Validators.required], dueDate: [null as Date | null],
    lines: this.fb.array<unknown>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  /** Payment terms (days) per vendor id; null = the vendor has no terms. Same pattern as the invoices drawer. */
  private readonly partyTerms = new Map<string, number | null>();
  /** True once the user picked a due date themselves; then bill date / vendor changes no longer move it. */
  private dueDateTouched = false;
  private syncingDueDate = false;

  constructor() {
    this.loadStats();
    const c = this.form.controls;
    c.partyId.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => { if (!this.syncingDueDate && id) this.withTerms(id, () => this.recalcDueDate()); });
    c.billDate.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => { if (!this.syncingDueDate) this.recalcDueDate(); });
    c.dueDate.valueChanges.pipe(takeUntilDestroyed()).subscribe((d) => {
      if (this.syncingDueDate) return;
      // Clearing the due date hands it back to the payment terms (the API applies them to an empty due date).
      this.dueDateTouched = !!d;
      if (!d) this.recalcDueDate();
    });
  }

  /** Mirrors the API rule (BillFeatures.cs): due date = bill date + the vendor's PaymentTermsDays. No terms: leave the due date alone. */
  private termsDueDate(billDate: Date | null | undefined, partyId: string | null | undefined): Date | null {
    const days = partyId ? this.partyTerms.get(partyId) : undefined;
    if (!billDate || days === undefined || days === null) return null;
    return new Date(billDate.getFullYear(), billDate.getMonth(), billDate.getDate() + days);
  }

  private recalcDueDate(): void {
    if (this.dueDateTouched || !this.editable()) return;
    const v = this.form.getRawValue();
    const due = this.termsDueDate(v.billDate, v.partyId);
    if (!due) return;
    this.syncingDueDate = true;
    this.form.controls.dueDate.setValue(due);
    this.syncingDueDate = false;
  }

  /** Runs `then` once the vendor's payment terms are known (from the search results or a single lookup). */
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
  readonly editable = computed(() => !this.bill() || this.bill()!.status === 'draft' || this.bill()!.status === 'pendingApproval');

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  /** A line field the user has tried to save empty; these sit outside nz-form-control, so they need their own red state. */
  lineStatus(line: unknown, name: string): 'error' | '' { const c = (line as { get(n: string): { invalid: boolean; touched: boolean } | null }).get(name); return c?.invalid && c.touched ? 'error' : ''; }
  /** Line totals in the currency of the chosen legal entity. */
  lineMoney(v: number): string { return formatMoney(v, this.entities().find((e) => e.id === this.value().legalEntityId)?.baseCurrency ?? this.currency()); }
  label(s: string): string { return STATUS_LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1); }
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: Partial<{ description: string; accountId: string; quantity: number; price: number; taxCodeId: string }>) {
    return this.fb.group({ description: [l?.description ?? '', Validators.required], accountId: [l?.accountId ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => { r.items.forEach((p) => this.partyTerms.set(p.id, p.paymentTermsDays ?? null)); this.partyOptions.set(r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))); });
  }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions); `?partyId=<id>` starts a new bill. */
  ngOnInit(): void {
    const id = this.route.snapshot.queryParamMap.get('open');
    const party = this.route.snapshot.queryParamMap.get('partyId');
    if (id) this.open({ id } as never);
    // The party page's New bill starts a draft for that vendor.
    else if (party) this.parties.get(party).subscribe((p) => {
      this.open(null);
      this.partyTerms.set(p.id, p.paymentTermsDays ?? null);
      this.partyOptions.set([{ value: p.id, label: `${p.code} · ${p.name}` }]);
      this.form.controls.partyId.setValue(p.id);
    });
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/bills', id]); }
  printDoc(id: string): void { window.open(`/print/bill/${id}`, '_blank'); }

  open(row: BillSummary | null): void {
    if (!row) { this.show(null); return; }
    this.api.detail(row.id).subscribe((b) => this.show(b));
  }

  private show(b: BillDto | null): void {
    this.bill.set(b);
    this.lines.clear();
    for (const l of b?.lines ?? [{}]) this.lines.push(this.newLine(l) as never);
    const entities = this.entities();
    this.syncingDueDate = true;
    this.form.patchValue({
      legalEntityId: b?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), partyId: b?.partyId ?? '', vendorReference: b?.vendorReference ?? '',
      billDate: b ? new Date(b.billDate + 'T00:00:00') : new Date(), dueDate: b ? new Date(b.dueDate + 'T00:00:00') : null,
    });
    this.partyOptions.set(b ? [{ value: b.partyId, label: b.partyName ?? b.partyId }] : []);
    if (!b) this.searchParties('');
    this.editable() ? this.form.enable() : this.form.disable();
    this.syncingDueDate = false;
    // A new bill follows the terms; an opened draft keeps its due date as an override only if it differs from date + terms.
    this.dueDateTouched = !!b;
    if (b && this.editable()) this.withTerms(b.partyId, () => {
      // Same rule as the API: no terms counts as 0 days, so such a draft is "automatic" when due = bill date.
      const v = this.form.getRawValue(), days = this.partyTerms.get(b.partyId) ?? 0, d = v.billDate;
      const auto = d ? toIsoDate(new Date(d.getFullYear(), d.getMonth(), d.getDate() + days)) : null;
      this.dueDateTouched = !v.dueDate || toIsoDate(v.dueDate) !== auto;
    });
    this.drawerOpen.set(true);
  }

  save(post: boolean): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, partyId: v.partyId, vendorReference: v.vendorReference || null, billDate: toIsoDate(v.billDate!), dueDate: v.dueDate ? toIsoDate(v.dueDate) : null,
      lines: (v.lines as { description: string; accountId: string; quantity: number; price: number; taxCodeId: string | null }[]).map((l) => ({ ...l, taxCodeId: l.taxCodeId || null })),
    };
    this.saving.set(true);
    this.api.save(this.bill()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!post) { this.done(doneText('saved', 'Draft bill', saved.number)); return; }
        this.api.post(saved.id).subscribe({
          next: (b) => this.done(b.status === 'pendingApproval' ? 'Sent for approval. Post it again once it is approved.' : `${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${b.number}`, b.status === 'pendingApproval' ? undefined : { label: 'View bill', to: ['/finance/bills', b.id] }),
          error: (e) => { this.saving.set(false); this.bill.set(saved); this.reload(); this.fail(e); },
        });
      },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  voidDocument(): void {
    const b = this.bill();
    if (!b) return;
    // Same required-reason prompt as the detail page; the void endpoint takes no body, so the reason is not sent.
    void askReason(this.modal, 'Void this bill?', 'Void Bill', 'This cannot be undone. The bill will be marked as voided.').then((reason) => {
      if (reason) this.api.void(b.id, reason).subscribe({ next: () => this.done(doneText('voided', 'Bill', b.number)), error: (e) => this.fail(e) });
    });
  }

  remove(): void {
    const b = this.bill();
    if (!b) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft bill?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(b.id).subscribe({ next: () => this.done(doneText('deleted', 'Draft bill', b.number)), error: (e) => this.fail(e) }),
    });
  }

  private done(text: string, action?: ToastAction): void { this.saving.set(false); this.drawerOpen.set(false); this.toast.show(text, action); this.reload(); }
  reload(): void { this.list.reload(); this.loadStats(); }

  private loadStats(): void {
    const source = (q: ListQuery) => this.api.list(q);
    forkJoin({
      open: fetchAllRows(source, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }], 'dueDate'),
      year: fetchAllRows(source, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid', 'paid'] }, { field: 'billDate', op: 'gte', value: startOfYear() }], '-billDate'),
      drafts: countRows(source, [{ field: 'status', op: 'eq', value: 'draft' }]),
    }).subscribe({
      next: (d) => { this.statsData.set(d); this.statsLoading.set(false); },
      error: () => { this.statsData.set(null); this.statsLoading.set(false); },
    });
  }
  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Could not save the bill', 'reason')); }
}
