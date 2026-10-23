import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { AccountDto, AccountsApi, BudgetDetail, BudgetSummary, BudgetVsActual, BudgetsApi, LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
interface Row { accountId: string; label: string; type: string; months: number[]; }

/** Budgets: a monthly grid per income/expense account, and the comparison with what the ledger actually posted. */
@Component({
  selector: 'erp-budgets',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule, NzTableModule, NzTabsModule],
  styles: [`.bar { display:flex; gap:12px; flex-wrap:wrap; align-items:center; margin-bottom:12px } .num { text-align:right } .bad { color: var(--erp-color-error, #cf1322) } .good { color: var(--erp-color-success, #389e0d) } .grid { overflow:auto }`],
  template: `
    @if (!editing()) {
      <h1 class="erp-page-title">Budgets</h1>
      <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search budgets" emptyText="No budgets yet" (rowClick)="open($event.id)">
        <button toolbar *erpHasPermission="'finance.budgets.write'" nz-button nzType="primary" (click)="create()" data-testid="budget-new"><span nz-icon nzType="plus"></span>New budget</button>
      </erp-paged-table>
    } @else {
      <div class="bar">
        <button nz-button (click)="close()">Back</button>
        <h1 class="erp-page-title" style="margin:0">{{ id() ? 'Budget' : 'New budget' }}</h1>
        <span style="flex:1"></span>
        @if (id()) { <button *erpHasPermission="'finance.budgets.write'" nz-button nzDanger (click)="remove()" data-testid="budget-delete">Delete</button> }
        <button *erpHasPermission="'finance.budgets.write'" nz-button nzType="primary" [nzLoading]="saving()" (click)="save()" data-testid="budget-save">Save</button>
      </div>
      <nz-tabset>
        <nz-tab nzTitle="Plan">
          <div class="bar">
            <nz-select style="width:240px" nzPlaceHolder="Legal entity" [(ngModel)]="entityId" [nzDisabled]="!!id()" data-testid="budget-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select>
            <input nz-input style="width:240px" placeholder="Name" [(ngModel)]="name" data-testid="budget-name" />
            <nz-input-number [(ngModel)]="year" [nzMin]="2000" [nzMax]="2100" [nzPrecision]="0" data-testid="budget-year"></nz-input-number>
            <nz-select style="width:280px" nzShowSearch nzPlaceHolder="Add an income/expense account" [ngModel]="null" (ngModelChange)="addRow($event)" data-testid="budget-add-account">
              @for (a of addable(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }
            </nz-select>
          </div>
          <div class="grid">
            <table class="erp-grid" style="border-collapse:collapse;min-width:1200px" data-testid="budget-grid">
              <thead><tr><th style="text-align:left;min-width:220px">Account</th>@for (m of months; track m) { <th class="num">{{ m }}</th> }<th class="num">Total</th><th></th></tr></thead>
              <tbody>
                @for (r of rows(); track r.accountId; let i = $index) {
                  <tr>
                    <td>{{ r.label }}</td>
                    @for (m of months; track m; let mi = $index) {
                      <td><nz-input-number [ngModel]="r.months[mi]" (ngModelChange)="setCell(i, mi, $event)" [nzMin]="0" [nzStep]="100" [nzPrecision]="0" style="width:84px" [attr.data-testid]="'cell-' + i + '-' + mi"></nz-input-number></td>
                    }
                    <td class="num"><b>{{ total(r) | number: '1.0-0' }}</b></td>
                    <td><button nz-button nzType="text" nzSize="small" (click)="removeRow(i)"><span nz-icon nzType="delete"></span></button></td>
                  </tr>
                } @empty { <tr><td colspan="15">Add accounts to start planning.</td></tr> }
              </tbody>
            </table>
          </div>
        </nz-tab>
        @if (id()) {
          <nz-tab nzTitle="Budget vs actual" (nzSelect)="loadActual()">
            <div class="bar">
              <nz-select style="width:110px" [(ngModel)]="fromMonth" (ngModelChange)="loadActual()">@for (m of months; track m; let i = $index) { <nz-option [nzValue]="i + 1" [nzLabel]="m"></nz-option> }</nz-select>
              <span>to</span>
              <nz-select style="width:110px" [(ngModel)]="toMonth" (ngModelChange)="loadActual()">@for (m of months; track m; let i = $index) { <nz-option [nzValue]="i + 1" [nzLabel]="m"></nz-option> }</nz-select>
            </div>
            @if (actual(); as a) {
              <p data-testid="budget-net">Net budget {{ a.budgetNet | number: '1.0-0' }} · Net actual {{ a.actualNet | number: '1.0-0' }}</p>
              <nz-table [nzData]="a.rows" nzSize="small" [nzShowPagination]="false" data-testid="budget-actual-table">
                <thead><tr><th>Account</th><th class="num">Budget</th><th class="num">Actual</th><th class="num">Variance</th><th class="num">%</th></tr></thead>
                <tbody>
                  @for (r of a.rows; track r.accountId) {
                    <tr><td>{{ r.code }} · {{ r.name }}</td><td class="num">{{ r.budget | number: '1.0-0' }}</td><td class="num">{{ r.actual | number: '1.0-0' }}</td>
                      <td class="num" [class.bad]="r.variance < 0" [class.good]="r.variance > 0">{{ r.variance | number: '1.0-0' }}</td><td class="num">{{ r.variancePercent ?? '—' }}</td></tr>
                  }
                </tbody>
              </nz-table>
            }
          </nz-tab>
        }
      </nz-tabset>
    }`,
})
export class BudgetsPage {
  private readonly api = inject(BudgetsApi);
  private readonly accountsApi = inject(AccountsApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
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
      const byAccount = new Map<string, Row>();
      for (const l of b.lines) {
        const row = byAccount.get(l.accountId) ?? { accountId: l.accountId, label: `${l.accountCode} · ${l.accountName}`, type: '', months: Array(12).fill(0) };
        row.months[l.month - 1] = l.amount;
        byAccount.set(l.accountId, row);
      }
      this.rows.set([...byAccount.values()]);
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
    const lines = this.rows().flatMap((r) => r.months.map((amount, i) => ({ accountId: r.accountId, month: i + 1, amount })).filter((l) => l.amount > 0));
    const body = { legalEntityId: this.entityId, name: this.name, fiscalYear: this.year, isActive: true, lines };
    this.saving.set(true);
    const id = this.id();
    (id ? this.api.update(id, body) : this.api.create(body)).subscribe({
      next: (b) => { this.saving.set(false); this.message.success('Budget saved'); this.id.set((b as unknown as BudgetDetail).id); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save the budget'); },
    });
  }

  remove(): void {
    const id = this.id();
    if (id) this.api.delete(id).subscribe(() => { this.message.success('Budget deleted'); this.close(); });
  }

  loadActual(): void {
    const id = this.id();
    if (id) this.api.vsActual(id, this.fromMonth, Math.max(this.fromMonth, this.toMonth)).subscribe((a) => this.actual.set(a));
  }
}
