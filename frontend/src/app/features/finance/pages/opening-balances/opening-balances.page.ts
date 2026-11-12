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
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AccountDto, AccountsApi, JournalSummary, JournalsApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { OpeningBalanceLine, OpeningBalancesApi } from '@core/api/period-close.api';
import { toIsoDate } from '@core/format/dates';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { loadInto } from '@shared/utils/load-into';
import { askReason } from '@shared/utils/reason-prompt';

/** One row of the opening-balances grid as typed (amounts and party may still be empty). */
interface DraftLine { accountId: string | null; debit: number | null; credit: number | null; partyId: string | null; dueDate: Date | null; reference: string; }
const blankLine = (): DraftLine => ({ accountId: null, debit: null, credit: null, partyId: null, dueDate: null, reference: '' });

/** A row with an account and an amount: the rows that are posted. */
const isFilled = (l: DraftLine): boolean => !!l.accountId && ((l.debit ?? 0) > 0 || (l.credit ?? 0) > 0);

/** Sum of one side of the grid, rounded to cents. */
const sideTotal = (lines: DraftLine[], key: 'debit' | 'credit'): number => Math.round(lines.reduce((s, l) => s + (l[key] ?? 0), 0) * 100) / 100;

/** A filled row as the API takes it. */
const toOpeningLine = (l: DraftLine): OpeningBalanceLine => ({
  accountId: l.accountId!, debit: l.debit ?? 0, credit: l.credit ?? 0, partyId: l.partyId, dueDate: l.dueDate ? toIsoDate(l.dueDate) : null, reference: l.reference || null,
});

/** Receivable and payable control accounts need a party and raise open invoices and bills. */
export const needsParty = (account: Pick<AccountDto, 'systemRole'> | undefined): boolean => account?.systemRole === 'ar.control' || account?.systemRole === 'ap.control';

/** Opening balances: the figures the books start from. The difference to the opening equity account is added automatically. */
@Component({
  selector: 'erp-opening-balances',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
    NzAlertModule,
    NzButtonModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    PagedTableComponent,
    StatusTagComponent,
    HasPermissionDirective,
  ],
  styleUrl: './opening-balances.page.scss',
  templateUrl: './opening-balances.page.html',
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

  readonly totalDebit = computed(() => sideTotal(this.lines(), 'debit'));
  readonly totalCredit = computed(() => sideTotal(this.lines(), 'credit'));
  readonly equityDifference = computed(() => Math.round((this.totalDebit() - this.totalCredit()) * 100) / 100);
  readonly canSave = computed(() => !!this.entityId && this.filled().length > 0 && this.filled().every((l) => !this.partyRequired(l) || !!l.partyId) && !this.saving());
  private readonly filled = () => this.lines().filter(isFilled);

  constructor() {
    // The opening equity account takes the difference automatically, so it is not offered as a line.
    loadInto(this.accounts, inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }), (r) => r.items.filter((a) => a.isPostable && a.isActive && a.systemRole !== 'equity.opening'));
  }

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
    loadInto(this.parties, this.partiesApi.list({ page: 1, pageSize: 20, search: term }), (r) => r.items.map((p) => ({ value: p.id, label: p.name })));
  }

  save(): void {
    this.saving.set(true);
    this.serverError.set(null);
    this.api.post({ legalEntityId: this.entityId!, date: toIsoDate(this.date), memo: this.memo.trim() || null, lines: this.filled().map(toOpeningLine) }).subscribe({
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

  private account(l: DraftLine): AccountDto | undefined { return this.accounts().find((a) => a.id === l.accountId); }
}
