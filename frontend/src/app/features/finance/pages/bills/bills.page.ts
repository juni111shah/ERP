import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { BillDto, BillSummary, BillsApi } from '@core/api/admin.api';
import { ListQuery } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { controlError, problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { SuccessToastService, ToastAction } from '@core/services/success-toast.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { askReason } from '@shared/utils/reason-prompt';
import { TermsDueDate } from '../../finance-due-date';
import { formatMoney } from '../../finance-format';
import { KpiStats } from '../../finance-kpis';
import { billLineGroup, lineFieldStatus, taxedLineTotals, withTaxCodes } from '../../finance-lines';
import { FinanceLookups, documentPartyOption, partyOption } from '../../finance-lookups';
import { DocumentStatsData, billStats, loadDocumentStats } from '../../finance-stats';

const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid', pendingApproval: 'Needs approval', issued: 'Posted' };
const STATUSES = ['draft', 'pendingApproval', 'issued', 'partlyPaid', 'paid', 'voided'];

/** A bill status as the bills list words it ("Posted", "Needs approval"...). */
const billStatusLabel = (s: string): string => STATUS_LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1);

/** Vendor bills: draft with a line editor, post to the ledger (after approval when a rule applies), void. */
@Component({
  selector: 'erp-bills',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
    PagedTableComponent,
    StatCardComponent,
    StatusTagComponent,
    HasPermissionDirective,
  ],
  providers: [FinanceLookups],
  styleUrl: './bills.page.scss',
  templateUrl: './bills.page.html',
})
export class BillsPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(BillsApi);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly toast = inject(SuccessToastService);
  private readonly fb = inject(FormBuilder);
  private readonly lookups = inject(FinanceLookups).load({ entities: true, taxCodes: true, accounts: true });

  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly entities = this.lookups.entities;
  readonly taxes = this.lookups.activeTaxes;
  /** The accounts a bill line can be booked to: postable expense and asset accounts. */
  readonly accounts = computed(() => this.lookups.accounts().filter((a) => a.isPostable && a.isActive && (a.type === 'expense' || a.type === 'asset')));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-billDate' });

  private readonly kpis = new KpiStats<DocumentStatsData<BillSummary>>();
  readonly statsLoading = this.kpis.loading;
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = this.kpis.error;
  /** The figures for the whole workspace, not the page on screen: open bills and this year's bills are read in full, drafts are counted by the server. */
  readonly stats = computed(() => { const d = this.kpis.data(); return d ? billStats(d, this.currency()) : null; });

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
    { key: 'status', label: 'Status', width: '130px', filterField: 'status', filterOptions: STATUSES.map((s) => ({ value: s, label: billStatusLabel(s) })) },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly bill = signal<BillDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], vendorReference: [''], billDate: [null as Date | null, Validators.required], dueDate: [null as Date | null],
    lines: this.fb.array<ReturnType<typeof billLineGroup>>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly totals = computed(() => taxedLineTotals(this.value().lines ?? [], this.taxes()));
  readonly editable = computed(() => !this.bill() || this.bill()!.status === 'draft' || this.bill()!.status === 'pendingApproval');
  /** Due date = bill date + the vendor's terms; same pattern as the invoices drawer. */
  private readonly dueDate: TermsDueDate;

  constructor() {
    this.loadStats();
    const c = this.form.controls;
    this.dueDate = new TermsDueDate({ party: c.partyId, date: c.billDate, due: c.dueDate }, this.parties, this.editable);
  }

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  /** A line field the user has tried to save empty; these sit outside nz-form-control, so they need their own red state. */
  lineStatus(line: AbstractControl, name: string): 'error' | '' { return lineFieldStatus(line, name); }
  /** Line totals in the currency of the chosen legal entity. */
  lineMoney(v: number): string { return formatMoney(v, this.entities().find((e) => e.id === this.value().legalEntityId)?.baseCurrency ?? this.currency()); }
  label(s: string): string { return billStatusLabel(s); }
  err(name: string): string { return controlError(this.form.get(name)); }

  addLine(): void { this.lines.push(billLineGroup(this.fb)); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.lookups.findParties(term).subscribe((found) => {
      found.forEach((p) => this.dueDate.remember(p));
      this.partyOptions.set(found.map(partyOption));
    });
  }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions); `?partyId=<id>` starts a new bill. */
  ngOnInit(): void {
    const id = this.route.snapshot.queryParamMap.get('open');
    const party = this.route.snapshot.queryParamMap.get('partyId');
    if (id) this.api.detail(id).subscribe((b) => this.show(b));
    // The party page's New bill starts a draft for that vendor.
    else if (party) this.parties.get(party).subscribe((p) => {
      this.open(null);
      this.dueDate.remember(p);
      this.partyOptions.set([partyOption(p)]);
      this.form.controls.partyId.setValue(p.id);
    });
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/bills', id]); }
  printDoc(id: string): void { window.open(`/print/bill/${id}`, '_blank'); }

  open(row: BillSummary | null): void {
    if (!row) { this.show(null); return; }
    this.api.detail(row.id).subscribe((b) => this.show(b));
  }

  save(post: boolean): void {
    if (this.form.invalid) { revealErrors(this.form, { touchAll: true }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, partyId: v.partyId, vendorReference: v.vendorReference || null, billDate: toIsoDate(v.billDate!), dueDate: v.dueDate ? toIsoDate(v.dueDate) : null,
      lines: withTaxCodes(v.lines),
    };
    this.saving.set(true);
    this.api.save(this.bill()?.id ?? null, body).subscribe({
      next: (saved) => (post ? this.post(saved) : this.done(doneText('saved', 'Draft bill', saved.number))),
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

  reload(): void { this.list.reload(); this.loadStats(); }

  private show(b: BillDto | null): void {
    this.bill.set(b);
    this.lines.clear();
    for (const l of b?.lines ?? [{}]) this.lines.push(billLineGroup(this.fb, l));
    this.dueDate.quietly(() => {
      this.form.patchValue({
        legalEntityId: b?.legalEntityId ?? this.lookups.onlyEntityId(), partyId: b?.partyId ?? '', vendorReference: b?.vendorReference ?? '',
        billDate: b ? new Date(b.billDate + 'T00:00:00') : new Date(), dueDate: b ? new Date(b.dueDate + 'T00:00:00') : null,
      });
      this.partyOptions.set(b ? [documentPartyOption(b)] : []);
      if (!b) this.searchParties('');
      if (this.editable()) this.form.enable(); else this.form.disable();
    });
    this.dueDate.follow(b);
    this.drawerOpen.set(true);
  }

  /** Posts a saved draft; a bill under an approval rule goes for approval first. */
  private post(saved: BillDto): void {
    this.api.post(saved.id).subscribe({
      next: (b) => b.status === 'pendingApproval'
        ? this.done('Sent for approval. Post it again once it is approved.')
        : this.done(`${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${b.number}`, { label: 'View bill', to: ['/finance/bills', b.id] }),
      error: (e) => { this.saving.set(false); this.bill.set(saved); this.reload(); this.fail(e); },
    });
  }

  private done(text: string, action?: ToastAction): void { this.saving.set(false); this.drawerOpen.set(false); this.toast.show(text, action); this.reload(); }
  private loadStats(): void { this.kpis.load(loadDocumentStats((q: ListQuery) => this.api.list(q), 'billDate')); }
  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Could not save the bill', 'reason')); }
}
