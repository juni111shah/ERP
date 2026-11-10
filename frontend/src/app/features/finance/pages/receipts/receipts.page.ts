import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { InvoiceSummary, InvoicesApi, ReceiptDto, ReceiptSummary, ReceiptsApi, SETTLEMENT_METHODS } from '@core/api/admin.api';
import { ListQuery } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { controlError, problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { askReason } from '@shared/utils/reason-prompt';
import { formatMoney, isOpenStatus } from '../../finance-format';
import { AllRows, KpiStats } from '../../finance-kpis';
import { FinanceLookups, documentPartyOption, mainBankAccount, partyOption } from '../../finance-lookups';
import { allocatedAmounts, fillLateDefaults, openDocumentsQuery } from '../../finance-settlements';
import { loadSettlementStats, settlementStats } from '../../finance-stats';

/** Customer receipts: record money received, settle open invoices at the same time (or later), and void. */
@Component({
  selector: 'erp-receipts',
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
  ],
  providers: [FinanceLookups],
  styleUrl: './receipts.page.scss',
  templateUrl: './receipts.page.html',
})
export class ReceiptsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ReceiptsApi);
  private readonly router = inject(Router);
  private readonly modal = inject(NzModalService);
  private readonly invoices = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly lookups = inject(FinanceLookups).load({ entities: true, accounts: true });
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;

  readonly entities = this.lookups.entities;
  readonly banks = this.lookups.cashAndBankAccounts;
  readonly methods = SETTLEMENT_METHODS;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });
  readonly columns: TableColumn<ReceiptSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'partyName', label: 'Customer' },
    { key: 'date', label: 'Date', sortField: 'date', width: '120px', filterField: 'date' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '130px', align: 'right', value: (r) => formatMoney(r.amount, r.currencyCode) },
    { key: 'unallocated', label: 'Unallocated', width: '130px', align: 'right', value: (r) => formatMoney(r.unallocated, r.currencyCode) },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: [{ value: 'issued', label: 'Posted' }, { value: 'voided', label: 'Voided' }] },
  ];

  private readonly kpis = new KpiStats<AllRows<ReceiptSummary>>();
  readonly statsLoading = this.kpis.loading;
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = this.kpis.error;
  /** The figures for the whole workspace, not the page on screen: every posted receipt is read, newest first. */
  readonly stats = computed(() => { const d = this.kpis.data(); return d ? settlementStats(d, this.workspaceCurrency(), 'receipt', 'On account') : null; });

  readonly drawerOpen = signal(false);
  /** Arriving from an invoice or bill opens the form before the legal entities and bank accounts have loaded; fill the defaults once they arrive. */
  private readonly lateDefaults = effect(() => {
    const entities = this.entities(), banks = this.banks();
    if (!this.drawerOpen() || this.receipt()) return;
    fillLateDefaults(this.form.controls, entities, banks);
  });
  readonly saving = signal(false);
  readonly receipt = signal<ReceiptDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly openInvoices = signal<InvoiceSummary[]>([]);
  readonly amounts = signal<Record<string, number>>({});
  readonly editableAllocation = computed(() => !this.receipt() || (this.receipt()!.status === 'issued' && this.receipt()!.unallocated > 0));
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], date: [null as Date | null, Validators.required], method: ['bankTransfer', Validators.required],
    bankAccountId: ['', Validators.required], amount: [null as number | null, [Validators.required, Validators.min(0.01)]], reference: [''], autoAllocate: [false],
  });

  readonly money = formatMoney;
  err(name: string): string { return controlError(this.form.get(name)); }
  setAmount(id: string, v: number | null): void { this.amounts.update((a) => ({ ...a, [id]: v ?? 0 })); }

  reload(): void { this.list.reload(); this.loadStats(); }

  searchParties(term: string): void {
    this.lookups.findParties(term).subscribe((found) => this.partyOptions.set(found.map(partyOption)));
  }

  partyChanged(partyId: string | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!partyId) return;
    this.loadOpen(partyId);
  }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions). */
  ngOnInit(): void {
    this.loadStats();
    const q = this.route.snapshot.queryParamMap;
    const id = q.get('open');
    if (id) this.open({ id } as ReceiptSummary);
    // `?partyId=&invoiceId=` (invoice detail's Record Payment) starts a new receipt for that customer, allocated to the invoice's balance.
    if (q.get('new')) this.open(null);
    const partyId = q.get('partyId');
    if (!id && partyId) this.startFor(partyId, q.get('invoiceId'));
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/receipts', id]); }
  printDoc(id: string): void { window.open(`/print/receipt/${id}`, '_blank'); }

  open(row: ReceiptSummary | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!row) { this.openNew(); return; }
    this.api.detail(row.id).subscribe((r) => this.show(r));
  }

  create(): void {
    if (this.form.invalid) { revealErrors(this.form, { touchAll: true }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.create({ ...v, date: toIsoDate(v.date!), reference: v.reference || null, allocations: this.allocations() }).subscribe({
      next: (r) => { this.saving.set(false); this.drawerOpen.set(false); void this.router.navigate(['/finance/receipts', (r as ReceiptDto).id], { queryParams: { recorded: 1 } }); },
      error: (e) => this.fail(e),
    });
  }

  allocate(): void {
    const r = this.receipt();
    const allocations = this.allocations();
    if (!r || allocations.length === 0) { this.message.warning('Enter an amount against at least one invoice'); return; }
    this.saving.set(true);
    this.api.allocate(r.id, allocations).subscribe({ next: () => this.done(doneText('updated', 'Receipt', r.number)), error: (e) => this.fail(e) });
  }

  voidDocument(): void {
    const r = this.receipt();
    if (!r) return;
    // Same required-reason prompt as the detail page; the void endpoint takes no body, so the reason is not sent.
    void askReason(this.modal, 'Void this receipt?', 'Void Receipt', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (reason) this.api.void(r.id, reason).subscribe({ next: () => this.done(doneText('voided', 'Receipt', r.number)), error: (e) => this.fail(e) });
    });
  }

  /** A new receipt for one customer, allocated to one invoice's balance when `invoiceId` is given. */
  private startFor(partyId: string, invoiceId: string | null): void {
    this.open(null);
    this.form.patchValue({ partyId });
    this.parties.get(partyId).subscribe((p) => this.partyOptions.update((o) => [partyOption(p), ...o.filter((x) => x.value !== p.id)]));
    this.loadOpen(partyId, invoiceId);
  }

  private openNew(): void {
    this.receipt.set(null);
    this.form.reset({ legalEntityId: this.lookups.onlyEntityId(), partyId: '', date: new Date(), method: 'bankTransfer', bankAccountId: mainBankAccount(this.banks())?.id ?? '', amount: null, reference: '', autoAllocate: false });
    this.form.enable();
    this.searchParties('');
    this.drawerOpen.set(true);
  }

  private show(r: ReceiptDto): void {
    this.receipt.set(r);
    this.partyOptions.set([documentPartyOption(r)]);
    this.form.reset({ legalEntityId: r.legalEntityId, partyId: r.partyId, date: new Date(r.date + 'T00:00:00'), method: r.method, bankAccountId: r.bankAccountId ?? '', amount: r.amount, reference: r.reference ?? '', autoAllocate: false });
    this.form.disable();
    if (r.status === 'issued' && r.unallocated > 0) this.loadOpen(r.partyId);
    this.drawerOpen.set(true);
  }

  /** The customer's open invoices; `prefillInvoiceId` gets its whole balance, which also becomes the receipt amount. */
  private loadOpen(partyId: string, prefillInvoiceId?: string | null): void {
    this.invoices.list(openDocumentsQuery(partyId, [{ field: 'isCreditNote', op: 'eq', value: false }, { field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }]))
      .subscribe((r) => {
        this.openInvoices.set(r.items.filter((i) => isOpenStatus(i.status)));
        const inv = prefillInvoiceId ? this.openInvoices().find((i) => i.id === prefillInvoiceId) : undefined;
        if (inv) { this.setAmount(inv.id, inv.outstanding); this.form.patchValue({ amount: inv.outstanding }); }
      });
  }

  private allocations(): { invoiceId: string; amount: number }[] {
    return allocatedAmounts(this.amounts()).map(([invoiceId, amount]) => ({ invoiceId, amount }));
  }

  private loadStats(): void { this.kpis.load(loadSettlementStats((q: ListQuery) => this.api.list(q))); }
  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.reload(); }
  private fail(e: unknown): void { this.saving.set(false); this.message.error(problemMessage(e, 'Could not save the receipt', 'reason')); }
}
