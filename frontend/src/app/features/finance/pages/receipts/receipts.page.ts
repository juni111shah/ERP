import { doneText } from '@core/format/done-text';
import { ActivatedRoute, Router } from '@angular/router';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '@shared/utils/reason-prompt';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, of } from 'rxjs';
import { ListQuery } from '@core/api/api.models';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { AllRows, countOf, fetchAllRows, moneyByCurrency, startOfMonth, startOfYear } from '../../finance-kpis';
import { AccountDto, AccountsApi, InvoiceSummary, InvoicesApi, LegalEntitiesApi, LegalEntityDto, ReceiptDto, ReceiptSummary, ReceiptsApi, SETTLEMENT_METHODS } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { asProblem, controlError, problemMessage } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { formatMoney } from '../../components/invoice-sheet/invoice-sheet.component';
import { PartiesApi } from '@features/parties/pages/parties/parties.page';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Customer receipts: record money received, settle open invoices at the same time (or later), and void. */
@Component({
  selector: 'erp-receipts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, FormsModule, NzModalModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzSelectModule, NzTagModule, StatCardComponent, StatusTagComponent],
  styleUrl: './receipts.page.scss',
  templateUrl: './receipts.page.html',
})
export class ReceiptsPage {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ReceiptsApi);
  private readonly router = inject(Router);
  private readonly modal = inject(NzModalService);
  private readonly invoices = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly banks = computed(() => this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'cash' || a.systemRole === 'bank')));
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
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;
  private readonly statsData = signal<AllRows<ReceiptSummary> | null>(null);
  readonly statsLoading = signal(true);
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = computed(() => (this.statsLoading() ? null : 'Could not load this figure'));
  /** The figures for the whole workspace, not the page on screen: every posted receipt is read, newest first. */
  readonly stats = computed(() => {
    const d = this.statsData();
    if (!d) return null;
    const cur = this.workspaceCurrency();
    const month = d.items.filter((r) => r.date >= startOfMonth());
    const year = d.items.filter((r) => r.date >= startOfYear());
    const open = d.items.filter((r) => r.unallocated > 0);
    return {
      count: d.total,
      month: moneyByCurrency(month, (r) => r.amount, cur),
      monthCaption: countOf(month.length, 'receipt'),
      year: moneyByCurrency(year, (r) => r.amount, cur),
      yearCaption: countOf(year.length, 'receipt'),
      unallocated: moneyByCurrency(open, (r) => r.unallocated, cur),
      unallocatedCount: open.length,
      unallocatedCaption: (open.length ? `On account, ${countOf(open.length, 'receipt')}` : 'All allocated') + (d.capped ? ' (latest rows only)' : ''),
    };
  });
  reload(): void { this.list.reload(); this.loadStats(); }
  private loadStats(): void {
    fetchAllRows((q: ListQuery) => this.api.list(q), [{ field: 'status', op: 'eq', value: 'issued' }], '-date').subscribe({
      next: (d) => { this.statsData.set(d); this.statsLoading.set(false); },
      error: () => { this.statsData.set(null); this.statsLoading.set(false); },
    });
  }
  readonly drawerOpen = signal(false);
  /** Arriving from an invoice or bill opens the form before the legal entities and bank accounts have loaded; fill the defaults once they arrive. */
  private readonly lateDefaults = effect(() => {
    const entities = this.entities(), banks = this.banks();
    if (!this.drawerOpen() || this.receipt()) return;
    const c = this.form.controls;
    if (entities.length === 1 && !c.legalEntityId.value) c.legalEntityId.setValue(entities[0].id);
    const bank = banks.find((b) => b.systemRole === 'bank') ?? banks[0];
    if (bank && !c.bankAccountId.value) c.bankAccountId.setValue(bank.id);
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

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => this.partyOptions.set(r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));
  }

  partyChanged(partyId: string | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!partyId) return;
    this.loadOpen(partyId);
  }

  private loadOpen(partyId: string, prefillInvoiceId?: string | null): void {
    this.invoices.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'partyId', op: 'eq', value: partyId }, { field: 'isCreditNote', op: 'eq', value: false }, { field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }] })
      .subscribe((r) => {
        this.openInvoices.set(r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid'));
        const inv = prefillInvoiceId ? this.openInvoices().find((i) => i.id === prefillInvoiceId) : undefined;
        if (inv) { this.setAmount(inv.id, inv.outstanding); this.form.patchValue({ amount: inv.outstanding }); }
      });
  }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions). */
  ngOnInit(): void {
    this.loadStats();
    const id = this.route.snapshot.queryParamMap.get('open');
    if (id) this.open({ id } as never);
    // `?partyId=&invoiceId=` (invoice detail's Record Payment) starts a new receipt for that customer, allocated to the invoice's balance.
    if (this.route.snapshot.queryParamMap.get('new')) this.open(null);
    const partyId = this.route.snapshot.queryParamMap.get('partyId');
    if (!id && partyId) {
      this.open(null);
      this.form.patchValue({ partyId });
      this.parties.get(partyId).subscribe((p) => this.partyOptions.update((o) => [{ value: p.id, label: `${p.code} · ${p.name}` }, ...o.filter((x) => x.value !== p.id)]));
      this.loadOpen(partyId, this.route.snapshot.queryParamMap.get('invoiceId'));
    }
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/receipts', id]); }
  printDoc(id: string): void { window.open(`/print/receipt/${id}`, '_blank'); }

  open(row: ReceiptSummary | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!row) {
      this.receipt.set(null);
      const entities = this.entities();
      this.form.reset({ legalEntityId: entities.length === 1 ? entities[0].id : '', partyId: '', date: new Date(), method: 'bankTransfer', bankAccountId: this.banks().find((b) => b.systemRole === 'bank')?.id ?? '', amount: null, reference: '', autoAllocate: false });
      this.form.enable();
      this.searchParties('');
      this.drawerOpen.set(true);
      return;
    }
    this.api.detail(row.id).subscribe((r) => {
      this.receipt.set(r);
      this.partyOptions.set([{ value: r.partyId, label: r.partyName ?? r.partyId }]);
      this.form.reset({ legalEntityId: r.legalEntityId, partyId: r.partyId, date: new Date(r.date + 'T00:00:00'), method: r.method, bankAccountId: r.bankAccountId ?? '', amount: r.amount, reference: r.reference ?? '', autoAllocate: false });
      this.form.disable();
      if (r.status === 'issued' && r.unallocated > 0) this.loadOpen(r.partyId);
      this.drawerOpen.set(true);
    });
  }

  private allocations(): { invoiceId: string; amount: number }[] {
    return Object.entries(this.amounts()).filter(([, a]) => a > 0).map(([invoiceId, amount]) => ({ invoiceId, amount }));
  }

  create(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); this.message.warning('Fill in the fields marked in red'); return; }
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

  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.reload(); }
  private fail(e: unknown): void { this.saving.set(false); this.message.error(problemMessage(e, 'Could not save the receipt', 'reason')); }
}
