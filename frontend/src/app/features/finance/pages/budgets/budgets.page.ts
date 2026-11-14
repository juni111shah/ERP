import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { AccountDto, AccountsApi, BudgetDetail, BudgetLineValue, BudgetSummary, BudgetVsActual, BudgetsApi, LegalEntitiesApi, LegalEntityDto } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CanPipe } from '@shared/pipes/can.pipe';
import { RevealDirective } from '@shared/directives/reveal.directive';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** One account row of the budget grid: twelve monthly amounts. */
interface Row { accountId: string; label: string; type: string; months: number[]; }

/** A saved budget's lines (one per account and month) as grid rows. */
function budgetRows(lines: BudgetLineValue[]): Row[] {
  const byAccount = new Map<string, Row>();
  for (const l of lines) {
    const row = byAccount.get(l.accountId) ?? { accountId: l.accountId, label: `${l.accountCode} · ${l.accountName}`, type: '', months: Array(12).fill(0) };
    row.months[l.month - 1] = l.amount;
    byAccount.set(l.accountId, row);
  }
  return [...byAccount.values()];
}

/** The grid as budget lines: one per account and month with an amount. */
function budgetLines(rows: Row[]): { accountId: string; month: number; amount: number }[] {
  return rows.flatMap((r) => r.months.map((amount, i) => ({ accountId: r.accountId, month: i + 1, amount })).filter((l) => l.amount > 0));
}

/** Budgets: a monthly grid per income/expense account, and the comparison with what the ledger actually posted. */
@Component({
  selector: 'erp-budgets',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzTableModule,
    NzTabsModule,
    NzToolTipModule,
    TranslatePipe,
    PagedTableComponent,
    StatCardComponent,
    HasPermissionDirective,
    CanPipe,
    RevealDirective,
  ],
  styleUrl: './budgets.page.scss',
  templateUrl: './budgets.page.html',
})
export class BudgetsPage {
  private readonly api = inject(BudgetsApi);
  private readonly accountsApi = inject(AccountsApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly months = MONTHS;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-fiscalYear' });
  readonly columns: TableColumn<BudgetSummary>[] = [
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'fiscalYear', label: 'Year', sortField: 'fiscalYear', width: '100px' },
    { key: 'isActive', label: 'Active', value: (b) => (b.isActive ? 'Yes' : 'No'), width: '90px' },
  ];

  readonly editing = signal(false);
  readonly id = signal<string | null>(null);
  readonly saving = signal(false);
  readonly entities = signal<LegalEntityDto[]>([]);
  readonly accounts = signal<AccountDto[]>([]);
  readonly rows = signal<Row[]>([]);
  readonly actual = signal<BudgetVsActual | null>(null);
  entityId: string | null = null;
  name = '';
  year = new Date().getFullYear();
  fromMonth = 1;
  toMonth = 12;

  /** Postable income and expense accounts not in the grid yet. */
  readonly addable = computed(() => {
    const used = new Set(this.rows().map((r) => r.accountId));
    return this.accounts().filter((a) => (a.type === 'income' || a.type === 'expense') && a.isPostable && !used.has(a.id));
  });

  constructor() {
    this.entitiesApi.list({ page: 1, pageSize: 100 }).subscribe((r) => this.entities.set(r.items));
    this.accountsApi.list({ page: 1, pageSize: 500 }).subscribe((r) => this.accounts.set(r.items));
  }

  create(): void { this.id.set(null); this.name = ''; this.year = new Date().getFullYear(); this.entityId = this.entities()[0]?.id ?? null; this.rows.set([]); this.actual.set(null); this.editing.set(true); }

  open(id: string): void {
    this.api.detail(id).subscribe((b: BudgetDetail) => {
      this.id.set(b.id); this.name = b.name; this.year = b.fiscalYear; this.entityId = b.legalEntityId; this.actual.set(null);
      this.rows.set(budgetRows(b.lines));
      this.editing.set(true);
    });
  }

  close(): void { this.editing.set(false); this.list.reload(); }

  addRow(accountId: string | null): void {
    const a = this.accounts().find((x) => x.id === accountId);
    if (a) this.rows.update((r) => [...r, { accountId: a.id, label: `${a.code} · ${a.name}`, type: a.type, months: Array(12).fill(0) }]);
  }

  removeRow(i: number): void { this.rows.update((r) => r.filter((_, idx) => idx !== i)); }

  setCell(row: number, month: number, value: number | null): void {
    this.rows.update((rs) => rs.map((r, i) => (i === row ? { ...r, months: r.months.map((m, mi) => (mi === month ? (value ?? 0) : m)) } : r)));
  }

  total(r: Row): number { return r.months.reduce((a, b) => a + (b ?? 0), 0); }

  save(): void {
    if (!this.entityId || !this.name.trim()) { this.message.warning('Choose a legal entity and enter a name'); return; }
    const body = { legalEntityId: this.entityId, name: this.name, fiscalYear: this.year, isActive: true, lines: budgetLines(this.rows()) };
    this.saving.set(true);
    const id = this.id();
    (id ? this.api.update(id, body) : this.api.create(body)).subscribe({
      next: (b) => { this.saving.set(false); this.message.success(doneText('saved', 'Budget', this.name)); this.id.set((b as unknown as BudgetDetail).id); },
      error: (e) => { this.saving.set(false); this.message.error(problemMessage(e, 'Could not save the budget')); },
    });
  }

  remove(): void {
    const id = this.id();
    if (!id) return;
    this.modal.confirm({
      nzTitle: 'Delete this budget?', nzContent: 'All its monthly figures are removed.', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(id).subscribe({ next: () => { this.message.success(doneText('deleted', 'Budget', this.name)); this.close(); }, error: (e) => this.message.error(problemMessage(e, 'Could not delete the budget')) }),
    });
  }

  loadActual(): void {
    const id = this.id();
    if (id) this.api.vsActual(id, this.fromMonth, Math.max(this.fromMonth, this.toMonth)).subscribe((a) => this.actual.set(a));
  }
}
