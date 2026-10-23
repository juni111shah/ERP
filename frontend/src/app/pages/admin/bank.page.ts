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
import { AccountDto, AccountsApi, BankApi, CashPosition, LegalEntitiesApi, LegalEntityDto, Reconciliation, StatementLine } from '../../core/api/admin.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';

/** Cash and bank: balances, bank statement import, reconciliation and transfers between accounts. */
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
@Component({
  selector: 'erp-bank',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, TranslatePipe, FormsModule, DecimalPipe, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule, NzTableModule, NzTagModule, RevealDirective],
  styles: [`.bar { display:flex; gap:12px; flex-wrap:wrap; margin-bottom:16px; align-items:center } .cards { display:flex; gap:16px; flex-wrap:wrap; margin-bottom:24px } .card { padding:12px 16px; border:1px solid var(--erp-color-border, #eee); border-radius:8px; min-width:200px; cursor:pointer } .card.sel { border-color: var(--erp-color-primary, #1677ff) } .card b { display:block; font-size:20px } .num { text-align:right } h3 { margin: 20px 0 8px }`],
  template: `
    <h1 class="erp-page-title">Cash and bank</h1>
    <div class="bar" erpReveal>
      <nz-select [(ngModel)]="entityId" nzAllowClear nzPlaceHolder="All legal entities" style="width:240px" (ngModelChange)="refresh()" data-testid="bank-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select>
      <nz-date-picker [(ngModel)]="asOf" (ngModelChange)="refresh()" data-testid="bank-asof"></nz-date-picker>
      <button *erpHasPermission="'finance.bank.write'" nz-button (click)="transferOpen.set(true)" data-testid="bank-transfer"><span nz-icon nzType="swap"></span>Transfer</button>
      <button nz-button nzType="default" type="button" (click)="refresh()" data-testid="bank-refresh"><span nz-icon nzType="reload"></span> {{ 'ui.common.refresh' | translate: 'Refresh' }}</button>
    </div>
    @if (position(); as p) {
      <div class="cards" data-testid="bank-cards">
        @for (a of p.accounts; track a.accountId) {
          <div class="card" [class.sel]="a.accountId === accountId()" (click)="select(a.accountId)" [attr.data-testid]="'bank-card-' + a.code"><span>{{ a.code }} · {{ a.name }}</span><b>{{ a.balance | number: '1.2-2' }}</b></div>
        }
        <div class="card" style="cursor:default"><span>Total</span><b>{{ p.total | number: '1.2-2' }}</b></div>
      </div>
    }
    @if (rec(); as r) {
      <div class="bar" erpReveal>
        <h3 style="margin:0">{{ 'ui.bank.reconciliation' | translate: 'Reconciliation' }} — {{ r.code }} {{ r.name }}</h3>
        <nz-tag [nzColor]="r.reconciled ? 'green' : 'orange'" data-testid="rec-status">{{ r.reconciled ? 'Reconciled' : 'Differences to review' }}</nz-tag>
        <span style="flex:1"></span>
        <ng-container *erpHasPermission="'finance.bank.write'">
          <button nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.importStatement' | translate: 'Import statement'" nz-tooltip [nzTooltipTitle]="'ui.add.importStatement' | translate: 'Import statement'" (click)="importOpen.set(true)" data-testid="bank-import"><span nz-icon nzType="plus"></span>Import statement</button>
          <button nz-button nzType="primary" [disabled]="!entityId" (click)="autoMatch()" data-testid="bank-automatch">Auto-match</button>
        </ng-container>
      </div>
      @if (!entityId) { <p>Choose a legal entity above to import and match statements.</p> }
      <p>{{ 'ui.bank.ledgerBalance' | translate: 'Ledger balance' }} {{ r.ledgerBalance | number: '1.2-2' }} · {{ r.matched }} {{ 'ui.bank.linesMatched' | translate: 'lines matched' }} · {{ 'ui.bank.unmatchedStatement' | translate: 'unmatched statement' }} {{ r.unmatchedStatementTotal | number: '1.2-2' }} · {{ 'ui.bank.unmatchedLedger' | translate: 'unmatched ledger' }} {{ r.unmatchedLedgerTotal | number: '1.2-2' }}</p>
      <h3>On the statement, not in the books</h3>
      <nz-table [nzData]="r.unmatchedStatement" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rec-statement">
        <thead><tr><th>Date</th><th>Description</th><th>Reference</th><th class="num">Amount</th><th></th></tr></thead>
        <tbody>
          @for (l of r.unmatchedStatement; track l.id) {
            <tr><td>{{ l.date }}</td><td>{{ l.description }}</td><td>{{ l.reference }}</td><td class="num">{{ l.amount | number: '1.2-2' }}</td>
              <td><a *erpHasPermission="'finance.bank.write'" (click)="startBook(l)" data-testid="rec-book">Book it</a></td></tr>
          }
        </tbody>
      </nz-table>
      <h3>In the books, not on the statement</h3>
      <nz-table [nzData]="r.unmatchedLedger" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rec-ledger">
        <thead><tr><th>Date</th><th>Entry</th><th>Description</th><th class="num">Amount</th></tr></thead>
        <tbody>@for (l of r.unmatchedLedger; track l.journalLineId) { <tr><td>{{ l.date }}</td><td>{{ l.number }}</td><td>{{ l.description }}</td><td class="num">{{ l.amount | number: '1.2-2' }}</td></tr> }</tbody>
      </nz-table>
    }

    <nz-modal [nzVisible]="importOpen()" nzTitle="Import bank statement" (nzOnCancel)="importOpen.set(false)" (nzOnOk)="runImport()" nzOkText="Import" [nzOkLoading]="busy()" [nzWidth]="640">
      <ng-container *nzModalContent>
        <p>Paste one line per row: <code>date, description, reference, amount</code> (date as 2026-05-31; money out is negative).</p>
        <textarea nz-input rows="10" [(ngModel)]="importText" placeholder="2026-05-03, Deposit, D1, 10000" data-testid="import-text"></textarea>
      </ng-container>
    </nz-modal>

    <nz-modal [nzVisible]="bookLine() !== null" nzTitle="Book statement line" (nzOnCancel)="bookLine.set(null)" (nzOnOk)="runBook()" nzOkText="Book and match" [nzOkLoading]="busy()">
      <ng-container *nzModalContent>
        @if (bookLine(); as l) { <p>{{ l.date }} · {{ l.description }} · <b>{{ l.amount | number: '1.2-2' }}</b></p> }
        <nz-select [(ngModel)]="bookAccount" nzShowSearch style="width:100%;margin-bottom:12px" nzPlaceHolder="Other account (for example bank charges)" data-testid="book-account">
          @for (a of postable(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }
        </nz-select>
        <input nz-input [(ngModel)]="bookMemo" placeholder="Memo (optional)" />
      </ng-container>
    </nz-modal>

    <nz-modal [nzVisible]="transferOpen()" nzTitle="Transfer between cash and bank accounts" (nzOnCancel)="transferOpen.set(false)" (nzOnOk)="runTransfer()" nzOkText="Transfer" [nzOkLoading]="busy()">
      <ng-container *nzModalContent>
        <nz-select [(ngModel)]="trEntity" style="width:100%;margin-bottom:12px" nzPlaceHolder="Legal entity" data-testid="tr-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select>
        <nz-select [(ngModel)]="trFrom" style="width:100%;margin-bottom:12px" nzPlaceHolder="From" data-testid="tr-from">@for (a of cashAccounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
        <nz-select [(ngModel)]="trTo" style="width:100%;margin-bottom:12px" nzPlaceHolder="To" data-testid="tr-to">@for (a of cashAccounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
        <nz-input-number [(ngModel)]="trAmount" [nzMin]="0.01" [nzPrecision]="2" style="width:100%;margin-bottom:12px" data-testid="tr-amount"></nz-input-number>
        <nz-date-picker [(ngModel)]="trDate" style="width:100%"></nz-date-picker>
      </ng-container>
    </nz-modal>`,
})
export class BankPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(BankApi);
  private readonly message = inject(NzMessageService);
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
    this.api.position(this.date(), this.entityId ?? undefined).subscribe((p) => {
      this.position.set(p);
      const id = this.accountId() ?? p.accounts.find((a) => a.isBank)?.accountId ?? p.accounts[0]?.accountId ?? null;
      if (id) this.select(id);
    });
  }

  select(accountId: string): void {
    this.accountId.set(accountId);
    this.api.reconciliation(accountId, this.date(), this.entityId ?? undefined).subscribe((r) => this.rec.set(r));
  }

  runImport(): void {
    const account = this.accountId();
    if (!account || !this.entityId) { this.message.warning('Choose a legal entity and an account'); return; }
    const lines: { date: string; description: string; reference: string | null; amount: number }[] = [];
    for (const raw of this.importText.split('\n').map((l) => l.trim()).filter(Boolean)) {
      const [date, description, reference, amount] = raw.split(',').map((p) => p.trim());
      const value = Number(amount);
      if (!/^\d{4}-\d{2}-\d{2}$/.test(date ?? '') || !description || !Number.isFinite(value) || value === 0) { this.message.error(`${this.i18n.t('ui.bank.cannotReadLine', 'Cannot read this line:')} ${raw}`); return; }
      lines.push({ date, description, reference: reference || null, amount: value });
    }
    if (lines.length === 0) return;
    this.busy.set(true);
    this.api.import(account, this.entityId, lines).subscribe({
      next: (r) => { this.busy.set(false); this.importOpen.set(false); this.importText = ''; this.message.success(`${r.imported} ${this.i18n.t('ui.msg.importedLc', 'imported')}, ${r.duplicates} ${this.i18n.t('ui.msg.alreadyThere', 'already there')}`); this.select(account); },
      error: (e) => this.fail(e),
    });
  }

  autoMatch(): void {
    const account = this.accountId();
    if (!account || !this.entityId) return;
    this.api.autoMatch(account, this.entityId).subscribe({ next: (r) => { this.message.success(`${r.matched} ${this.i18n.t('ui.bank.linesMatched', 'lines matched')}`); this.select(account); }, error: (e) => this.fail(e) });
  }

  startBook(l: StatementLine): void { this.bookLine.set(l); this.bookAccount = null; this.bookMemo = ''; }

  runBook(): void {
    const l = this.bookLine();
    if (!l || !this.bookAccount) { this.message.warning('Choose the account to book it to'); return; }
    this.busy.set(true);
    this.api.book(l.id, this.bookAccount, this.bookMemo || undefined).subscribe({
      next: () => { this.busy.set(false); this.bookLine.set(null); this.message.success('Booked and matched'); this.refresh(); },
      error: (e) => this.fail(e),
    });
  }

  runTransfer(): void {
    if (!this.trEntity || !this.trFrom || !this.trTo || !this.trAmount) { this.message.warning('Fill in every field'); return; }
    this.busy.set(true);
    this.api.transfer({ legalEntityId: this.trEntity, fromAccountId: this.trFrom, toAccountId: this.trTo, amount: this.trAmount, date: toIsoDate(this.trDate), memo: null }).subscribe({
      next: () => { this.busy.set(false); this.transferOpen.set(false); this.message.success('Transfer posted'); this.refresh(); },
      error: (e) => this.fail(e),
    });
  }

  private fail(e: unknown): void { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not complete the action'); }
}
