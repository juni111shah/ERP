import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AccountDto, AccountsApi, JournalSummary, JournalsApi } from '@core/api/admin.api';
import { OpeningBalanceLine, OpeningBalancesApi } from '@core/api/period-close.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { askReason } from '@shared/utils/reason-prompt';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { PartiesApi } from '@features/parties/pages/parties/parties.page';

interface DraftLine { accountId: string | null; debit: number | null; credit: number | null; partyId: string | null; dueDate: Date | null; reference: string; }
const blankLine = (): DraftLine => ({ accountId: null, debit: null, credit: null, partyId: null, dueDate: null, reference: '' });

/** Receivable and payable control accounts need a party and raise open invoices and bills. */
export const needsParty = (account: Pick<AccountDto, 'systemRole'> | undefined): boolean => account?.systemRole === 'ar.control' || account?.systemRole === 'ap.control';

/** Opening balances: the figures the books start from. The difference to the opening equity account is added automatically. */
@Component({
  selector: 'erp-opening-balances',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, DecimalPipe, FormsModule, RouterLink, NzAlertModule, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzIconModule, NzInputModule, NzInputNumberModule, NzSelectModule, HasPermissionDirective, PagedTableComponent, StatusTagComponent],
  styles: [`
    .head { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 16px; }
    .field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: var(--erp-color-text-muted); }
    .line { display: grid; grid-template-columns: 2.2fr 1fr 1fr 1.6fr 1.2fr auto; gap: 8px; margin-bottom: 8px; align-items: start; }
    .line.head-row { font-size: 12px; color: var(--erp-color-text-muted); margin-bottom: 4px; }
    .totals { display: flex; justify-content: space-between; margin-top: 12px; font-weight: 600; }
    .warn { color: var(--erp-color-danger, #cf1322); }
    .hint { font-size: 12px; color: var(--erp-color-text-muted); }
  `],
  template: `
    <h1 class="erp-page-title">Opening balances</h1>
    <p>Enter the balances you start the books with. Lines on the receivables account become open customer invoices and lines on the payables account become open vendor bills, so ageing and payments work from day one. The difference goes to the opening equity account.</p>
    <erp-paged-table [list]="list" [columns]="columns" [searchable]="false" [clickable]="false" emptyText="No opening balances have been entered.">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('number') { <a [routerLink]="['/finance/journals', row.id]">{{ row.number }}</a> }
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('amount') { {{ row.amount | number: '1.2-2' }} }
          @case ('actions') {
            @if (row.status === 'posted') {
              <button *erpHasPermission="'finance.journals.reverse'" nz-button nzSize="small" nzDanger type="button" (click)="reverse(row)" data-testid="opening-reverse">Reverse</button>
            }
          }
          @default { {{ value }} }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'finance.journals.post'" nz-button nzType="primary" (click)="openForm()" data-testid="opening-new"><span nz-icon nzType="plus"></span>Enter opening balances</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="open()" nzTitle="Enter opening balances" [nzWidth]="980" (nzOnClose)="open.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        @if (serverError(); as msg) { <nz-alert nzType="error" nzShowIcon [nzMessage]="msg" style="margin-bottom:12px" data-testid="opening-error"></nz-alert> }
        <div class="head">
          <div class="field">Legal entity
            <nz-select [(ngModel)]="entityId" style="width:240px" data-testid="opening-entity">@for (o of entities; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label" /> }</nz-select>
          </div>
          <div class="field">As of date <nz-date-picker [(ngModel)]="date" [nzAllowClear]="false" data-testid="opening-date" /></div>
          <div class="field" style="flex:1;min-width:200px">Memo <input nz-input [(ngModel)]="memo" maxlength="300" data-testid="opening-memo" /></div>
        </div>
        <div class="line head-row" aria-hidden="true"><span>Account</span><span>Debit</span><span>Credit</span><span>Party</span><span>Due date</span><span></span></div>
        @for (l of lines(); track $index; let i = $index) {
          <div class="line" [attr.data-testid]="'opening-line-' + i">
            <nz-select [ngModel]="l.accountId" (ngModelChange)="set(i, { accountId: $event })" nzShowSearch nzPlaceHolder="Account" [attr.aria-label]="'Account, line ' + (i + 1)">
              @for (a of accounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name" /> }
            </nz-select>
            <nz-input-number [ngModel]="l.debit" (ngModelChange)="set(i, { debit: $event })" [nzMin]="0" [nzPrecision]="2" style="width:100%" [attr.aria-label]="'Debit, line ' + (i + 1)" />
            <nz-input-number [ngModel]="l.credit" (ngModelChange)="set(i, { credit: $event })" [nzMin]="0" [nzPrecision]="2" style="width:100%" [attr.aria-label]="'Credit, line ' + (i + 1)" />
            <div>
              <nz-select [ngModel]="l.partyId" (ngModelChange)="set(i, { partyId: $event })" nzShowSearch nzServerSearch nzAllowClear (nzOnSearch)="searchParties($event)" [nzPlaceHolder]="partyRequired(l) ? 'Required' : 'Optional'" [attr.aria-label]="'Party, line ' + (i + 1)" style="width:100%">
                @for (p of parties(); track p.value) { <nz-option [nzValue]="p.value" [nzLabel]="p.label" /> }
              </nz-select>
              @if (partyRequired(l) && !l.partyId) { <div class="hint warn">A {{ partyKind(l) }} is needed on this account.</div> }
            </div>
            <nz-date-picker [ngModel]="l.dueDate" (ngModelChange)="set(i, { dueDate: $event })" [attr.aria-label]="'Due date, line ' + (i + 1)" style="width:100%" />
            <button nz-button nzSize="small" nzDanger type="button" (click)="remove(i)" [attr.aria-label]="'Remove line ' + (i + 1)">Remove</button>
          </div>
        }
        <button nz-button nzSize="small" type="button" (click)="add()" data-testid="opening-add">Add line</button>
        <div class="totals" data-testid="opening-totals">
          <span>Debit {{ totalDebit() | number: '1.2-2' }}</span><span>Credit {{ totalCredit() | number: '1.2-2' }}</span>
          <span>Opening equity {{ equityDifference() | number: '1.2-2' }} {{ equityDifference() >= 0 ? 'credit' : 'debit' }}</span>
        </div>
        <p class="hint">Do not add the opening equity account or any income or expense account. The difference is posted to opening equity for you.</p>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button type="button" (click)="open.set(false)">Cancel</button>
      <button nz-button nzType="primary" type="button" [nzLoading]="saving()" [disabled]="!canSave()" (click)="save()" style="margin-inline-start:8px" data-testid="opening-save">Post opening balances</button>
    </ng-template>`,
})
export class OpeningBalancesPage {
  private readonly api = inject(OpeningBalancesApi);
  private readonly journals = inject(JournalsApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly partiesApi = inject(PartiesApi);

  readonly list = createPagedList((q) => this.journals.list({ ...q, filters: [...(q.filters ?? []), { field: 'sourceType', op: 'eq', value: 'opening' }] }), { sort: '-date' });
  readonly columns: TableColumn<JournalSummary>[] = [
    { key: 'number', label: 'Number', filter: false },
    { key: 'date', label: 'As of', sortField: 'date', filter: false, value: (j) => displayDate(j.date) },
    { key: 'memo', label: 'Memo', filter: false },
    { key: 'amount', label: 'Amount', align: 'right', filter: false },
    { key: 'status', label: 'Status', filter: false },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly entities = legalEntityOptions(this.session);
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly serverError = signal<string | null>(null);
  readonly accounts = signal<AccountDto[]>([]);
  readonly parties = signal<{ value: string; label: string }[]>([]);
  readonly lines = signal<DraftLine[]>([blankLine(), blankLine()]);
  entityId: string | null = defaultLegalEntityId(this.session);
  date: Date = new Date();
  memo = '';

  readonly totalDebit = computed(() => this.sum('debit'));
  readonly totalCredit = computed(() => this.sum('credit'));
  readonly equityDifference = computed(() => Math.round((this.totalDebit() - this.totalCredit()) * 100) / 100);
  readonly canSave = computed(() => !!this.entityId && this.filled().length > 0 && this.filled().every((l) => !this.partyRequired(l) || !!l.partyId) && !this.saving());
  private filled = () => this.lines().filter((l) => l.accountId && ((l.debit ?? 0) > 0 || (l.credit ?? 0) > 0));

  constructor() {
    inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({ next: (r) => this.accounts.set(r.items.filter((a) => a.isPostable && a.isActive && a.systemRole !== 'equity.opening')), error: () => undefined });
  }

  private sum(key: 'debit' | 'credit'): number { return Math.round(this.lines().reduce((s, l) => s + (l[key] ?? 0), 0) * 100) / 100; }
  private account(l: DraftLine): AccountDto | undefined { return this.accounts().find((a) => a.id === l.accountId); }
  partyRequired(l: DraftLine): boolean { return needsParty(this.account(l)); }
  partyKind(l: DraftLine): string { return this.account(l)?.systemRole === 'ap.control' ? 'vendor' : 'customer'; }

  openForm(): void {
    this.lines.set([blankLine(), blankLine()]);
    this.memo = '';
    this.serverError.set(null);
    this.open.set(true);
  }

  set(i: number, patch: Partial<DraftLine>): void { this.lines.update((ls) => ls.map((l, n) => (n === i ? { ...l, ...patch } : l))); }
  add(): void { this.lines.update((ls) => [...ls, blankLine()]); }
  remove(i: number): void { this.lines.update((ls) => (ls.length > 1 ? ls.filter((_, n) => n !== i) : ls)); }

  searchParties(term: string): void {
    this.partiesApi.list({ page: 1, pageSize: 20, search: term }).subscribe({ next: (r) => this.parties.set(r.items.map((p) => ({ value: p.id, label: p.name }))), error: () => undefined });
  }

  save(): void {
    const lines: OpeningBalanceLine[] = this.filled().map((l) => ({
      accountId: l.accountId!, debit: l.debit ?? 0, credit: l.credit ?? 0, partyId: l.partyId, dueDate: l.dueDate ? toIsoDate(l.dueDate) : null, reference: l.reference || null,
    }));
    this.saving.set(true);
    this.serverError.set(null);
    this.api.post({ legalEntityId: this.entityId!, date: toIsoDate(this.date), memo: this.memo.trim() || null, lines }).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.open.set(false);
        this.message.success(doneText('posted', 'Opening balances', r.number));
        this.list.reload();
      },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        this.serverError.set(problemMessage(e, 'Could not post the opening balances'));
        if (p?.status === 409) this.message.warning(p.detail ?? 'Opening balances already exist for this legal entity.');
      },
    });
  }

  reverse(row: JournalSummary): void {
    askReason(this.modal, 'Reverse the opening balances?', 'Reverse', 'The journal is reversed on its own date. This is refused once any of the invoices or bills it raised has a payment against it.').then((reason) => {
      if (!reason) return;
      this.api.reverse(row.id, reason).subscribe({
        next: () => { this.message.success(doneText('voided', 'Opening balances', row.number)); this.list.reload(); },
        error: (e) => this.message.error(problemMessage(e, 'Could not reverse the opening balances', 'reason')),
      });
    });
  }
}
