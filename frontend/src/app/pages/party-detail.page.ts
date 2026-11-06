import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { API_BASE_URL } from '../core/api/api-config';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../shared/detail-page.component';
import { AttachmentsComponent } from '../shared/attachments.component';
import { PartyStatementComponent } from '../shared/party-statement.component';
import { matchText, TableToolsComponent } from '../shared/table-tools.component';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { SessionStore } from '../core/session/session.store';
import { I18nService } from '../core/i18n/i18n.service';
import { KeyboardClickDirective } from '../shared/keyboard-click.directive';
import { ErpLabelPipe, ErpDatePipe } from '../shared/display.pipes';
import { StatePanelComponent } from '../shared/state-panel.component';
import { StatusTagComponent } from '../shared/status-tag.component';
import { catchError, forkJoin, of } from 'rxjs';
import { BillSummary, BillsApi, InvoiceSummary, InvoicesApi } from '../core/api/admin.api';
import { ListQuery } from '../core/api/api.models';
import { WorkspaceCurrencyService } from '../core/services/workspace-currency.service';
import { AllRows, fetchAllRows, moneyByCurrency } from './admin/finance-kpis';
import { formatMoney } from './admin/invoice-sheet.component';
import { ErpTableModule } from '../shared/erp-table.module';
import { SkeletonComponent } from '../shared/skeleton.component';

interface Transaction {
  id: string;
  type: string;
  number: string;
  date: string;
  amount: number;
  outstanding: number | null;
  status: string;
  currencyCode: string;
}

interface Party {
  id: string;
  code: string;
  name: string;
  kind: string;
  roles: string[];
  email: string | null;
  phone: string | null;
  taxNumber: string | null;
  currencyCode: string | null;
  paymentTermsDays: number | null;
  address: string | null;
  isActive: boolean;
  totalSales?: number;
  totalPurchases?: number;
  outstandingReceivables?: number;
  outstandingPayables?: number;
  creditLimit?: number;
}

@Component({
  selector: 'erp-party-detail',
  standalone: true,
  imports: [ErpLabelPipe, ErpDatePipe, SkeletonComponent, StatePanelComponent, StatusTagComponent, KeyboardClickDirective, TableToolsComponent, CommonModule, DatePipe, DecimalPipe, ErpTableModule, NzTabsModule, NzDrawerModule, DetailPageComponent, AttachmentsComponent, PartyStatementComponent, TranslatePipe],
  template: `
    @if (loading()) {
      <erp-skeleton kind="detail" />
    } @else if (error()) {
      <erp-state-panel kind="error" title="Could not load this party" [hint]="error()" actionLabel="Try again" (action)="retry()" />
    } @else if (party()) {
      <erp-detail-page
        [backLink]="['/parties']"
        [backLabel]="'Back to Parties'"
        [title]="party()!.name"
        [status]="party()!.isActive ? 'Active' : 'Inactive'"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="''"
        [entityName]="partyType()"
        [showDocuments]="false"
        [showHistory]="true"
        recordType="Party" [recordId]="party()!.id">
        
        <div content style="margin-top: 24px;">
        <nz-tabset>
          @if (isCustomer()) {
            <nz-tab nzTitle="Recent Transactions">
              <div style="padding: 16px;">
                <h3>Recent Sales Transactions</h3>
                <erp-table-tools [(search)]="txQ" (refresh)="loadTransactions(party()!.id)" [loading]="loadingTx()" testId="party-tx-sales"></erp-table-tools>
                <nz-table #txTable [nzData]="txShown()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzLoading]="loadingTx()">
                  <thead>
                    <tr>
                      <th>Type</th>
                      <th>Number</th>
                      <th>Date</th>
                      <th style="text-align: right;">Amount</th>
                      <th style="text-align: right;">Outstanding</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (tx of txTable.data; track tx.id) {
                      <tr erpKeyboardClick erpKeyboardClickRole="" style="cursor: pointer;" (click)="viewTransaction(tx)">
                        <td>{{ tx.type | erpLabel }}</td>
                        <td><span class="code-pill">{{ tx.number }}</span></td>
                        <td style="white-space: nowrap;">{{ tx.date | erpDate }}</td>
                        <td style="text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums;">{{ txMoney(tx.amount, tx.currencyCode) }}</td>
                        <td style="text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums;">{{ tx.outstanding === null ? '—' : txMoney(tx.outstanding, tx.currencyCode) }}</td>
                        <td><erp-status [value]="tx.status" /></td>
                      </tr>
                    }
                    @empty {
                      <tr><td colspan="6" style="text-align: center; color: var(--erp-color-text-muted);">{{ txFailed() ? 'Could not load the transactions. Use Refresh to try again.' : 'No transactions yet' }}</td></tr>
                    }
                  </tbody>
                </nz-table>
              </div>
            </nz-tab>
            @if (canReports()) {
              <nz-tab [nzTitle]="isVendor() ? ('party.customerStatement' | translate: 'Customer statement') : ('party.statement' | translate: 'Statement')">
                <ng-template nz-tab>
                  <div style="padding: 16px;" data-testid="party-statement-tab"><erp-party-statement [partyId]="party()!.id" side="customer" [partyName]="party()!.name" /></div>
                </ng-template>
              </nz-tab>
            }
          }
          @if (isVendor()) {
            <nz-tab nzTitle="Recent Transactions">
              <div style="padding: 16px;">
                <h3>Recent Purchase Transactions</h3>
                <erp-table-tools [(search)]="txQ" (refresh)="loadTransactions(party()!.id)" [loading]="loadingTx()" testId="party-tx-purchases"></erp-table-tools>
                <nz-table #txTable [nzData]="txShown()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzLoading]="loadingTx()">
                  <thead>
                    <tr>
                      <th>Type</th>
                      <th>Number</th>
                      <th>Date</th>
                      <th style="text-align: right;">Amount</th>
                      <th style="text-align: right;">Outstanding</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (tx of txTable.data; track tx.id) {
                      <tr erpKeyboardClick erpKeyboardClickRole="" style="cursor: pointer;" (click)="viewTransaction(tx)">
                        <td>{{ tx.type | erpLabel }}</td>
                        <td><span class="code-pill">{{ tx.number }}</span></td>
                        <td style="white-space: nowrap;">{{ tx.date | erpDate }}</td>
                        <td style="text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums;">{{ txMoney(tx.amount, tx.currencyCode) }}</td>
                        <td style="text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums;">{{ tx.outstanding === null ? '—' : txMoney(tx.outstanding, tx.currencyCode) }}</td>
                        <td><erp-status [value]="tx.status" /></td>
                      </tr>
                    }
                    @empty {
                      <tr><td colspan="6" style="text-align: center; color: var(--erp-color-text-muted);">{{ txFailed() ? 'Could not load the transactions. Use Refresh to try again.' : 'No transactions yet' }}</td></tr>
                    }
                  </tbody>
                </nz-table>
              </div>
            </nz-tab>
            @if (canReports()) {
              <nz-tab [nzTitle]="isCustomer() ? ('party.vendorStatement' | translate: 'Vendor statement') : ('party.statement' | translate: 'Statement')">
                <ng-template nz-tab>
                  <div style="padding: 16px;" data-testid="party-vendor-statement-tab"><erp-party-statement [partyId]="party()!.id" side="vendor" [partyName]="party()!.name" /></div>
                </ng-template>
              </nz-tab>
            }
          }
          @if (canDocuments()) {
            <nz-tab [nzTitle]="'party.documents' | translate: 'Documents'">
              <ng-template nz-tab>
                <div style="padding: 16px;" data-testid="party-documents-tab"><erp-attachments ownerType="party" [ownerId]="party()!.id" /></div>
              </ng-template>
            </nz-tab>
          }
        </nz-tabset>
        </div>
      </erp-detail-page>

      <nz-drawer [nzVisible]="statementOpen()" [nzWidth]="720" [nzTitle]="statementTitle()" (nzOnClose)="statementOpen.set(false)">
        <ng-container *nzDrawerContent>
          @if (statement(); as st) {
            <p>{{ 'ui.common.asOf' | translate: 'As of' }} {{ st.asOf | erpDate }}</p>
            <nz-table #stTable [nzData]="st.items" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="party-statement">
              <thead><tr><th>Number</th><th>Date</th><th>Due</th><th style="text-align: right;">Days overdue</th><th style="text-align: right;">Outstanding</th></tr></thead>
              <tbody>
                @for (it of stTable.data; track it.invoiceId) {
                  <tr>
                    <td>{{ it.number }}{{ it.isCreditNote ? ' (credit)' : '' }}</td>
                    <td>{{ it.invoiceDate | erpDate }}</td>
                    <td>{{ it.dueDate | erpDate }}</td>
                    <td style="text-align: right;">{{ it.daysOverdue }}</td>
                    <td style="text-align: right;">{{ it.outstanding | number:'1.2-2' }}</td>
                  </tr>
                } @empty { <tr><td colspan="5" style="text-align: center; color: var(--erp-color-text-muted);">Nothing outstanding</td></tr> }
              </tbody>
            </nz-table>
            <p style="text-align: right; margin-top: 12px;">{{ 'ui.party.unallocated' | translate: 'Unallocated' }}: {{ (st.unallocatedReceipts ?? st.unallocatedPayments ?? 0) | number:'1.2-2' }}<br /><b>{{ 'ui.party.balance' | translate: 'Balance' }}: {{ st.balance | number:'1.2-2' }}</b></p>
          } @else { <erp-skeleton kind="table" [rows]="4" [columns]="5" /> }
        </ng-container>
      </nz-drawer>
    }
  `
})
export class PartyDetailPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly session = inject(SessionStore);
  readonly canReports = computed(() => this.session.hasPermission('finance.reports.read'));
  readonly canDocuments = computed(() => this.session.hasPermission('core.attachments.read'));

  statementOpen = signal(false);
  statementTitle = signal('Statement');
  statement = signal<{ asOf: string; items: { invoiceId: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; daysOverdue: number; outstanding: number }[]; unallocatedReceipts?: number; unallocatedPayments?: number; balance: number } | null>(null);

  loading = signal(true);
  error = signal<string | null>(null);
  party = signal<Party | null>(null);
  transactions = signal<Transaction[]>([]);
  loadingTx = signal(false);
  readonly txFailed = signal(false);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly billsApi = inject(BillsApi);
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService).currency;
  /** The party's documents, read from the invoice and bill lists: the party record itself carries no totals. */
  private readonly sales = signal<AllRows<InvoiceSummary> | null>(null);
  private readonly purchases = signal<AllRows<BillSummary> | null>(null);
  txMoney(v: number, currency: string): string { return formatMoney(v, currency); }
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

  actions = computed((): ActionButton[] => {
    const p = this.party();
    if (!p) return [];
    
    const acts: ActionButton[] = [
      {
        label: 'Edit',
        icon: 'edit',
        permission: 'core.parties.write',
        handler: () => this.router.navigate(['/parties'], { queryParams: { edit: p.id } })
      }
    ];

    if (this.isCustomer()) {
      acts.push({
        label: 'New Invoice',
        icon: 'file-add',
        type: 'primary',
        reveal: true,
        labelKey: 'ui.add.newInvoice',
        permission: 'finance.invoices.write',
        handler: () => this.router.navigate(['/finance/invoices'], { queryParams: { partyId: p.id } })
      });
      acts.push({
        label: 'View Statement',
        icon: 'file-text',
        permission: 'finance.reports.read',
        handler: () => this.openStatement('customer')
      });
    }

    if (this.isVendor()) {
      acts.push({
        label: 'New Bill',
        icon: 'file-add',
        type: 'primary',
        reveal: true,
        labelKey: 'ui.add.newBill',
        permission: 'finance.bills.write',
        handler: () => this.router.navigate(['/finance/bills'], { queryParams: { partyId: p.id } })
      });
      acts.push({
        label: 'Vendor Statement',
        icon: 'file-text',
        permission: 'finance.reports.read',
        handler: () => this.openStatement('vendor')
      });
    }

    return acts;
  });

  kpiCards = computed((): KpiCard[] => {
    const p = this.party();
    if (!p) return [];

    const cards: KpiCard[] = [];
    const cur = p.currencyCode ?? this.workspaceCurrency();
    const open = (status: string) => status === 'issued' || status === 'partlyPaid';

    if (this.isCustomer()) {
      const rows = this.sales()?.items ?? [];
      const sign = (r: InvoiceSummary) => (r.isCreditNote ? -1 : 1);
      const balance = rows.filter((r) => open(r.status)).reduce((s, r) => s + sign(r) * r.outstanding, 0);
      cards.push(
        { title: 'Total sales', caption: 'Less credit notes', value: this.sales() ? moneyByCurrency(rows, (r) => sign(r) * r.total, cur) : '—' },
        { title: 'Outstanding', caption: balance < 0 ? 'Customer is in credit' : 'Open invoices less credits', value: this.sales() ? moneyByCurrency(rows.filter((r) => open(r.status)), (r) => sign(r) * r.outstanding, cur) : '—', valueStyle: { color: balance > 0 ? '#fa8c16' : '#52c41a' } },
      );
    }

    if (this.isVendor()) {
      const rows = this.purchases()?.items ?? [];
      const balance = rows.filter((r) => open(r.status)).reduce((s, r) => s + r.outstanding, 0);
      cards.push(
        { title: 'Total purchases', value: this.purchases() ? moneyByCurrency(rows, (r) => r.total, cur) : '—' },
        { title: 'Outstanding payables', value: this.purchases() ? moneyByCurrency(rows.filter((r) => open(r.status)), (r) => r.outstanding, cur) : '—', valueStyle: { color: balance > 0 ? '#fa8c16' : '#52c41a' } },
      );
    }

    if (p.paymentTermsDays) {
      cards.push({
        title: 'Payment Terms',
        value: `${p.paymentTermsDays} ${this.i18n.t('ui.common.days', 'days')}`
      });
    }

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
      { label: 'Address', value: p.address, span: 2 }
    ];
  });

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid party ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/parties/${id}`).subscribe({
      next: (response) => {
        this.party.set(response);
        this.loading.set(false);
        this.loadTransactions(id);
        this.loadTotals(id);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load party');
        this.loading.set(false);
      }
    });
  }

  /** Every issued document of this party. A failure leaves the cards showing a dash rather than a made-up zero. */
  private loadTotals(partyId: string): void {
    const filters = [{ field: 'partyId', op: 'eq' as const, value: partyId }, { field: 'status', op: 'in' as const, value: ['issued', 'partlyPaid', 'paid'] }];
    if (this.isCustomer()) fetchAllRows((q: ListQuery) => this.invoicesApi.list(q), filters).pipe(catchError(() => of(null))).subscribe((r) => this.sales.set(r));
    if (this.isVendor()) fetchAllRows((q: ListQuery) => this.billsApi.list(q), filters).pipe(catchError(() => of(null))).subscribe((r) => this.purchases.set(r));
  }

  loadTransactions(partyId: string): void {
    this.loadingTx.set(true);
    this.txFailed.set(false);
    
    const endpoints: string[] = [];
    if (this.isCustomer()) {
      endpoints.push(`${this.apiBase}/finance/invoices?filter=partyId:eq:${partyId}&pageSize=10&sort=-invoiceDate`);
      endpoints.push(`${this.apiBase}/finance/receipts?filter=partyId:eq:${partyId}&pageSize=10&sort=-date`);
    }
    if (this.isVendor()) {
      endpoints.push(`${this.apiBase}/finance/bills?filter=partyId:eq:${partyId}&pageSize=10&sort=-billDate`);
      endpoints.push(`${this.apiBase}/finance/payments?filter=partyId:eq:${partyId}&pageSize=10&sort=-date`);
    }

    Promise.all(endpoints.map(url => this.http.get<any>(url).toPromise()))
      .then(responses => {
        const allTx: Transaction[] = [];
        responses.forEach((res, idx) => {
          if (res?.items) {
            const type = endpoints[idx].includes('invoices') ? 'Invoice' :
                        endpoints[idx].includes('receipts') ? 'Receipt' :
                        endpoints[idx].includes('bills') ? 'Bill' : 'Payment';
            
            res.items.forEach((item: any) => {
              allTx.push({
                id: item.id,
                type: item.isCreditNote ? 'Credit note' : type,
                number: item.number,
                date: item.invoiceDate || item.billDate || item.date,
                // A credit note takes money off the account, so it reads as negative here too.
                amount: (item.isCreditNote ? -1 : 1) * (item.total || item.amount || 0),
                // Receipts and payments have no balance of their own.
                outstanding: item.total === undefined ? null : item.status === 'voided' ? 0 : (item.isCreditNote ? -1 : 1) * (item.total - (item.paid || 0)),
                status: item.total === undefined && item.status === 'issued' ? 'posted' : item.status,
                currencyCode: item.currencyCode
              });
            });
          }
        });
        
        allTx.sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
        this.transactions.set(allTx.slice(0, 20));
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
    const d = new Date();
    const asOf = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.statement.set(null);
    this.statementTitle.set(`${kind === 'customer' ? this.i18n.t('ui.party.customerStatement', 'Customer statement') : this.i18n.t('ui.party.vendorStatement', 'Vendor statement')} · ${p.name}`);
    this.statementOpen.set(true);
    this.http.get<any>(`${this.apiBase}/finance/reports/${kind}-statement`, { params: { partyId: p.id, asOf } }).subscribe({
      next: (res) => this.statement.set(res),
      error: (err) => { this.statementOpen.set(false); this.message.error(err?.error?.detail || err?.error?.message || 'Failed to load statement'); }
    });
  }

  viewTransaction(tx: Transaction): void {
    const routes: Record<string, string> = {
      'Invoice': '/finance/invoices',
      'Credit note': '/finance/invoices',
      'Receipt': '/finance/receipts',
      'Bill': '/finance/bills',
      'Payment': '/finance/payments'
    };
    this.router.navigate([routes[tx.type], tx.id]);
  }
}
