import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { Observable } from 'rxjs';
import {
  ArAgeing, BalanceSheet, BusinessUnitDto, BusinessUnitsApi, CashFlowStatement, GeneralLedger, IncomeStatement, LedgerReportsApi, PartyGroupDto, PartyGroupsApi, TrialBalance,
} from '@core/api/admin.api';
import { toIsoDate } from '@core/format/dates';
import { problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { FinanceLookups } from '../../finance-lookups';

/** The tabs, in order: their index is the `?tab=` value. */
const TAB = { trialBalance: 0, generalLedger: 1, incomeStatement: 2, balanceSheet: 3, arAgeing: 4, apAgeing: 5, cashFlow: 6 } as const;

const amount = (v: number) => v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Ledger reports on posted entries: trial balance, general ledger, income statement, balance sheet, ageing and cash flow. */
@Component({
  selector: 'erp-ledger-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    NgTemplateOutlet,
    RouterLink,
    NzAlertModule,
    NzButtonModule,
    NzDatePickerModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTabsModule,
    TranslatePipe,
    StatePanelComponent,
    HasPermissionDirective,
    ErpDatePipe,
  ],
  providers: [FinanceLookups],
  styleUrl: './ledger-reports.page.scss',
  templateUrl: './ledger-reports.page.html',
})
export class LedgerReportsPage {
  private readonly api = inject(LedgerReportsApi);
  private readonly message = inject(NzMessageService);
  private readonly currency = inject(WorkspaceCurrencyService);
  private readonly lookups = inject(FinanceLookups).load({ entities: true, accounts: true });
  private readonly allUnits = signal<BusinessUnitDto[]>([]);

  readonly entities = this.lookups.entities;
  readonly partyGroups = signal<PartyGroupDto[]>([]);
  readonly accounts = this.lookups.accounts;
  readonly tab = signal(Number(inject(ActivatedRoute).snapshot.queryParamMap.get('tab') ?? 0) || 0);
  readonly loading = signal(false);
  readonly failed = signal<string | null>(null);
  /** Balance sheet and ageing are a position at one date; the rest cover a period. */
  readonly usesFrom = computed(() => this.tab() < TAB.balanceSheet || this.tab() === TAB.cashFlow);
  readonly chosenAccount = signal<string | null>(null);
  readonly needsAccount = computed(() => this.tab() === TAB.generalLedger && !this.chosenAccount());
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
  /** The business units of the chosen legal entity (all of them when none is chosen). */
  readonly units = () => this.allUnits().filter((b) => !this.entityId || b.legalEntityId === this.entityId);
  readonly ar = signal<ArAgeing | null>(null);
  readonly ap = signal<ArAgeing | null>(null);
  readonly cf = signal<CashFlowStatement | null>(null);

  constructor() {
    loadInto(this.allUnits, inject(BusinessUnitsApi).list({ page: 1, pageSize: 200 }), (r) => r.items);
    loadInto(this.partyGroups, inject(PartyGroupsApi).list({ page: 1, pageSize: 200, sort: 'code' }), (r) => r.items);
    this.run();
  }

  selectTab(index: number): void {
    if (index === this.tab()) return;
    this.tab.set(index);
    this.run();
  }

  cfTitle(category: string): string {
    return category === 'operating' ? 'Cash from operating activities' : category === 'investing' ? 'Cash from investing activities' : 'Cash from financing activities';
  }

  money(v: number): string { return `${this.currency.currency()} ${amount(v)}`; }
  /** A ledger balance: debit is positive and credit is negative in the API, which reads better with the side written out. */
  drCr(v: number): string {
    if (!v) return '0.00';
    return `${amount(Math.abs(v))} ${v > 0 ? 'Dr' : 'Cr'}`;
  }

  /** Runs the report of the open tab with the current filters. */
  run(): void {
    this.failed.set(null);
    this.chosenAccount.set(this.accountId);
    if (!this.to) { this.message.warning('Choose the date to report to'); return; }
    const to = toIsoDate(this.to);
    const from = this.from ? toIsoDate(this.from) : undefined;
    const legalEntityId = this.entityId ?? undefined;
    this.loading.set(true);
    switch (this.tab()) {
      case TAB.trialBalance: return this.load(this.api.trialBalance({ from, to, legalEntityId }), this.tb);
      case TAB.generalLedger:
        if (!this.accountId) { this.loading.set(false); return; }
        return this.load(this.api.generalLedger({ accountId: this.accountId, from, to, legalEntityId }), this.gl);
      case TAB.incomeStatement:
        if (!from) return this.needFrom();
        return this.load(this.api.incomeStatement({ from, to, legalEntityId }), this.pl);
      case TAB.balanceSheet: return this.load(this.api.balanceSheet({ asOf: to, legalEntityId }), this.bs);
      case TAB.arAgeing:
        return this.load(this.api.arAgeing({
          asOf: to, legalEntityId, businessUnitId: this.businessUnitId ?? undefined, partyGroupId: this.partyGroupId ?? undefined,
          sourceType: this.sourceType.trim() || undefined, groupBy: this.groupBy === 'customer' ? undefined : this.groupBy,
        }), this.ar);
      case TAB.apAgeing: return this.load(this.api.apAgeing({ asOf: to, legalEntityId }), this.ap);
      default:
        if (!from) return this.needFrom();
        return this.load(this.api.cashFlow({ from, to, legalEntityId }), this.cf);
    }
  }

  /** Shows a report once it arrives, or the server's message in the error panel. */
  private load<T>(report: Observable<T>, into: { set(v: T): void }): void {
    report.subscribe({
      next: (v) => { this.loading.set(false); into.set(v); },
      error: (e: unknown) => { this.loading.set(false); this.failed.set(problemMessage(e, 'The server did not return the report.', 'legalEntityId')); },
    });
  }

  private needFrom(): void {
    this.loading.set(false);
    this.message.warning('Choose the date to report from');
  }
}
