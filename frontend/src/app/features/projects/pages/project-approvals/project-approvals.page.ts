import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { catchError, of } from 'rxjs';
import { ExpenseDto, ProjectExpensesApi, ProjectsExtraApi } from '@core/api/projects-extra.api';
import { ProjectTimesheetsApi, TimesheetDto } from '@core/api/projects.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { errorText, PRJ_STYLES } from '../../projects-ui';
import { ApprovalGroup, ApprovalLine, expenseApprovalLines, groupByPerson, timeApprovalLines } from '../../projects-view';

/** An inbox of time and expenses waiting for a decision: grouped by person, with the project on every line. Approve whole people or single lines. */
@Component({
  selector: 'erp-project-approvals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule],
  templateUrl: './project-approvals.page.html',
  styles: [PRJ_STYLES + `
    .person { margin-bottom:12px; padding:12px 14px; } header { display:flex; justify-content:space-between; gap:10px; flex-wrap:wrap; margin-bottom:6px; }
    table { width:100%; border-collapse:collapse; font-size:13px; min-width:600px; } td { padding:6px 8px; border-top:1px solid var(--erp-color-border-subtle,#f1f5f9); } .tick { width:30px; } .date { width:100px; color:var(--erp-color-text-secondary,#64748b); } .what { min-width:180px; }
  `],
})
export class ProjectApprovalsPage {
  /** time or expenses, from ?tab= in the address. */
  readonly tab = input<string | null>(null);

  private readonly timeApi = inject(ProjectTimesheetsApi);
  private readonly costApi = inject(ProjectExpensesApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly time = signal<TimesheetDto[]>([]);
  readonly costs = signal<ExpenseDto[]>([]);
  readonly picked = signal(new Set<string>());
  readonly asking = signal(false);
  reason = '';

  readonly canTime = computed(() => this.session.hasPermission('projects.timesheets.approve'));
  readonly canCost = computed(() => this.session.hasPermission('projects.expenses.approve'));
  readonly active = computed<'time' | 'expenses'>(() => { const t = this.tab(); return (t === 'expenses' && this.canCost()) || !this.canTime() ? 'expenses' : 'time'; });

  readonly lines = computed<ApprovalLine[]>(() => (this.active() === 'time' ? timeApprovalLines(this.time()) : expenseApprovalLines(this.costs())));
  readonly groups = computed<ApprovalGroup[]>(() => groupByPerson(this.lines()));
  readonly pickedAmount = computed(() => this.lines().filter((l) => this.picked().has(l.id)).reduce((a, l) => a + l.amount, 0));

  constructor() {
    this.load();
    effect(() => { this.active(); this.picked.set(new Set()); });
  }

  /** Loads what waits for the person's decision; a list that cannot be loaded is shown empty. Any picks are cleared. */
  load(): void {
    if (this.canTime()) {
      const time$ = this.timeApi.list({ page: 1, pageSize: 200, sort: 'date', filters: [{ field: 'approvalStatus', op: 'eq', value: 'submitted' }] });
      time$.pipe(catchError(() => of({ items: [] as TimesheetDto[] }))).subscribe((r) => { this.time.set(r.items); this.picked.set(new Set()); });
    }
    if (this.canCost()) {
      const costs$ = this.costApi.list({ page: 1, pageSize: 200, sort: 'date', filters: [{ field: 'status', op: 'eq', value: 'submitted' }] });
      costs$.pipe(catchError(() => of({ items: [] as ExpenseDto[] }))).subscribe((r) => { this.costs.set(r.items); this.picked.set(new Set()); });
    }
  }

  go(tab: 'time' | 'expenses'): void { void this.router.navigate([], { queryParams: { tab }, queryParamsHandling: 'merge', replaceUrl: true }); }
  pick(id: string, on: boolean): void { this.pickAll([id], on); }
  allOf(g: ApprovalGroup): boolean { return g.lines.every((l) => this.picked().has(l.id)); }
  pickGroup(g: ApprovalGroup, on: boolean): void { this.pickAll(g.lines.map((l) => l.id), on); }
  selectAll(): void { this.picked.set(this.picked().size === this.lines().length ? new Set() : new Set(this.lines().map((l) => l.id))); }

  decide(approve: boolean, reason?: string): void {
    const ids = [...this.picked()];
    const call = this.active() === 'time' ? this.extra.decideTime(ids, approve, reason) : this.costApi.decide(ids, approve, reason);
    call.subscribe({
      next: (r) => { this.msg.success(`${r.decided} ${r.decided === 1 ? 'line' : 'lines'} ${approve ? 'approved' : 'rejected'}`); this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not save the decision.')),
    });
  }
  askReason(): void { this.reason = ''; this.asking.set(true); }
  reject(): void {
    if (!this.reason.trim()) { this.msg.warning('Say why it is rejected.'); return; }
    this.asking.set(false);
    this.decide(false, this.reason.trim());
  }

  private pickAll(ids: string[], on: boolean): void {
    const next = new Set(this.picked());
    for (const id of ids) { if (on) next.add(id); else next.delete(id); }
    this.picked.set(next);
  }
}
