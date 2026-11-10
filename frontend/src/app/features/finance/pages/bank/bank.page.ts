import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi, BankApi, CashPosition, LegalEntitiesApi, LegalEntityDto, Reconciliation, StatementLine } from '@core/api/admin.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

/** Cash and bank: balances, bank statement import, reconciliation and transfers between accounts. */
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { countOf } from '../../finance-kpis';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';
@Component({
  selector: 'erp-bank',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, ErpDatePipe, NzSpinModule, StatePanelComponent, PaginationBarComponent, KeyboardClickDirective, NzToolTipModule, TranslatePipe, FormsModule, DecimalPipe, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule, NzTableModule, NzTagModule, RevealDirective],
  styleUrl: './bank.page.scss',
  templateUrl: './bank.page.html',
})
export class BankPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(BankApi);
  private readonly message = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly postable = computed(() => this.accountPage().items.filter((a) => a.isPostable && a.isActive));
  readonly cashAccounts = computed(() => this.postable().filter((a) => a.type === 'asset' && (a.isBank || a.systemRole === 'cash' || a.systemRole === 'bank')));
  entityId: string | null = null;
  asOf: Date = new Date();
  readonly position = signal<CashPosition | null>(null);
  readonly accountId = signal<string | null>(null);
  readonly rec = signal<Reconciliation | null>(null);
  readonly failed = signal(false);
  readonly entityChoice = signal<string | null>(null);
  /** The legal entity a statement belongs to: the one chosen, or the only one there is. */
  readonly statementEntity = computed(() => this.entityChoice() ?? (this.entities().length === 1 ? this.entities()[0].id : null));
  readonly stPg = new ClientPager(10);
  readonly ltPg = new ClientPager(10);
  /** Newest first, one page at a time: a bank with no statement imported can have hundreds of open ledger lines. */
  readonly statementRows = computed(() => [...(this.rec()?.unmatchedStatement ?? [])].sort((a, b) => b.date.localeCompare(a.date)));
  readonly ledgerRows = computed(() => [...(this.rec()?.unmatchedLedger ?? [])].sort((a, b) => b.date.localeCompare(a.date)));
  readonly busy = signal(false);
  readonly importOpen = signal(false);
  importText = '';
  readonly bookLine = signal<StatementLine | null>(null);
  bookAccount: string | null = null;
  bookMemo = '';
  readonly transferOpen = signal(false);
  trEntity: string | null = null; trFrom: string | null = null; trTo: string | null = null; trAmount: number | null = null; trDate: Date = new Date();

  constructor() { this.refresh(); }

  private date(): string { return toIsoDate(this.asOf); }

  refresh(): void {
    this.failed.set(false);
    this.entityChoice.set(this.entityId);
    this.api.position(this.date(), this.entityId ?? undefined).subscribe({
      next: (p) => {
        this.position.set(p);
        const id = this.accountId() ?? p.accounts.find((a) => a.isBank)?.accountId ?? p.accounts[0]?.accountId ?? null;
        if (id) this.select(id);
      },
      error: () => this.failed.set(true),
    });
  }

  select(accountId: string): void {
    this.accountId.set(accountId);
    this.stPg.page.set(1);
    this.ltPg.page.set(1);
    this.api.reconciliation(accountId, this.date(), this.entityId ?? undefined).subscribe({ next: (r) => this.rec.set(r), error: (e) => this.fail(e) });
  }

  openTransfer(): void {
    this.trEntity = this.statementEntity();
    this.transferOpen.set(true);
  }

  runImport(): void {
    const account = this.accountId();
    const entity = this.statementEntity();
    if (!account || !entity) { this.message.warning('Choose a legal entity and an account'); return; }
    const lines: { date: string; description: string; reference: string | null; amount: number }[] = [];
    for (const raw of this.importText.split('\n').map((l) => l.trim()).filter(Boolean)) {
      const [date, description, reference, amount] = raw.split(',').map((p) => p.trim());
      const value = Number(amount);
      if (!/^\d{4}-\d{2}-\d{2}$/.test(date ?? '') || !description || !Number.isFinite(value) || value === 0) { this.message.error(`${this.i18n.t('ui.bank.cannotReadLine', 'Cannot read this line:')} ${raw}`); return; }
      lines.push({ date, description, reference: reference || null, amount: value });
    }
    if (lines.length === 0) return;
    this.busy.set(true);
    this.api.import(account, entity, lines).subscribe({
      next: (r) => { this.busy.set(false); this.importOpen.set(false); this.importText = ''; this.message.success(`${r.imported} ${this.i18n.t('ui.msg.importedLc', 'imported')}, ${r.duplicates} ${this.i18n.t('ui.msg.alreadyThere', 'already there')}`); this.select(account); },
      error: (e) => this.fail(e),
    });
  }

  autoMatch(): void {
    const account = this.accountId();
    const entity = this.statementEntity();
    if (!account || !entity) return;
    this.api.autoMatch(account, entity).subscribe({ next: (r) => { this.message.success(r.matched ? `${countOf(r.matched, 'line')} matched` : 'Nothing new to match'); this.select(account); }, error: (e) => this.fail(e) });
  }

  startBook(l: StatementLine): void { this.bookLine.set(l); this.bookAccount = null; this.bookMemo = ''; }

  runBook(): void {
    const l = this.bookLine();
    if (!l || !this.bookAccount) { this.message.warning('Choose the account to book it to'); return; }
    this.busy.set(true);
    this.api.book(l.id, this.bookAccount, this.bookMemo || undefined).subscribe({
      next: () => { this.busy.set(false); this.bookLine.set(null); this.message.success(`Statement line ${l.description} booked and matched`); this.refresh(); },
      error: (e) => this.fail(e),
    });
  }

  runTransfer(): void {
    if (!this.trEntity || !this.trFrom || !this.trTo || !this.trAmount) { this.message.warning('Fill in every field'); return; }
    if (this.trFrom === this.trTo) { this.message.warning('Choose two different accounts'); return; }
    this.busy.set(true);
    this.api.transfer({ legalEntityId: this.trEntity, fromAccountId: this.trFrom, toAccountId: this.trTo, amount: this.trAmount, date: toIsoDate(this.trDate), memo: null }).subscribe({
      next: () => { this.busy.set(false); this.transferOpen.set(false); this.message.success(doneText('posted', 'Bank transfer')); this.refresh(); },
      error: (e) => this.fail(e),
    });
  }

  private fail(e: unknown): void { this.busy.set(false); this.message.error(problemMessage(e, 'Could not complete the action')); }
}
