import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
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
} from '@core/api/admin.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

/** Ledger reports on posted entries: trial balance, general ledger, income statement and balance sheet. */
@Component({
  selector: 'erp-ledger-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, HasPermissionDirective, ErpDatePipe, StatePanelComponent, NzSpinModule, TranslatePipe, FormsModule, DecimalPipe, NgTemplateOutlet, NzTabsModule, NzTableModule, NzInputModule, NzSelectModule, NzDatePickerModule, NzButtonModule, NzAlertModule],
  styleUrl: './ledger-reports.page.scss',
  templateUrl: './ledger-reports.page.html',
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
