import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, of } from 'rxjs';
import {
  AccountDto, AccountsApi, ArAgeing, CashFlowStatement, BusinessUnitDto, BusinessUnitsApi, BalanceSheet, GeneralLedger, IncomeStatement, LedgerReportsApi, LegalEntitiesApi, PartyGroupDto, PartyGroupsApi, LegalEntityDto, TrialBalance,
} from '../../core/api/admin.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

/** Ledger reports on posted entries: trial balance, general ledger, income statement and balance sheet. */
@Component({
  selector: 'erp-ledger-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, HasPermissionDirective, ErpDatePipe, StatePanelComponent, NzSpinModule, TranslatePipe, FormsModule, DecimalPipe, NgTemplateOutlet, NzTabsModule, NzTableModule, NzInputModule, NzSelectModule, NzDatePickerModule, NzButtonModule, NzAlertModule],
  styles: [`.bar { display:flex; gap:12px; flex-wrap:wrap; margin-bottom:16px; align-items:flex-end }
    .field { display:flex; flex-direction:column; gap:4px; font-size:12px; color: var(--erp-color-text-muted) }
    .num { text-align:end; font-variant-numeric: tabular-nums; white-space:nowrap }
    .total td { font-weight:700; background: var(--erp-color-bg-subtle) } h4 { margin: 16px 0 8px; font-size:15px; font-weight:700; color: var(--erp-color-text-primary) }
    .name { font-weight:600; color: var(--erp-color-text-primary) }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius:12px; overflow:hidden; margin-top:12px }
    .links { display:flex; gap:16px; margin: -8px 0 12px } .group { color: var(--erp-color-text-muted); font-size:12px } .check { margin-top:12px } .loading { text-align:center; padding:48px }
    .result { display:flex; justify-content:space-between; gap:16px; margin-top:16px; padding:14px 16px; border-radius:12px; font-size:16px; font-weight:700; background: var(--erp-color-bg-surface); border:1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-primary) }`],
  template: `
    <h1 class="erp-page-title">Financial reports</h1>
    <p class="links">
      <a *erpHasPermission="'finance.yearend.close'" routerLink="/finance/year-end" data-testid="link-year-end">Year-end close</a>
      <a *erpHasPermission="'finance.journals.post'" routerLink="/finance/opening-balances" data-testid="link-opening">Opening balances</a>
    </p>
    <div class="bar">
      <div class="field">Legal entity
        <nz-select [(ngModel)]="entityId" nzAllowClear nzPlaceHolder="All legal entities" style="width:240px" data-testid="rep-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select>
      </div>
      @if (usesFrom()) { <div class="field">From <nz-date-picker [(ngModel)]="from" nzFormat="dd MMM yyyy" data-testid="rep-from"></nz-date-picker></div> }
      <div class="field">{{ usesFrom() ? 'To' : 'As of' }} <nz-date-picker [(ngModel)]="to" nzFormat="dd MMM yyyy" data-testid="rep-to"></nz-date-picker></div>
      @if (tab() === 4) {
        <div class="field">Group by
          <nz-select [(ngModel)]="groupBy" style="width:170px" data-testid="rep-groupby">
            <nz-option nzValue="customer" nzLabel="Customer"></nz-option>
            <nz-option nzValue="businessUnit" nzLabel="Business unit"></nz-option>
            <nz-option nzValue="sourceType" nzLabel="Source"></nz-option>
            <nz-option nzValue="partyGroup" nzLabel="Party group"></nz-option>
          </nz-select>
        </div>
        <div class="field">Business unit <nz-select [(ngModel)]="businessUnitId" nzAllowClear nzPlaceHolder="All business units" style="width:200px" data-testid="rep-bu">@for (b of units(); track b.id) { <nz-option [nzValue]="b.id" [nzLabel]="b.name"></nz-option> }</nz-select></div>
        <div class="field">Party group <nz-select [(ngModel)]="partyGroupId" nzAllowClear nzPlaceHolder="All party groups" style="width:180px" data-testid="rep-party-group">@for (g of partyGroups(); track g.id) { <nz-option [nzValue]="g.id" [nzLabel]="g.name"></nz-option> }</nz-select></div>
        <div class="field">Source type <input nz-input [(ngModel)]="sourceType" placeholder="Optional" aria-label="Source type" style="width:150px" data-testid="rep-source" /></div>
      }
      @if (tab() === 1) {
        <div class="field">Account
          <nz-select [(ngModel)]="accountId" (ngModelChange)="accountId = $event; run()" nzShowSearch nzPlaceHolder="Choose an account" style="width:280px" data-testid="rep-account">@for (a of accounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
        </div>
      }
      <button nz-button nzType="primary" [nzLoading]="loading()" (click)="run()" data-testid="rep-run">Run report</button>
    </div>
    <nz-tabset [nzSelectedIndex]="tab()" (nzSelectedIndexChange)="selectTab($event)">
      <nz-tab nzTitle="Trial balance"></nz-tab><nz-tab nzTitle="General ledger"></nz-tab><nz-tab nzTitle="Income statement"></nz-tab><nz-tab nzTitle="Balance sheet"></nz-tab><nz-tab nzTitle="Receivables ageing"></nz-tab><nz-tab nzTitle="Payables ageing"></nz-tab><nz-tab nzTitle="Cash flow"></nz-tab>
    </nz-tabset>

    @if (failed(); as msg) {
      <erp-state-panel kind="error" title="Could not run this report" [hint]="msg" actionLabel="Try again" (action)="run()" />
    } @else if (needsAccount()) {
      <erp-state-panel title="Choose an account" hint="The general ledger shows the entries of one account. Pick it above and the report runs." icon="book" />
    } @else if (!hasResult() && loading()) {
      <div class="loading"><nz-spin nzSimple></nz-spin></div>
    } @else {
      @switch (tab()) {
        @case (0) {
          @if (tb(); as t) {
            @if (t.rows.length) {
              @if (t.balanced) { <nz-alert class="check" nzType="success" nzShowIcon data-testid="tb-balanced" [nzMessage]="'Balanced. Debits and credits both come to ' + money(t.totalDebit) + '.'"></nz-alert> }
              @else { <nz-alert class="check" nzType="error" nzShowIcon data-testid="tb-unbalanced" [nzMessage]="'Does not balance. Debits and credits differ by ' + money(t.totalDebit - t.totalCredit) + '.'"></nz-alert> }
              <div class="card">
                <nz-table [nzData]="t.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" data-testid="tb-table">
                  <thead><tr><th>Code</th><th>Account</th><th class="num">Opening</th><th class="num">Debit</th><th class="num">Credit</th><th class="num">Closing</th></tr></thead>
                  <tbody>
                    @for (r of t.rows; track r.accountId) { <tr><td><span class="code-pill">{{ r.code }}</span></td><td class="name">{{ r.name }}</td><td class="num">{{ drCr(r.opening) }}</td><td class="num">{{ r.debit | number: '1.2-2' }}</td><td class="num">{{ r.credit | number: '1.2-2' }}</td><td class="num">{{ drCr(r.closing) }}</td></tr> }
                    <tr class="total"><td colspan="3">Total</td><td class="num">{{ t.totalDebit | number: '1.2-2' }}</td><td class="num">{{ t.totalCredit | number: '1.2-2' }}</td><td></td></tr>
                  </tbody>
                </nz-table>
              </div>
            } @else { <erp-state-panel title="Nothing posted in this period" hint="Change the dates, or post a journal or document first." icon="calculator" /> }
          }
        }
        @case (1) {
          @if (gl(); as g) {
            <h4><span class="code-pill">{{ g.code }}</span> {{ g.name }}: {{ 'ui.ledger.openingLc' | translate: 'opening' }} {{ drCr(g.opening) }}</h4>
            @if (g.lines.length) {
              <div class="card">
                <nz-table [nzData]="g.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" data-testid="gl-table">
                  <thead><tr><th>Date</th><th>Entry</th><th>Description</th><th class="num">Debit</th><th class="num">Credit</th><th class="num">Balance</th></tr></thead>
                  <tbody>
                    @for (l of g.lines; track $index) { <tr><td style="white-space:nowrap">{{ l.date | erpDate }}</td><td><span class="code-pill">{{ l.number }}</span></td><td>{{ l.description || l.memo }}</td><td class="num">{{ l.debit | number: '1.2-2' }}</td><td class="num">{{ l.credit | number: '1.2-2' }}</td><td class="num">{{ drCr(l.balance) }}</td></tr> }
                    <tr class="total"><td colspan="5">Closing balance</td><td class="num">{{ drCr(g.closing) }}</td></tr>
                  </tbody>
                </nz-table>
              </div>
            } @else { <erp-state-panel title="No entries in this period" hint="This account has no posted entries between these dates." icon="book" /> }
          }
        }
        @case (2) {
          @if (pl(); as p) {
            <h4>Income</h4>
            <ng-container *ngTemplateOutlet="lines; context: { $implicit: p.income, total: p.totalIncome, label: 'Total income' }" />
            <h4>Expenses</h4>
            <ng-container *ngTemplateOutlet="lines; context: { $implicit: p.expenses, total: p.totalExpenses, label: 'Total expenses' }" />
            <div class="result" data-testid="net-profit"><span>{{ p.netProfit < 0 ? 'Net loss' : ('ui.ledger.netProfit' | translate: 'Net profit') }}</span><span class="num">{{ money(p.netProfit) }}</span></div>
          }
        }
        @case (3) {
          @if (bs(); as b) {
            @if (b.balanced) { <nz-alert class="check" nzType="success" nzShowIcon data-testid="bs-balanced" [nzMessage]="'Balanced. Assets equal liabilities plus equity: ' + money(b.totalAssets) + '.'"></nz-alert> }
            @else { <nz-alert class="check" nzType="error" nzShowIcon [nzMessage]="'Does not balance. Assets ' + money(b.totalAssets) + ', liabilities plus equity ' + money(b.totalLiabilities + b.totalEquity + b.currentEarnings + (b.priorOpenEarnings ?? 0)) + '.'"></nz-alert> }
            <h4>Assets</h4>
            <ng-container *ngTemplateOutlet="lines; context: { $implicit: b.assets, total: b.totalAssets, label: 'Total assets' }" />
            <h4>Liabilities</h4>
            <ng-container *ngTemplateOutlet="lines; context: { $implicit: b.liabilities, total: b.totalLiabilities, label: 'Total liabilities' }" />
            <h4>Equity</h4>
            <ng-container *ngTemplateOutlet="lines; context: { $implicit: b.equity, total: b.totalEquity + b.currentEarnings + (b.priorOpenEarnings ?? 0), label: 'Total equity, including current earnings of ' + (b.currentEarnings | number: '1.2-2') + ((b.priorOpenEarnings ?? 0) ? ' and earnings of years not yet closed of ' + (b.priorOpenEarnings! | number: '1.2-2') : '') }" />
          }
        }
        @case (4) {
          @if (ar(); as a) {
            @if (a.rows.length) { <ng-container *ngTemplateOutlet="ageing; context: { $implicit: a, name: groupBy === 'businessUnit' ? 'Business unit' : groupBy === 'sourceType' ? 'Source' : groupBy === 'partyGroup' ? 'Party group' : 'Customer', id: 'ar-table' }" /> }
            @else { <erp-state-panel title="Nothing to collect" hint="No customer owes money at this date." icon="check-circle" /> }
          }
        }
        @case (6) {
          @if (cf(); as c) {
            @if (c.tiesOut) { <nz-alert class="check" nzType="success" nzShowIcon data-testid="cf-ties" [nzMessage]="'The statement agrees with the bank and cash balance in the ledger: ' + money(c.ledgerClosingBalance) + '.'"></nz-alert> }
            @else { <nz-alert class="check" nzType="warning" nzShowIcon data-testid="cf-differs" [nzMessage]="'The statement closes at ' + money(c.closingBalance) + ' but the ledger holds ' + money(c.ledgerClosingBalance) + ' in bank and cash accounts. Check for entries dated outside the period.'"></nz-alert> }
            <div class="card">
              <nz-table [nzData]="c.sections" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="cf-table">
                <tbody>
                  <tr class="total"><td colspan="2">Opening cash and bank</td><td class="num">{{ c.openingBalance | number: '1.2-2' }}</td></tr>
                  @for (s of c.sections; track s.category) {
                    <tr class="total"><td colspan="2">{{ cfTitle(s.category) }}</td><td class="num">{{ s.net | number: '1.2-2' }}</td></tr>
                    @for (l of s.lines; track l.code) { <tr><td style="width:120px"><span class="code-pill">{{ l.code }}</span></td><td>{{ l.name }}<span class="group"> {{ l.group }}</span></td><td class="num">{{ l.net | number: '1.2-2' }}</td></tr> }
                    @if (!s.lines.length) { <tr><td colspan="3" class="group">Nothing in this period.</td></tr> }
                  }
                  <tr class="total"><td colspan="2">Net change in cash</td><td class="num">{{ c.netChange | number: '1.2-2' }}</td></tr>
                  <tr class="total"><td colspan="2">Closing cash and bank</td><td class="num">{{ c.closingBalance | number: '1.2-2' }}</td></tr>
                </tbody>
              </nz-table>
            </div>
            @if (c.internalTransfers) { <p class="group" style="margin-top:8px">Transfers between your own bank and cash accounts ({{ c.internalTransfers | number: '1.2-2' }}) are left out, as they do not change total cash.</p> }
          }
        }
        @case (5) {
          @if (ap(); as a) {
            @if (a.rows.length) { <ng-container *ngTemplateOutlet="ageing; context: { $implicit: a, name: 'Vendor', id: 'ap-table' }" /> }
            @else { <erp-state-panel title="Nothing to pay" hint="No vendor is owed money at this date." icon="check-circle" /> }
          }
        }
      }
    }
    <ng-template #ageing let-a let-name="name" let-id="id">
      <div class="card">
        <nz-table [nzData]="a.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" [attr.data-testid]="id">
          <thead><tr><th>{{ name }}</th><th class="num">Current</th><th class="num">1-30 days</th><th class="num">31-60 days</th><th class="num">61-90 days</th><th class="num">Over 90 days</th><th class="num">Total</th></tr></thead>
          <tbody>
            @for (r of a.rows; track r.partyId) { <tr><td class="name">{{ r.partyName }}</td><td class="num">{{ r.current | number: '1.2-2' }}</td><td class="num">{{ r.days1To30 | number: '1.2-2' }}</td><td class="num">{{ r.days31To60 | number: '1.2-2' }}</td><td class="num">{{ r.days61To90 | number: '1.2-2' }}</td><td class="num">{{ r.over90 | number: '1.2-2' }}</td><td class="num">{{ r.total | number: '1.2-2' }}</td></tr> }
            <tr class="total"><td>Total</td><td class="num">{{ a.totals.current | number: '1.2-2' }}</td><td class="num">{{ a.totals.days1To30 | number: '1.2-2' }}</td><td class="num">{{ a.totals.days31To60 | number: '1.2-2' }}</td><td class="num">{{ a.totals.days61To90 | number: '1.2-2' }}</td><td class="num">{{ a.totals.over90 | number: '1.2-2' }}</td><td class="num">{{ a.totals.total | number: '1.2-2' }}</td></tr>
          </tbody>
        </nz-table>
      </div>
    </ng-template>
    <ng-template #lines let-rows let-total="total" let-label="label">
      <div class="card">
        <nz-table [nzData]="rows" [nzFrontPagination]="false" nzSize="small" [nzShowPagination]="false">
          <tbody>
            @for (l of rows; track l.code) { <tr><td style="width:120px"><span class="code-pill">{{ l.code }}</span></td><td>{{ l.name }}</td><td class="num">{{ l.amount | number: '1.2-2' }}</td></tr> }
            <tr class="total"><td colspan="2">{{ label }}</td><td class="num">{{ total | number: '1.2-2' }}</td></tr>
          </tbody>
        </nz-table>
      </div>
    </ng-template>`,
})
export class LedgerReportsPage {
  private readonly api = inject(LedgerReportsApi);
  private readonly message = inject(NzMessageService);
  private readonly currency = inject(WorkspaceCurrencyService);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  private readonly unitPage = toSignal(inject(BusinessUnitsApi).list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] as BusinessUnitDto[] }))), { initialValue: { items: [] as BusinessUnitDto[] } });
  private readonly groupPage = toSignal(inject(PartyGroupsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as PartyGroupDto[] }))), { initialValue: { items: [] as PartyGroupDto[] } });
  readonly partyGroups = computed(() => this.groupPage().items);
  readonly accounts = computed(() => this.accountPage().items);
  readonly tab = signal(Number(inject(ActivatedRoute).snapshot.queryParamMap.get('tab') ?? 0) || 0);
  readonly loading = signal(false);
  readonly failed = signal<string | null>(null);
  /** Balance sheet and ageing are a position at one date; the rest cover a period. */
  readonly usesFrom = computed(() => this.tab() < 3 || this.tab() === 6);
  readonly chosenAccount = signal<string | null>(null);
  readonly needsAccount = computed(() => this.tab() === 1 && !this.chosenAccount());
  readonly hasResult = computed(() => !![this.tb(), this.gl(), this.pl(), this.bs(), this.ar(), this.ap(), this.cf()][this.tab()]);
  entityId: string | null = null;
  accountId: string | null = null;
  from: Date | null = new Date(new Date().getFullYear(), 0, 1);
  to: Date | null = new Date();
  readonly tb = signal<TrialBalance | null>(null);
  readonly gl = signal<GeneralLedger | null>(null);
  readonly pl = signal<IncomeStatement | null>(null);
  readonly bs = signal<BalanceSheet | null>(null);
  groupBy = 'customer';
  businessUnitId: string | null = null;
  partyGroupId: string | null = null;
  sourceType = '';
  readonly units = () => this.unitPage().items.filter((b) => !this.entityId || b.legalEntityId === this.entityId);
  readonly ar = signal<ArAgeing | null>(null);
  readonly ap = signal<ArAgeing | null>(null);
  readonly cf = signal<CashFlowStatement | null>(null);

  constructor() { this.run(); }

  selectTab(index: number): void {
    if (index === this.tab()) return;
    this.tab.set(index);
    this.run();
  }

  cfTitle(category: string): string {
    return category === 'operating' ? 'Cash from operating activities' : category === 'investing' ? 'Cash from investing activities' : 'Cash from financing activities';
  }

  money(v: number): string { return `${this.currency.currency()} ${v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`; }
  /** A ledger balance: debit is positive and credit is negative in the API, which reads better with the side written out. */
  drCr(v: number): string {
    if (!v) return '0.00';
    return `${Math.abs(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${v > 0 ? 'Dr' : 'Cr'}`;
  }

  run(): void {
    this.failed.set(null);
    this.chosenAccount.set(this.accountId);
    if (!this.to) { this.message.warning('Choose the date to report to'); return; }
    const to = toIsoDate(this.to);
    const from = this.from ? toIsoDate(this.from) : undefined;
    const legalEntityId = this.entityId ?? undefined;
    const done = <T>(set: (v: T) => void) => ({ next: (v: T) => { this.loading.set(false); set(v); }, error: (e: unknown) => { this.loading.set(false); this.failed.set(problemMessage(e, 'The server did not return the report.', 'legalEntityId')); } });
    this.loading.set(true);
    switch (this.tab()) {
      case 0: this.api.trialBalance({ from, to, legalEntityId }).subscribe(done((v) => this.tb.set(v))); break;
      case 1:
        if (!this.accountId) { this.loading.set(false); return; }
        this.api.generalLedger({ accountId: this.accountId, from, to, legalEntityId }).subscribe(done((v) => this.gl.set(v))); break;
      case 2:
        if (!from) { this.loading.set(false); this.message.warning('Choose the date to report from'); return; }
        this.api.incomeStatement({ from, to, legalEntityId }).subscribe(done((v) => this.pl.set(v))); break;
      case 3: this.api.balanceSheet({ asOf: to, legalEntityId }).subscribe(done((v) => this.bs.set(v))); break;
      case 4: this.api.arAgeing({ asOf: to, legalEntityId, businessUnitId: this.businessUnitId ?? undefined, partyGroupId: this.partyGroupId ?? undefined, sourceType: this.sourceType.trim() || undefined, groupBy: this.groupBy === 'customer' ? undefined : this.groupBy }).subscribe(done((v) => this.ar.set(v))); break;
      case 5: this.api.apAgeing({ asOf: to, legalEntityId }).subscribe(done((v) => this.ap.set(v))); break;
      default:
        if (!from) { this.loading.set(false); this.message.warning('Choose the date to report from'); return; }
        this.api.cashFlow({ from, to, legalEntityId }).subscribe(done((v) => this.cf.set(v)));
    }
  }
}
