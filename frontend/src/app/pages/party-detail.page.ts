import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { API_BASE_URL } from '../core/api/api-config';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../shared/detail-page.component';

interface Transaction {
  id: string;
  type: string;
  number: string;
  date: string;
  amount: number;
  outstanding: number;
  status: string;
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
  imports: [CommonModule, DatePipe, CurrencyPipe, NzTableModule, NzTabsModule, NzDrawerModule, DetailPageComponent],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
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
        [showHistory]="false">
        
        <div content style="margin-top: 24px;">
        <nz-tabset>
          @if (isCustomer()) {
            <nz-tab nzTitle="Recent Transactions">
              <div style="padding: 16px;">
                <h3>Recent Sales Transactions</h3>
                <nz-table #txTable [nzData]="transactions()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzLoading]="loadingTx()">
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
                      <tr style="cursor: pointer;" (click)="viewTransaction(tx)">
                        <td>{{ tx.type }}</td>
                        <td>{{ tx.number }}</td>
                        <td>{{ tx.date | date:'dd MMM yyyy' }}</td>
                        <td style="text-align: right;">{{ tx.amount | currency:party()!.currencyCode || 'USD' }}</td>
                        <td style="text-align: right;">{{ tx.outstanding | currency:party()!.currencyCode || 'USD' }}</td>
                        <td>{{ tx.status }}</td>
                      </tr>
                    }
                    @empty {
                      <tr><td colspan="6" style="text-align: center; color: #999;">No transactions yet</td></tr>
                    }
                  </tbody>
                </nz-table>
              </div>
            </nz-tab>
            <nz-tab nzTitle="Statement">
              <div style="padding: 16px;">
                <p><i>Customer statement functionality will be fully implemented</i></p>
              </div>
            </nz-tab>
          }
          @if (isVendor()) {
            <nz-tab nzTitle="Recent Transactions">
              <div style="padding: 16px;">
                <h3>Recent Purchase Transactions</h3>
                <nz-table #txTable [nzData]="transactions()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzLoading]="loadingTx()">
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
                      <tr style="cursor: pointer;" (click)="viewTransaction(tx)">
                        <td>{{ tx.type }}</td>
                        <td>{{ tx.number }}</td>
                        <td>{{ tx.date | date:'dd MMM yyyy' }}</td>
                        <td style="text-align: right;">{{ tx.amount | currency:party()!.currencyCode || 'USD' }}</td>
                        <td style="text-align: right;">{{ tx.outstanding | currency:party()!.currencyCode || 'USD' }}</td>
                        <td>{{ tx.status }}</td>
                      </tr>
                    }
                    @empty {
                      <tr><td colspan="6" style="text-align: center; color: #999;">No transactions yet</td></tr>
                    }
                  </tbody>
                </nz-table>
              </div>
            </nz-tab>
          }
          <nz-tab nzTitle="Documents">
            <div style="padding: 16px;">
              <p><i>Document attachments will be displayed here</i></p>
            </div>
          </nz-tab>
          <nz-tab nzTitle="History">
            <div style="padding: 16px;">
              <p><i>Audit history will be displayed here</i></p>
            </div>
          </nz-tab>
        </nz-tabset>
        </div>
      </erp-detail-page>

      <nz-drawer [nzVisible]="statementOpen()" [nzWidth]="720" [nzTitle]="statementTitle()" (nzOnClose)="statementOpen.set(false)">
        <ng-container *nzDrawerContent>
          @if (statement(); as st) {
            <p>As of {{ st.asOf | date:'dd MMM yyyy' }}</p>
            <nz-table #stTable [nzData]="st.items" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="party-statement">
              <thead><tr><th>Number</th><th>Date</th><th>Due</th><th style="text-align: right;">Days overdue</th><th style="text-align: right;">Outstanding</th></tr></thead>
              <tbody>
                @for (it of stTable.data; track it.invoiceId) {
                  <tr>
                    <td>{{ it.number }}{{ it.isCreditNote ? ' (credit)' : '' }}</td>
                    <td>{{ it.invoiceDate | date:'dd MMM yyyy' }}</td>
                    <td>{{ it.dueDate | date:'dd MMM yyyy' }}</td>
                    <td style="text-align: right;">{{ it.daysOverdue }}</td>
                    <td style="text-align: right;">{{ it.outstanding | number:'1.2-2' }}</td>
                  </tr>
                } @empty { <tr><td colspan="5" style="text-align: center; color: #999;">Nothing outstanding</td></tr> }
              </tbody>
            </nz-table>
            <p style="text-align: right; margin-top: 12px;">Unallocated: {{ (st.unallocatedReceipts ?? st.unallocatedPayments ?? 0) | number:'1.2-2' }}<br /><b>Balance: {{ st.balance | number:'1.2-2' }}</b></p>
          } @else { <p>Loading…</p> }
        </ng-container>
      </nz-drawer>
    }
  `
})
export class PartyDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);

  statementOpen = signal(false);
  statementTitle = signal('Statement');
  statement = signal<{ asOf: string; items: { invoiceId: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; daysOverdue: number; outstanding: number }[]; unallocatedReceipts?: number; unallocatedPayments?: number; balance: number } | null>(null);

  loading = signal(true);
  error = signal<string | null>(null);
  party = signal<Party | null>(null);
  transactions = signal<Transaction[]>([]);
  loadingTx = signal(false);

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

    if (this.isCustomer()) {
      cards.push(
        {
          title: 'Total Sales',
          value: p.totalSales ?? 0,
          suffix: p.currencyCode ?? 'USD'
        },
        {
          title: 'Outstanding',
          value: p.outstandingReceivables ?? 0,
          suffix: p.currencyCode ?? 'USD',
          valueStyle: { color: (p.outstandingReceivables ?? 0) > 0 ? '#fa8c16' : '#52c41a' }
        }
      );

      if (p.creditLimit) {
        cards.push({
          title: 'Credit Limit',
          value: p.creditLimit,
          suffix: p.currencyCode ?? 'USD'
        });
      }
    }

    if (this.isVendor()) {
      cards.push(
        {
          title: 'Total Purchases',
          value: p.totalPurchases ?? 0,
          suffix: p.currencyCode ?? 'USD'
        },
        {
          title: 'Outstanding Payables',
          value: p.outstandingPayables ?? 0,
          suffix: p.currencyCode ?? 'USD',
          valueStyle: { color: (p.outstandingPayables ?? 0) > 0 ? '#fa8c16' : '#52c41a' }
        }
      );
    }

    if (p.paymentTermsDays) {
      cards.push({
        title: 'Payment Terms',
        value: `${p.paymentTermsDays} days`
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
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load party');
        this.loading.set(false);
      }
    });
  }

  loadTransactions(partyId: string): void {
    this.loadingTx.set(true);
    
    const endpoints: string[] = [];
    if (this.isCustomer()) {
      endpoints.push(`${this.apiBase}/finance/invoices?filter=partyId:eq:${partyId}&pageSize=10`);
      endpoints.push(`${this.apiBase}/finance/receipts?filter=partyId:eq:${partyId}&pageSize=10`);
    }
    if (this.isVendor()) {
      endpoints.push(`${this.apiBase}/finance/bills?filter=partyId:eq:${partyId}&pageSize=10`);
      endpoints.push(`${this.apiBase}/finance/payments?filter=partyId:eq:${partyId}&pageSize=10`);
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
                type,
                number: item.number,
                date: item.invoiceDate || item.billDate || item.date,
                amount: item.total || item.amount || 0,
                outstanding: item.total - (item.paid || 0),
                status: item.status
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
      });
  }

  /** GET /finance/reports/{customer|vendor}-statement (open items as of today). */
  openStatement(kind: 'customer' | 'vendor'): void {
    const p = this.party();
    if (!p) return;
    const d = new Date();
    const asOf = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.statement.set(null);
    this.statementTitle.set(`${kind === 'customer' ? 'Customer' : 'Vendor'} statement · ${p.name}`);
    this.statementOpen.set(true);
    this.http.get<any>(`${this.apiBase}/finance/reports/${kind}-statement`, { params: { partyId: p.id, asOf } }).subscribe({
      next: (res) => this.statement.set(res),
      error: (err) => { this.statementOpen.set(false); this.message.error(err?.error?.detail || err?.error?.message || 'Failed to load statement'); }
    });
  }

  viewTransaction(tx: Transaction): void {
    const routes: Record<string, string> = {
      'Invoice': '/finance/invoices',
      'Receipt': '/finance/receipts',
      'Bill': '/finance/bills',
      'Payment': '/finance/payments'
    };
    this.router.navigate([routes[tx.type], tx.id]);
  }
}
