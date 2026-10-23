import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, of } from 'rxjs';
import {
  AccountDto, AccountsApi, ArAgeing, BalanceSheet, GeneralLedger, IncomeStatement, LedgerReportsApi, LegalEntitiesApi, LegalEntityDto, TrialBalance,
} from '../../core/api/admin.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem } from '../../core/http/problem-details';

/** Ledger reports on posted entries: trial balance, general ledger, income statement and balance sheet. */
@Component({
  selector: 'erp-ledger-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DecimalPipe, NgTemplateOutlet, NzTabsModule, NzTableModule, NzSelectModule, NzDatePickerModule, NzButtonModule, NzAlertModule],
  styles: [`.bar { display:flex; gap:12px; flex-wrap:wrap; margin-bottom:16px; align-items:center } .num { text-align:right } .total td { font-weight:600 } h4 { margin: 16px 0 4px }`],
  template: `
    <h1 class="erp-page-title">Financial reports</h1>
    <div class="bar">
      <nz-select [(ngModel)]="entityId" nzAllowClear nzPlaceHolder="All legal entities" style="width:240px" data-testid="rep-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select>
      <nz-date-picker [(ngModel)]="from" nzPlaceHolder="From" data-testid="rep-from"></nz-date-picker>
      <nz-date-picker [(ngModel)]="to" nzPlaceHolder="To / as of" data-testid="rep-to"></nz-date-picker>
      @if (tab() === 1) {
        <nz-select [(ngModel)]="accountId" nzShowSearch nzPlaceHolder="Account" style="width:280px" data-testid="rep-account">@for (a of accounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
      }
      <button nz-button nzType="primary" [nzLoading]="loading()" (click)="run()" data-testid="rep-run">Run</button>
    </div>
    <nz-tabset [nzSelectedIndex]="tab()" (nzSelectedIndexChange)="tab.set($event)">
      <nz-tab nzTitle="Trial balance"></nz-tab><nz-tab nzTitle="General ledger"></nz-tab><nz-tab nzTitle="Income statement"></nz-tab><nz-tab nzTitle="Balance sheet"></nz-tab><nz-tab nzTitle="Receivables ageing"></nz-tab><nz-tab nzTitle="Payables ageing"></nz-tab>
    </nz-tabset>

    @switch (tab()) {
      @case (0) {
        @if (tb(); as t) {
          @if (!t.balanced) { <nz-alert nzType="error" nzShowIcon nzMessage="The trial balance does not balance."></nz-alert> }
          <nz-table [nzData]="t.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="tb-table">
            <thead><tr><th>Code</th><th>Account</th><th class="num">Opening</th><th class="num">Debit</th><th class="num">Credit</th><th class="num">Closing</th></tr></thead>
            <tbody>
              @for (r of t.rows; track r.accountId) { <tr><td>{{ r.code }}</td><td>{{ r.name }}</td><td class="num">{{ r.opening | number: '1.2-2' }}</td><td class="num">{{ r.debit | number: '1.2-2' }}</td><td class="num">{{ r.credit | number: '1.2-2' }}</td><td class="num">{{ r.closing | number: '1.2-2' }}</td></tr> }
              <tr class="total"><td colspan="3">Total</td><td class="num">{{ t.totalDebit | number: '1.2-2' }}</td><td class="num">{{ t.totalCredit | number: '1.2-2' }}</td><td></td></tr>
            </tbody>
          </nz-table>
        }
      }
      @case (1) {
        @if (gl(); as g) {
          <h4>{{ g.code }} · {{ g.name }} — opening {{ g.opening | number: '1.2-2' }}</h4>
          <nz-table [nzData]="g.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="gl-table">
            <thead><tr><th>Date</th><th>Entry</th><th>Description</th><th class="num">Debit</th><th class="num">Credit</th><th class="num">Balance</th></tr></thead>
            <tbody>
              @for (l of g.lines; track $index) { <tr><td>{{ l.date }}</td><td>{{ l.number }}</td><td>{{ l.description || l.memo }}</td><td class="num">{{ l.debit | number: '1.2-2' }}</td><td class="num">{{ l.credit | number: '1.2-2' }}</td><td class="num">{{ l.balance | number: '1.2-2' }}</td></tr> }
              <tr class="total"><td colspan="5">Closing balance</td><td class="num">{{ g.closing | number: '1.2-2' }}</td></tr>
            </tbody>
          </nz-table>
        }
      }
      @case (2) {
        @if (pl(); as p) {
          <h4>Income</h4>
          <ng-container *ngTemplateOutlet="lines; context: { $implicit: p.income, total: p.totalIncome, label: 'Total income' }" />
          <h4>Expenses</h4>
          <ng-container *ngTemplateOutlet="lines; context: { $implicit: p.expenses, total: p.totalExpenses, label: 'Total expenses' }" />
          <h4 data-testid="net-profit">Net profit: {{ p.netProfit | number: '1.2-2' }}</h4>
        }
      }
      @case (3) {
        @if (bs(); as b) {
          @if (!b.balanced) { <nz-alert nzType="error" nzShowIcon nzMessage="The balance sheet does not balance."></nz-alert> }
          <h4>Assets</h4>
          <ng-container *ngTemplateOutlet="lines; context: { $implicit: b.assets, total: b.totalAssets, label: 'Total assets' }" />
          <h4>Liabilities</h4>
          <ng-container *ngTemplateOutlet="lines; context: { $implicit: b.liabilities, total: b.totalLiabilities, label: 'Total liabilities' }" />
          <h4>Equity</h4>
          <ng-container *ngTemplateOutlet="lines; context: { $implicit: b.equity, total: b.totalEquity + b.currentEarnings, label: 'Total equity (including current earnings ' + (b.currentEarnings | number: '1.2-2') + ')' }" />
        }
      }
      @case (4) {
        @if (ar(); as a) {
          <nz-table [nzData]="a.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="ar-table">
            <thead><tr><th>Customer</th><th class="num">Current</th><th class="num">1-30</th><th class="num">31-60</th><th class="num">61-90</th><th class="num">Over 90</th><th class="num">Total</th></tr></thead>
            <tbody>
              @for (r of a.rows; track r.partyId) { <tr><td>{{ r.partyName }}</td><td class="num">{{ r.current | number: '1.2-2' }}</td><td class="num">{{ r.days1To30 | number: '1.2-2' }}</td><td class="num">{{ r.days31To60 | number: '1.2-2' }}</td><td class="num">{{ r.days61To90 | number: '1.2-2' }}</td><td class="num">{{ r.over90 | number: '1.2-2' }}</td><td class="num">{{ r.total | number: '1.2-2' }}</td></tr> }
              <tr class="total"><td>Total</td><td class="num">{{ a.totals.current | number: '1.2-2' }}</td><td class="num">{{ a.totals.days1To30 | number: '1.2-2' }}</td><td class="num">{{ a.totals.days31To60 | number: '1.2-2' }}</td><td class="num">{{ a.totals.days61To90 | number: '1.2-2' }}</td><td class="num">{{ a.totals.over90 | number: '1.2-2' }}</td><td class="num">{{ a.totals.total | number: '1.2-2' }}</td></tr>
            </tbody>
          </nz-table>
        }
      }
      @case (5) {
        @if (ap(); as a) {
          <nz-table [nzData]="a.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="ap-table">
            <thead><tr><th>Vendor</th><th class="num">Current</th><th class="num">1-30</th><th class="num">31-60</th><th class="num">61-90</th><th class="num">Over 90</th><th class="num">Total</th></tr></thead>
            <tbody>
              @for (r of a.rows; track r.partyId) { <tr><td>{{ r.partyName }}</td><td class="num">{{ r.current | number: '1.2-2' }}</td><td class="num">{{ r.days1To30 | number: '1.2-2' }}</td><td class="num">{{ r.days31To60 | number: '1.2-2' }}</td><td class="num">{{ r.days61To90 | number: '1.2-2' }}</td><td class="num">{{ r.over90 | number: '1.2-2' }}</td><td class="num">{{ r.total | number: '1.2-2' }}</td></tr> }
              <tr class="total"><td>Total</td><td class="num">{{ a.totals.current | number: '1.2-2' }}</td><td class="num">{{ a.totals.days1To30 | number: '1.2-2' }}</td><td class="num">{{ a.totals.days31To60 | number: '1.2-2' }}</td><td class="num">{{ a.totals.days61To90 | number: '1.2-2' }}</td><td class="num">{{ a.totals.over90 | number: '1.2-2' }}</td><td class="num">{{ a.totals.total | number: '1.2-2' }}</td></tr>
            </tbody>
          </nz-table>
        }
      }
    }
    <ng-template #lines let-rows let-total="total" let-label="label">
      <nz-table [nzData]="rows" [nzFrontPagination]="false" nzSize="small" [nzShowPagination]="false">
        <tbody>
          @for (l of rows; track l.code) { <tr><td>{{ l.code }}</td><td>{{ l.name }}</td><td class="num">{{ l.amount | number: '1.2-2' }}</td></tr> }
          <tr class="total"><td colspan="2">{{ label }}</td><td class="num">{{ total | number: '1.2-2' }}</td></tr>
        </tbody>
      </nz-table>
    </ng-template>`,
})
export class LedgerReportsPage {
  private readonly api = inject(LedgerReportsApi);
  private readonly message = inject(NzMessageService);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly accounts = computed(() => this.accountPage().items);
  readonly tab = signal(Number(inject(ActivatedRoute).snapshot.queryParamMap.get('tab') ?? 0) || 0);
  readonly loading = signal(false);
  entityId: string | null = null;
  accountId: string | null = null;
  from: Date | null = new Date(new Date().getFullYear(), 0, 1);
  to: Date | null = new Date();
  readonly tb = signal<TrialBalance | null>(null);
  readonly gl = signal<GeneralLedger | null>(null);
  readonly pl = signal<IncomeStatement | null>(null);
  readonly bs = signal<BalanceSheet | null>(null);
  readonly ar = signal<ArAgeing | null>(null);
  readonly ap = signal<ArAgeing | null>(null);

  run(): void {
    if (!this.to) { this.message.warning('Choose the "to" date'); return; }
    const to = toIsoDate(this.to);
    const from = this.from ? toIsoDate(this.from) : undefined;
    const legalEntityId = this.entityId ?? undefined;
    const done = <T>(set: (v: T) => void) => ({ next: (v: T) => { this.loading.set(false); set(v); }, error: (e: unknown) => { this.loading.set(false); this.message.error(asProblem(e)?.detail ?? 'Could not run the report'); } });
    this.loading.set(true);
    switch (this.tab()) {
      case 0: this.api.trialBalance({ from, to, legalEntityId }).subscribe(done((v) => this.tb.set(v))); break;
      case 1:
        if (!this.accountId) { this.loading.set(false); this.message.warning('Choose an account'); return; }
        this.api.generalLedger({ accountId: this.accountId, from, to, legalEntityId }).subscribe(done((v) => this.gl.set(v))); break;
      case 2:
        if (!from) { this.loading.set(false); this.message.warning('Choose the "from" date'); return; }
        this.api.incomeStatement({ from, to, legalEntityId }).subscribe(done((v) => this.pl.set(v))); break;
      case 3: this.api.balanceSheet({ asOf: to, legalEntityId }).subscribe(done((v) => this.bs.set(v))); break;
      case 4: this.api.arAgeing({ asOf: to, legalEntityId }).subscribe(done((v) => this.ar.set(v))); break;
      default: this.api.apAgeing({ asOf: to, legalEntityId }).subscribe(done((v) => this.ap.set(v)));
    }
  }
}
