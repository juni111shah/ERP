import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, lastValueFrom, of } from 'rxjs';
import { BillSummary, BillsApi, InvoiceSummary, InvoicesApi } from '@core/api/admin.api';
import { FilterCriterion, ListQuery } from '@core/api/api.models';
import { PartyActivityApi, PartyDetailDto, PartyStatementDto } from '@core/api/party-activity.api';
import { toIsoDate } from '@core/format/dates';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { PartyStatementComponent } from '@shared/components/party-statement/party-statement.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { TableToolsComponent, matchText } from '@shared/components/table-tools/table-tools.component';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { loadRecord } from '@shared/utils/load-record';
import { formatMoney } from '@features/finance/finance-format';
import { AllRows, fetchAllRows } from '@shared/utils/kpi-stats';
import { Transaction, activityLists, customerCards, latestTransactions, transactionLink, vendorCards } from '../../parties-activity';

/** A customer or vendor: details, sales / purchase cards, recent documents, statement, attachments and the actions for its roles. */
@Component({
  selector: 'erp-party-detail',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    NzDrawerModule,
    NzSpinModule,
    NzTableModule,
    NzTabsModule,
    TranslatePipe,
    AttachmentsComponent,
    DetailPageComponent,
    PartyStatementComponent,
    StatePanelComponent,
    StatusTagComponent,
    TableToolsComponent,
    KeyboardClickDirective,
    ErpDatePipe,
    ErpLabelPipe,
  ],
  templateUrl: './party-detail.page.html',
})
export class PartyDetailPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  private readonly activityApi = inject(PartyActivityApi);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly billsApi = inject(BillsApi);
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;

  readonly canReports = computed(() => this.session.hasPermission('finance.reports.read'));
  readonly canDocuments = computed(() => this.session.hasPermission('core.attachments.read'));

  statementOpen = signal(false);
  statementTitle = signal('Statement');
  statement = signal<PartyStatementDto | null>(null);

  loading = signal(true);
  error = signal<string | null>(null);
  party = signal<PartyDetailDto | null>(null);
  transactions = signal<Transaction[]>([]);
  loadingTx = signal(false);
  readonly txFailed = signal(false);
  /** The party's documents, read from the invoice and bill lists: the party record itself carries no totals. */
  private readonly sales = signal<AllRows<InvoiceSummary> | null>(null);
  private readonly purchases = signal<AllRows<BillSummary> | null>(null);
  readonly txQ = signal('');
  /** Client-side search over the loaded recent transactions (type, number, status). */
  readonly txShown = computed(() => this.transactions().filter((t) => matchText(this.txQ(), t.type, t.number, t.status)));

  isCustomer = computed(() => this.party()?.roles.includes('Customer') ?? false);
  isVendor = computed(() => this.party()?.roles.includes('Vendor') ?? false);

  partyType = computed(() => {
    const roles = [];
    if (this.isCustomer()) roles.push('Customer');
    if (this.isVendor()) roles.push('Vendor');
    return roles.join(' / ') || 'Party';
  });

  actions = computed((): ActionButton[] => { const p = this.party(); return p ? this.buildActions(p) : []; });

  kpiCards = computed((): KpiCard[] => {
    const p = this.party();
    if (!p) return [];
    const cur = p.currencyCode ?? this.workspaceCurrency();
    const cards: KpiCard[] = [];
    if (this.isCustomer()) cards.push(...customerCards(this.sales(), cur));
    if (this.isVendor()) cards.push(...vendorCards(this.purchases(), cur));
    if (p.creditLimit !== undefined && p.creditLimit !== null) {
      cards.push({ title: 'Credit limit', value: p.creditLimit.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) });
    }
    if (p.paymentTermsDays) cards.push({ title: 'Payment Terms', value: `${p.paymentTermsDays} ${this.i18n.t('ui.common.days', 'days')}` });
    return cards;
  });

  descriptions = computed((): DescriptionItem[] => {
    const p = this.party();
    if (!p) return [];
    return [
      { label: 'Code', value: p.code },
      { label: 'Type', value: p.kind },
      { label: 'Roles', value: p.roles.join(', ') },
      { label: 'Email', value: p.email },
      { label: 'Phone', value: p.phone },
      { label: 'Tax Number', value: p.taxNumber },
      { label: 'Currency', value: p.currencyCode },
      { label: 'Address', value: p.address, span: 2 },
    ];
  });

  txMoney(v: number, currency: string): string { return formatMoney(v, currency); }

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    loadRecord(id, (partyId) => this.activityApi.party(partyId), { loading: this.loading, error: this.error, record: this.party },
      { invalid: 'Invalid party ID', failed: 'Failed to load party' }, () => { this.loadTransactions(id!); this.loadTotals(id!); });
  }

  /** The 20 latest invoices, receipts, bills and payments of the party, newest first. */
  loadTransactions(partyId: string): void {
    this.loadingTx.set(true);
    this.txFailed.set(false);
    const lists = activityLists(this.isCustomer(), this.isVendor());
    Promise.all(lists.map((list) => lastValueFrom(this.activityApi.recent(list, partyId))))
      .then((responses) => {
        this.transactions.set(latestTransactions(responses, lists));
        this.loadingTx.set(false);
      })
      .catch(() => {
        this.loadingTx.set(false);
        this.txFailed.set(true);
      });
  }

  /** GET /finance/reports/{customer|vendor}-statement (open items as of today). */
  openStatement(kind: 'customer' | 'vendor'): void {
    const p = this.party();
    if (!p) return;
    this.statement.set(null);
    this.statementTitle.set(`${kind === 'customer' ? this.i18n.t('ui.party.customerStatement', 'Customer statement') : this.i18n.t('ui.party.vendorStatement', 'Vendor statement')} · ${p.name}`);
    this.statementOpen.set(true);
    this.activityApi.statement(kind, p.id, toIsoDate(new Date())).subscribe({
      next: (res) => this.statement.set(res),
      error: (err) => { this.statementOpen.set(false); this.message.error(err?.error?.detail || err?.error?.message || 'Failed to load statement'); },
    });
  }

  viewTransaction(tx: Transaction): void { this.router.navigate(transactionLink(tx)); }

  private buildActions(p: PartyDetailDto): ActionButton[] {
    const acts: ActionButton[] = [
      { label: 'Edit', icon: 'edit', permission: 'core.parties.write', handler: () => this.router.navigate(['/parties'], { queryParams: { edit: p.id } }) },
    ];
    if (this.isCustomer()) {
      acts.push(
        { label: 'New Invoice', icon: 'file-add', type: 'primary', reveal: true, labelKey: 'ui.add.newInvoice', permission: 'finance.invoices.write', handler: () => this.router.navigate(['/finance/invoices'], { queryParams: { partyId: p.id } }) },
        { label: 'View Statement', icon: 'file-text', permission: 'finance.reports.read', handler: () => this.openStatement('customer') },
      );
    }
    if (this.isVendor()) {
      acts.push(
        { label: 'New Bill', icon: 'file-add', type: 'primary', reveal: true, labelKey: 'ui.add.newBill', permission: 'finance.bills.write', handler: () => this.router.navigate(['/finance/bills'], { queryParams: { partyId: p.id } }) },
        { label: 'Vendor Statement', icon: 'file-text', permission: 'finance.reports.read', handler: () => this.openStatement('vendor') },
      );
    }
    return acts;
  }

  /** Every issued document of this party. A failure leaves the cards showing a dash rather than a made-up zero. */
  private loadTotals(partyId: string): void {
    const filters: FilterCriterion[] = [{ field: 'partyId', op: 'eq', value: partyId }, { field: 'status', op: 'in', value: ['issued', 'partlyPaid', 'paid'] }];
    if (this.isCustomer()) fetchAllRows((q: ListQuery) => this.invoicesApi.list(q), filters).pipe(catchError(() => of(null))).subscribe((r) => this.sales.set(r));
    if (this.isVendor()) fetchAllRows((q: ListQuery) => this.billsApi.list(q), filters).pipe(catchError(() => of(null))).subscribe((r) => this.purchases.set(r));
  }
}
