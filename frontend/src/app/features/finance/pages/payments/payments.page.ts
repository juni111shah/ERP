import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
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
import { BillSummary, BillsApi, PAYMENT_METHODS, PaymentDto, PaymentSummary, PaymentsApi } from '@core/api/admin.api';
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
import { AllRows, KpiStats } from '@shared/utils/kpi-stats';
import { FinanceLookups, documentPartyOption, mainBankAccount, partyOption } from '../../finance-lookups';
import { allocatedAmounts, fillLateDefaults, openDocumentsQuery } from '../../finance-settlements';
import { loadSettlementStats, settlementStats } from '../../finance-stats';

/** Vendor payments: pay vendors by bank, cash or cheque, settle open bills at the same time (or later), clear issued cheques, and void. */
@Component({
  selector: 'erp-payments',
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
  styleUrl: './payments.page.scss',
  templateUrl: './payments.page.html',
})
export class PaymentsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PaymentsApi);
  private readonly router = inject(Router);
  private readonly modal = inject(NzModalService);
  private readonly bills = inject(BillsApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly lookups = inject(FinanceLookups).load({ entities: true, accounts: true });
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;

  readonly entities = this.lookups.entities;
  readonly banks = this.lookups.cashAndBankAccounts;
  readonly methods = PAYMENT_METHODS;
  readonly money = formatMoney;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });

  private readonly kpis = new KpiStats<AllRows<PaymentSummary>>();
  readonly statsLoading = this.kpis.loading;
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = this.kpis.error;
  /** The figures for the whole workspace, not the page on screen: every posted payment is read, newest first. */
  readonly stats = computed(() => { const d = this.kpis.data(); return d ? settlementStats(d, this.workspaceCurrency(), 'payment', 'Advances') : null; });

  readonly columns: TableColumn<PaymentSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'partyName', label: 'Vendor' },
    { key: 'date', label: 'Date', sortField: 'date', width: '120px', filterField: 'date' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '130px', align: 'right', value: (r) => formatMoney(r.amount, r.currencyCode) },
    { key: 'unallocated', label: 'Unallocated', width: '130px', align: 'right', value: (r) => formatMoney(r.unallocated, r.currencyCode) },
    { key: 'status', label: 'Status', width: '100px', filterField: 'status', filterOptions: [{ value: 'issued', label: 'Posted' }, { value: 'voided', label: 'Voided' }] },
  ];
  readonly drawerOpen = signal(false);
  /** Arriving from an invoice or bill opens the form before the legal entities and bank accounts have loaded; fill the defaults once they arrive. */
  private readonly lateDefaults = effect(() => {
    const entities = this.entities(), banks = this.banks();
    if (!this.drawerOpen() || this.receipt()) return;
    fillLateDefaults(this.form.controls, entities, banks);
  });
  readonly saving = signal(false);
  /** The payment open in the drawer (named like the receipts page, whose template this one mirrors). */
  readonly receipt = signal<PaymentDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly openInvoices = signal<BillSummary[]>([]);
  readonly amounts = signal<Record<string, number>>({});
  readonly editableAllocation = computed(() => !this.receipt() || (this.receipt()!.status === 'issued' && this.receipt()!.unallocated > 0));
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], date: [null as Date | null, Validators.required], method: ['bankTransfer', Validators.required],
    bankAccountId: ['', Validators.required], amount: [null as number | null, [Validators.required, Validators.min(0.01)]], reference: [''],
    chequeNo: [''], chequeDate: [null as Date | null], bankName: [''],
  });
  private readonly method = toSignal(this.form.controls.method.valueChanges, { initialValue: 'bankTransfer' });
  readonly isCheque = computed(() => this.method() === 'instrument');

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
    if (q.get('new')) this.open(null);
    const id = q.get('open');
    if (id) this.open({ id } as PaymentSummary);
    // `?partyId=&billId=` (bill detail's Make Payment) starts a new payment for that vendor, allocated to the bill's balance.
    const partyId = q.get('partyId');
    if (!id && partyId) this.startFor(partyId, q.get('billId'));
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/payments', id]); }
  printDoc(id: string): void { window.open(`/print/payment/${id}`, '_blank'); }

  open(row: PaymentSummary | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!row) { this.openNew(); return; }
    this.api.detail(row.id).subscribe((r) => this.show(r));
  }

  create(): void {
    if (this.form.invalid) { revealErrors(this.form, { touchAll: true }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const cheque = v.method === 'instrument';
    this.saving.set(true);
    this.api.create({ ...v, date: toIsoDate(v.date!), reference: v.reference || null, chequeNo: cheque ? v.chequeNo : null, chequeDate: cheque && v.chequeDate ? toIsoDate(v.chequeDate) : null, bankName: v.bankName || null, allocations: this.allocations() }).subscribe({
      next: (r) => { this.saving.set(false); this.drawerOpen.set(false); void this.router.navigate(['/finance/payments', (r as PaymentDto).id], { queryParams: { recorded: 1 } }); },
      error: (e) => this.fail(e),
    });
  }

  allocate(): void {
    const r = this.receipt();
    const allocations = this.allocations();
    if (!r || allocations.length === 0) { this.message.warning('Enter an amount against at least one invoice'); return; }
    this.saving.set(true);
    this.api.allocate(r.id, allocations).subscribe({ next: () => this.done(doneText('updated', 'Payment', r.number)), error: (e) => this.fail(e) });
  }

  clearCheque(): void {
    const r = this.receipt();
    if (r?.paymentInstrumentId) this.api.clearCheque(r.paymentInstrumentId, toIsoDate(new Date())).subscribe({ next: () => this.done('Cheque marked as cleared'), error: (e) => this.fail(e) });
  }

  voidDocument(): void {
    const r = this.receipt();
    if (!r) return;
    // Same required-reason prompt as the detail page; the void endpoint takes no body, so the reason is not sent.
    void askReason(this.modal, 'Void this payment?', 'Void Payment', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (reason) this.api.void(r.id, reason).subscribe({ next: () => this.done(doneText('voided', 'Payment', r.number)), error: (e) => this.fail(e) });
    });
  }

  /** A new payment for one vendor, allocated to one bill's balance when `billId` is given. */
  private startFor(partyId: string, billId: string | null): void {
    this.open(null);
    this.form.patchValue({ partyId });
    this.parties.get(partyId).subscribe((p) => this.partyOptions.update((o) => [partyOption(p), ...o.filter((x) => x.value !== p.id)]));
    this.loadOpen(partyId, billId);
  }

  private openNew(): void {
    this.receipt.set(null);
    this.form.reset({ legalEntityId: this.lookups.onlyEntityId(), partyId: '', date: new Date(), method: 'bankTransfer', bankAccountId: mainBankAccount(this.banks())?.id ?? '', amount: null, reference: '', chequeNo: '', chequeDate: null, bankName: '' });
    this.form.enable();
    this.searchParties('');
    this.drawerOpen.set(true);
  }

  private show(r: PaymentDto): void {
    this.receipt.set(r);
    this.partyOptions.set([documentPartyOption(r)]);
    this.form.reset({ legalEntityId: r.legalEntityId, partyId: r.partyId, date: new Date(r.date + 'T00:00:00'), method: r.method, bankAccountId: r.bankAccountId ?? '', amount: r.amount, reference: r.reference ?? '', chequeNo: '', chequeDate: null, bankName: '' });
    this.form.disable();
    if (r.status === 'issued' && r.unallocated > 0) this.loadOpen(r.partyId);
    this.drawerOpen.set(true);
  }

  /** The vendor's open bills; `prefillBillId` gets its whole balance, which also becomes the payment amount. */
  private loadOpen(partyId: string, prefillBillId?: string | null): void {
    this.bills.list(openDocumentsQuery(partyId, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }]))
      .subscribe((r) => {
        this.openInvoices.set(r.items.filter((i) => isOpenStatus(i.status)));
        const bill = prefillBillId ? this.openInvoices().find((i) => i.id === prefillBillId) : undefined;
        if (bill) { this.setAmount(bill.id, bill.outstanding); this.form.patchValue({ amount: bill.outstanding }); }
      });
  }

  private allocations(): { billId: string; amount: number }[] {
    return allocatedAmounts(this.amounts()).map(([billId, amount]) => ({ billId, amount }));
  }

  private loadStats(): void { this.kpis.load(loadSettlementStats((q: ListQuery) => this.api.list(q))); }
  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.reload(); }
  private fail(e: unknown): void { this.saving.set(false); this.message.error(problemMessage(e, 'Could not save the payment', 'reason')); }
}
